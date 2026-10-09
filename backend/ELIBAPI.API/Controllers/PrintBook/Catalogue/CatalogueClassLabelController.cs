using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Catalogue/ClassLabel")]
[Authorize]
public class CatalogueClassLabelController(ELIBAPIDbContext db, ISystemParameterService sysParam) : BaseApiController
{
    [HttpPost("Search")]
    [Permission("CATALOG_BIBS", "view")]
    public async Task<IActionResult> Search([FromBody] ClassLabelSearchRequest r)
    {
        // Trước đây không lọc đơn vị gì cả — trả ĐKCB của mọi đơn vị (và khoảng mã giờ có thể trùng giữa các đơn vị).
        var tenantId = GetTenantId();
        var query = db.Barcodes.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));

        if (!string.IsNullOrEmpty(r.ReceiptCode))
        {
            var receiptIds = db.AbReceipts
                .Where(x => x.Receipt_Name != null && x.Receipt_Name.Contains(r.ReceiptCode))
                .Select(x => (long?)x.Id);
            query = query.Where(x => receiptIds.Contains(x.Receipt_Id));
        }
        if (!string.IsNullOrEmpty(r.BarcodeFrom))
            query = query.Where(x => string.Compare(x.BarcodeValue, r.BarcodeFrom) >= 0);
        if (!string.IsNullOrEmpty(r.BarcodeTo))
            query = query.Where(x => string.Compare(x.BarcodeValue, r.BarcodeTo) <= 0);

        var rows = await query
            .OrderBy(x => x.BarcodeValue)
            .Select(x => new { x.Id, x.BarcodeValue, x.BibId })
            .ToListAsync();

        var bibIds = rows.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var xmlMap = await db.BibXmls
            .Where(x => bibIds.Contains(x.BibId))
            .ToDictionaryAsync(x => x.BibId);

        var items = rows.Select(x =>
        {
            xmlMap.TryGetValue(x.BibId ?? 0, out var bx);
            return new
            {
                x.Id,
                barcode     = x.BarcodeValue,
                title       = bx?.Title,
                classSymbol = bx?.DDC,
                authorMark  = (string?)null   // Cutter mark chưa có trong schema
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpPost("SearchByBib")]
    [Permission("CATALOG_BIBS", "view")]
    public async Task<IActionResult> SearchByBib([FromBody] ClassLabelByBibRequest r)
    {
        if (r.BibPublicIds == null || r.BibPublicIds.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { parentLibrary = "", libraryName = "", items = new List<object>() }));

        var bibs = await db.Bibs.Where(b => b.IsDelete != 2 && r.BibPublicIds.Contains(b.PublicId))
            .Select(b => new { b.Bibid, b.PublicId }).ToListAsync();
        var bibIds = bibs.Select(b => b.Bibid).ToList();

        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);

        var marc082 = await db.BibDatas
            .Where(d => d.IsDelete != 2 && d.Field == "082" && d.BibId.HasValue && bibIds.Contains(d.BibId.Value))
            .Select(d => new { BibId = d.BibId!.Value, d.SubField, d.Data })
            .ToListAsync();
        var ddcMap    = marc082.Where(d => d.SubField == "a").GroupBy(d => d.BibId).ToDictionary(g => g.Key, g => g.First().Data);
        var cutterMap = marc082.Where(d => d.SubField == "b").GroupBy(d => d.BibId).ToDictionary(g => g.Key, g => g.First().Data);

        var items = bibs.Select(b =>
        {
            xmlMap.TryGetValue(b.Bibid, out var xml);
            ddcMap.TryGetValue(b.Bibid, out var ddc);
            cutterMap.TryGetValue(b.Bibid, out var cutter);
            return new
            {
                id          = b.Bibid,
                bibPublicId = b.PublicId,
                title       = xml?.Title,
                author      = xml?.Author,
                classSymbol = ddc,
                authorMark  = cutter,
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            parentLibrary = await sysParam.GetValueAsync("ParentLibrary") ?? "",
            libraryName   = await sysParam.GetValueAsync("LibraryName") ?? "",
            items,
        }));
    }
}

public class ClassLabelSearchRequest
{
    public string? ReceiptCode { get; set; }
    public string? BarcodeFrom { get; set; }
    public string? BarcodeTo   { get; set; }
}

public class ClassLabelByBibRequest
{
    public List<Guid> BibPublicIds { get; set; } = new();
}
