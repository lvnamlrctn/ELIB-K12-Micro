using System.Security.Claims;
using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Store/Statistics")]
[Authorize]
public class StoreStatisticsController(ELIBAPIDbContext db, ISystemParameterService sysParam) : ControllerBase
{
    private long? GetTenantId()
    {
        var claim = User.FindFirstValue("TenantId");
        return long.TryParse(claim, out var id) ? id : null;
    }

    [HttpPost("Document")]
    [Permission("STAT_DOC", "view")]
    public async Task<IActionResult> Document([FromBody] StoreDocumentStatsRequest? r)
    {
        r ??= new StoreDocumentStatsRequest();
        var items = await BuildStatsAsync(r);
        var result = items.Select(x => new { key = x.key, label = x.label, count = x.count, detail = x.detail });
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("DocumentExport")]
    [Permission("STAT_DOC", "view")]
    public async Task<IActionResult> DocumentExport([FromBody] StoreDocumentStatsRequest? r)
    {
        r ??= new StoreDocumentStatsRequest();
        var items = await BuildStatsAsync(r);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Thống kê tài liệu");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "THỐNG KÊ TÀI LIỆU KHO", 3);
        ws.Cell(startRow, 1).Value = "Tiêu chí";
        ws.Cell(startRow, 2).Value = "Số biểu ghi";
        ws.Cell(startRow, 3).Value = "Số bản";

        int row = startRow + 1;
        foreach (var x in items)
        {
            ws.Cell(row, 1).Value = x.label;
            ws.Cell(row, 2).Value = x.count;
            ws.Cell(row, 3).Value = x.detail;
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, 3);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "store-document-statistics.xlsx");
    }

    private async Task<List<DocumentStatRow>> BuildStatsAsync(StoreDocumentStatsRequest r)
    {
        switch (r.Criterion?.ToLower())
        {
            case "status":
            {
                var tenantId = GetTenantId();
                var statusNames = await db.BarcodeStatuses
                    .Where(s => s.IsDelete != 2 && (!tenantId.HasValue || s.TenantId == tenantId))
                    .ToDictionaryAsync(s => s.Id ?? "", s => s.CommentStatus ?? s.Id ?? "");

                var groups = await FilterBarcodes(r).GroupBy(x => x.Status)
                    .Select(g => new { key = g.Key, count = g.Select(x => x.BibId).Distinct().Count(), detail = g.Count() })
                    .ToListAsync();
                return groups.Select(g => new DocumentStatRow(
                    g.key ?? "",
                    g.key != null && statusNames.TryGetValue(g.key, out var name) ? name : (g.key ?? "(Không xác định)"),
                    g.count, g.detail)).ToList();
            }

            case "store":
            {
                var tenantId = GetTenantId();
                var storeNames = await db.Stores
                    .Where(s => s.IsDelete != 2 && (!tenantId.HasValue || s.TenantId == tenantId))
                    .ToDictionaryAsync(s => s.Id, s => s.Name ?? s.Id.ToString());

                var groups = await FilterBarcodes(r).GroupBy(x => x.Store)
                    .Select(g => new { key = g.Key, count = g.Select(x => x.BibId).Distinct().Count(), detail = g.Count() })
                    .ToListAsync();
                return groups.Select(g => new DocumentStatRow(
                    g.key?.ToString() ?? "",
                    g.key.HasValue && storeNames.TryGetValue((long)g.key.Value, out var name) ? name : (g.key?.ToString() ?? "(Không xác định)"),
                    g.count, g.detail)).ToList();
            }

            case "ddc":
            {
                var dicClasses = await db.DicClasses
                    .Where(d => d.IsDelete != 2 && d.Code != null && d.Code != "")
                    .Select(d => new { d.Code, Label = d.VnDescription ?? d.Description ?? d.Code })
                    .ToListAsync();
                var sortedDic = dicClasses.OrderByDescending(d => d.Code!.Length).ToList();

                string MatchDdc(string? ddc)
                {
                    if (string.IsNullOrWhiteSpace(ddc)) return "Chưa phân loại";
                    var m = sortedDic.FirstOrDefault(d => ddc.StartsWith(d.Code!, StringComparison.OrdinalIgnoreCase));
                    return m != null ? $"{m.Code} - {m.Label}" : "Chưa phân loại";
                }

                var bibDdcValues = await (from b in FilterBibs(r)
                                           join x in db.BibXmls on b.Bibid equals x.BibId into xs
                                           from x in xs.DefaultIfEmpty()
                                           select x != null ? x.DDC : null)
                    .ToListAsync();
                var counts = bibDdcValues.GroupBy(MatchDdc).Select(g => (key: g.Key, count: g.Count())).ToList();

                var barcodeDdcValues = await (from bc in FilterBarcodes(r)
                                               join b in db.Bibs on bc.BibId equals b.Bibid into bs
                                               from b in bs.DefaultIfEmpty()
                                               join x in db.BibXmls on b.Bibid equals x.BibId into xs
                                               from x in xs.DefaultIfEmpty()
                                               select x != null ? x.DDC : null)
                    .ToListAsync();
                var details = barcodeDdcValues.GroupBy(MatchDdc).Select(g => (key: g.Key, detail: g.Count())).ToList();

                return MergeCounts(counts, details, k => k);
            }

            case "bibtype":
            default:
            {
                var tenantId = GetTenantId();
                var bibTypeNames = await db.BibTypes
                    .Where(t => t.IsDelete != 2 && (!tenantId.HasValue || t.TenantId == tenantId))
                    .ToDictionaryAsync(t => t.Id, t => t.Name ?? t.Id.ToString());

                string BibTypeLabel(string key) =>
                    long.TryParse(key, out var id) && bibTypeNames.TryGetValue(id, out var name) ? name : key;

                var countsRaw = await FilterBibs(r).GroupBy(b => b.Bib_type_id)
                    .Select(g => new { key = g.Key, count = g.Count() }).ToListAsync();

                var detailsRaw = await (from bc in FilterBarcodes(r)
                                      join bib in db.Bibs on bc.BibId equals bib.Bibid into bibs
                                      from bib in bibs.DefaultIfEmpty()
                                      group bc by bib.Bib_type_id into g
                                      select new { key = g.Key, detail = g.Count() })
                    .ToListAsync();

                return MergeCounts(
                    countsRaw.Select(x => (key: x.key?.ToString() ?? "(Không xác định)", x.count)),
                    detailsRaw.Select(x => (key: x.key?.ToString() ?? "(Không xác định)", x.detail)),
                    BibTypeLabel);
            }
        }
    }

    private static List<DocumentStatRow> MergeCounts<TKey>(
        IEnumerable<(TKey key, int count)> counts,
        IEnumerable<(TKey key, int detail)> details,
        Func<TKey, string> label) where TKey : notnull
    {
        var countMap = counts.ToDictionary(x => x.key, x => x.count);
        var detailMap = details.ToDictionary(x => x.key, x => x.detail);
        var keys = countMap.Keys.Union(detailMap.Keys);

        return keys.Select(k => new DocumentStatRow(
            k.ToString() ?? "",
            label(k),
            countMap.TryGetValue(k, out var c) ? c : 0,
            detailMap.TryGetValue(k, out var d) ? d : 0
        )).ToList();
    }

    private IQueryable<ELIBAPI.Core.Entities.PrintBook.Bib> FilterBibs(StoreDocumentStatsRequest r)
    {
        var tenantId = GetTenantId();
        var query = db.Bibs.Where(b => b.IsDelete != 2 && (!tenantId.HasValue || b.TenantId == tenantId));

        if (r.ReceiptDateFrom.HasValue) query = query.Where(b => b.CreatedRowDate >= r.ReceiptDateFrom);
        if (r.ReceiptDateTo.HasValue)   query = query.Where(b => b.CreatedRowDate <= r.ReceiptDateTo);
        if (r.StoreId.HasValue)
        {
            var storeId = (int)r.StoreId.Value;
            query = query.Where(b => db.Barcodes.Any(x => x.BibId == b.Bibid && x.IsDelete != 2 && x.Store == storeId));
        }

        return query;
    }

    private IQueryable<ELIBAPI.Core.Entities.PrintBook.Barcode> FilterBarcodes(StoreDocumentStatsRequest r)
    {
        var tenantId = GetTenantId();
        var query = db.Barcodes.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));

        if (r.StoreId.HasValue) query = query.Where(x => x.Store == (int?)r.StoreId);
        if (r.ReceiptDateFrom.HasValue) query = query.Where(x => x.CreatedRowDate >= r.ReceiptDateFrom);
        if (r.ReceiptDateTo.HasValue)   query = query.Where(x => x.CreatedRowDate <= r.ReceiptDateTo);

        return query;
    }
}

public class StoreDocumentStatsRequest
{
    public string?   Criterion       { get; set; }
    public DateTime? ReceiptDateFrom { get; set; }
    public DateTime? ReceiptDateTo   { get; set; }
    public long?     StoreId         { get; set; }
}

public record DocumentStatRow(string key, string label, int count, int detail);
