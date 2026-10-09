using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Report/DocumentUnit")]
public class DocumentUnitReportController(ELIBAPIDbContext db, ISystemParameterService sysParam) : BaseApiController
{
    [HttpPost("Summary")]
    [Permission("DOC_UNIT_REPORT", "view")]
    public async Task<IActionResult> Summary([FromBody] DocumentUnitReportRequest r)
    {
        var rows = await BuildRows(r);
        return Ok(ApiResponse<object>.Ok(new { items = rows, totalCount = rows.Count }));
    }

    [HttpPost("SummaryExport")]
    [Permission("DOC_UNIT_REPORT", "view")]
    public async Task<IActionResult> SummaryExport([FromBody] DocumentUnitReportRequest r)
    {
        var rows = await BuildRows(r);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Báo cáo tài liệu theo đơn vị");

        int lastColumn = 1;
        if (r.ReportType != "digital") lastColumn += 2;
        if (r.ReportType != "print")   lastColumn += 1;
        if (r.ReportType != "print" && r.ReportType != "digital") lastColumn += 1;

        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "BÁO CÁO TÀI LIỆU THEO ĐƠN VỊ", lastColumn);

        int col = 1;
        ws.Cell(startRow, col++).Value = "Đơn vị";
        if (r.ReportType != "digital") { ws.Cell(startRow, col++).Value = "Số đầu tài liệu in"; ws.Cell(startRow, col++).Value = "Số bản in"; }
        if (r.ReportType != "print")   { ws.Cell(startRow, col++).Value = "Số tài liệu số"; }
        if (r.ReportType != "print" && r.ReportType != "digital") { ws.Cell(startRow, col++).Value = "Tổng cộng"; }

        int row = startRow + 1;
        foreach (var x in rows)
        {
            col = 1;
            ws.Cell(row, col++).Value = x.tenantName;
            if (r.ReportType != "digital") { ws.Cell(row, col++).Value = x.bibCount; ws.Cell(row, col++).Value = x.barcodeCount; }
            if (r.ReportType != "print")   { ws.Cell(row, col++).Value = x.ebookCount; }
            if (r.ReportType != "print" && r.ReportType != "digital") { ws.Cell(row, col++).Value = x.bibCount + x.ebookCount; }
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, lastColumn);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "document-unit-report.xlsx");
    }

    private async Task<List<DocumentUnitReportRow>> BuildRows(DocumentUnitReportRequest r)
    {
        var jwtTenantId = GetTenantId();

        var bibQuery     = db.Bibs.Where(x => x.IsDelete != 2);
        var barcodeQuery = db.Barcodes.Where(x => x.IsDelete != 2);
        var ebookQuery   = db.EbookItems.Where(x => x.IsDelete != 2);

        if (jwtTenantId.HasValue)
        {
            bibQuery = bibQuery.Where(x => x.TenantId == jwtTenantId);
            barcodeQuery = barcodeQuery.Where(x => x.TenantId == jwtTenantId);
            ebookQuery = ebookQuery.Where(x => x.TenantId == jwtTenantId);
        }
        else if (r.TenantId.HasValue)
        {
            bibQuery = bibQuery.Where(x => x.TenantId == r.TenantId);
            barcodeQuery = barcodeQuery.Where(x => x.TenantId == r.TenantId);
            ebookQuery = ebookQuery.Where(x => x.TenantId == r.TenantId);
        }

        if (r.FromDate.HasValue)
        {
            bibQuery = bibQuery.Where(x => x.CreatedRowDate >= r.FromDate);
            barcodeQuery = barcodeQuery.Where(x => x.CreatedRowDate >= r.FromDate);
            ebookQuery = ebookQuery.Where(x => x.Submited >= r.FromDate);
        }
        if (r.ToDate.HasValue)
        {
            bibQuery = bibQuery.Where(x => x.CreatedRowDate <= r.ToDate);
            barcodeQuery = barcodeQuery.Where(x => x.CreatedRowDate <= r.ToDate);
            ebookQuery = ebookQuery.Where(x => x.Submited <= r.ToDate);
        }

        // Chỉ truy vấn bảng cần thiết cho loại báo cáo đang xem — tránh quét PrintBook.Barcode (bảng lớn nhất) khi không cần
        var needPrint  = r.ReportType != "digital";
        var needDigital = r.ReportType != "print";

        var bibCounts     = needPrint ? await bibQuery.GroupBy(x => x.TenantId).Select(g => new { TenantId = g.Key, Count = g.Count() }).ToListAsync() : [];
        var barcodeCounts = needPrint ? await barcodeQuery.GroupBy(x => x.TenantId).Select(g => new { TenantId = g.Key, Count = g.Count() }).ToListAsync() : [];
        var ebookCounts   = needDigital ? await ebookQuery.GroupBy(x => x.TenantId).Select(g => new { TenantId = g.Key, Count = g.Count() }).ToListAsync() : [];

        // Danh sách đơn vị lấy từ chính bảng Tenant (không suy ra từ kết quả đếm) — để đơn vị có 0 tài liệu vẫn hiện đúng dòng "0"
        var tenantsQuery = db.Tenants.Where(x => x.IsDelete != 2);
        if (jwtTenantId.HasValue) tenantsQuery = tenantsQuery.Where(x => x.Id == jwtTenantId);
        else if (r.TenantId.HasValue) tenantsQuery = tenantsQuery.Where(x => x.Id == r.TenantId);
        var tenantNames = await tenantsQuery.ToDictionaryAsync(x => x.Id, x => x.Name);

        return tenantNames.Select(kv => new DocumentUnitReportRow(
            kv.Key,
            kv.Value ?? "Không xác định",
            bibCounts.FirstOrDefault(x => x.TenantId == kv.Key)?.Count ?? 0,
            barcodeCounts.FirstOrDefault(x => x.TenantId == kv.Key)?.Count ?? 0,
            ebookCounts.FirstOrDefault(x => x.TenantId == kv.Key)?.Count ?? 0
        )).OrderByDescending(x => x.bibCount + x.ebookCount).ToList();
    }
}

public record DocumentUnitReportRow(long tenantId, string tenantName, int bibCount, int barcodeCount, int ebookCount);

public class DocumentUnitReportRequest
{
    public long?     TenantId   { get; set; }
    public DateTime? FromDate   { get; set; }
    public DateTime? ToDate     { get; set; }
    public string?   ReportType { get; set; } // "summary" | "print" | "digital" — chỉ ảnh hưởng cột xuất Excel
}
