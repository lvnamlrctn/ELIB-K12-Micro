using System.Security.Claims;
using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/Acquisition/Report")]
[Authorize]
public class AcquisitionReportController(
    ELIBAPIDbContext db,
    ISystemParameterService sysParam,
    IElasticsearchService elastic,
    IConfiguration configuration) : BaseApiController
{
    private const int MaxExportRows = 50_000;

    /// <summary>Chuẩn hoá request dùng chung cho mọi báo cáo (port ELIB-LRC 10-03) và phân giải phạm vi đơn vị.
    /// Ngày "đến" không kèm giờ tính trọn ngày: giao diện gửi yyyy-MM-dd (0 giờ) nên trước đây mọi bản ghi có giờ trong
    /// chính ngày "đến" bị loại.</summary>
    private async Task<TenantScope> PrepareAsync(AcquisitionReportRequest r)
    {
        r.ToDate        = EndOfDay(r.ToDate);
        r.CreatedDateTo = EndOfDay(r.CreatedDateTo);
        return await TenantScopeHelper.ResolveScopeAsync(db, r.TenantId, GetTenantId(), IsPrivilegedRole());
    }

    private static DateTime? EndOfDay(DateTime? d) =>
        d is { } v && v.TimeOfDay == TimeSpan.Zero ? v.Date.AddDays(1).AddTicks(-1) : d;

    // Tenant: trước đây chỉ Sổ ĐKCB lọc đơn vị — danh sách bổ sung, phân bổ kho, thư mục sách mới và các bản in lấy
    // đơn nhận / ĐKCB / biểu ghi của MỌI đơn vị. Đơn nhận và ĐKCB là dữ liệu riêng của đơn vị (không có bản dùng chung).
    private IQueryable<ELIBAPI.Core.Entities.PrintBook.AbReceipt> Receipts(TenantScope scope) =>
        db.AbReceipts.Where(x => x.IsDelete != 2 && (scope.All || x.TenantId == scope.TenantId));

    private IQueryable<ELIBAPI.Core.Entities.PrintBook.Barcode> Barcodes(TenantScope scope) =>
        db.Barcodes.Where(x => x.IsDelete != 2 && (scope.All || x.TenantId == scope.TenantId));

    private IQueryable<ELIBAPI.Core.Entities.PrintBook.Bib> Bibs(TenantScope scope) =>
        db.Bibs.Where(x => x.IsDelete != 2 && (scope.All || x.TenantId == scope.TenantId));

    /// <summary>Mới biên mục trước; biểu ghi không có ngày xếp cuối (Postgres mặc định đưa NULL lên đầu khi DESC), tiêu chí
    /// phụ theo Id để các dòng trùng ngày không lặp/mất khi lật trang.</summary>
    private static IOrderedQueryable<ELIBAPI.Core.Entities.PrintBook.Bib> NewestFirst(IQueryable<ELIBAPI.Core.Entities.PrintBook.Bib> q) =>
        q.OrderBy(x => (x.CreatedTime ?? x.CreatedRowDate) == null)
         .ThenByDescending(x => x.CreatedTime ?? x.CreatedRowDate)
         .ThenByDescending(x => x.Bibid);

    // ── 1. Danh sách tài liệu bổ sung ────────────────────────────────────────

    [HttpPost("AcquisitionList")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> AcquisitionList([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        var q = db.AbReceiptDetails
            .Where(x => x.IsDelete != 2)
            .Join(Receipts(scope),
                d => d.Receipt_Id, ar => ar.Id,
                (d, ar) => new { Detail = d, Receipt = ar })
            .Where(x => (!r.FromDate.HasValue || x.Receipt.Receipt_Date >= r.FromDate)
                     && (!r.ToDate.HasValue   || x.Receipt.Receipt_Date <= r.ToDate)
                     && (!r.SupplierId.HasValue || x.Receipt.Supplier_Id == r.SupplierId)
                     && (!r.StoreId.HasValue || x.Receipt.Store_Id == r.StoreId));

        var total = await q.CountAsync();
        var page  = Math.Max(1, r.PageIndex ?? 1);
        var size  = Math.Clamp(r.PageSize ?? 20, 1, 500);

        var rows = await q.OrderByDescending(x => x.Receipt.Receipt_Date).ThenBy(x => x.Detail.Id)
            .Skip((Math.Max(page, 1) - 1) * size).Take(size)
            .Select(x => new
            {
                receiptCode = x.Receipt.Receipt_Name,
                receiptDate = x.Receipt.Receipt_Date,
                bibId       = x.Detail.Bibid,
                amount      = x.Detail.Amount,
                price       = x.Detail.Price,
                currency    = x.Detail.CURRENCY
            }).ToListAsync();

        // Enrich với title/author từ BibXml
        var bibIds  = rows.Where(r2 => r2.bibId.HasValue).Select(r2 => r2.bibId!.Value).Distinct().ToList();
        var xmlMap  = await db.BibXmls.Where(x => bibIds.Contains(x.BibId))
            .ToDictionaryAsync(x => x.BibId);

        var items = rows.Select(x => new
        {
            x.receiptCode, x.receiptDate, x.bibId, x.amount, x.price, x.currency,
            title     = x.bibId.HasValue && xmlMap.TryGetValue(x.bibId.Value, out var bx) ? bx.Title  : null,
            author    = x.bibId.HasValue && xmlMap.TryGetValue(x.bibId.Value, out var bx2) ? bx2.Author : null
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, totalCount = total }));
    }

    [HttpPost("AcquisitionListExport")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> AcquisitionListExport([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        r.PageIndex = 1; r.PageSize = 10000;
        var q = db.AbReceiptDetails.Where(x => x.IsDelete != 2)
            .Join(Receipts(scope),
                d => d.Receipt_Id, ar => ar.Id, (d, ar) => new { Detail = d, Receipt = ar })
            .Where(x => (!r.FromDate.HasValue || x.Receipt.Receipt_Date >= r.FromDate)
                     && (!r.ToDate.HasValue   || x.Receipt.Receipt_Date <= r.ToDate)
                     && (!r.SupplierId.HasValue || x.Receipt.Supplier_Id == r.SupplierId)
                     && (!r.StoreId.HasValue || x.Receipt.Store_Id == r.StoreId));

        var rows = await q.OrderByDescending(x => x.Receipt.Receipt_Date).ThenBy(x => x.Detail.Id)
            .Select(x => new { x.Receipt.Receipt_Name, x.Receipt.Receipt_Date, x.Detail.Bibid, x.Detail.Amount, x.Detail.Price, x.Detail.CURRENCY })
            .Take(MaxExportRows).ToListAsync();
        var bibIds = rows.Where(x => x.Bibid.HasValue).Select(x => x.Bibid!.Value).Distinct().ToList();
        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Danh sách bổ sung");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH BỔ SUNG TÀI LIỆU", 6);
        ws.Cell(startRow, 1).Value = "Mã phiếu"; ws.Cell(startRow, 2).Value = "Ngày nhập";
        ws.Cell(startRow, 3).Value = "Nhan đề";  ws.Cell(startRow, 4).Value = "Tác giả";
        ws.Cell(startRow, 5).Value = "Số lượng"; ws.Cell(startRow, 6).Value = "Đơn giá";
        int row = startRow + 1;
        foreach (var x in rows)
        {
            xmlMap.TryGetValue(x.Bibid ?? 0, out var bx);
            ws.Cell(row, 1).Value = x.Receipt_Name ?? "";
            ws.Cell(row, 2).Value = x.Receipt_Date?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(row, 3).Value = bx?.Title  ?? "";
            ws.Cell(row, 4).Value = bx?.Author ?? "";
            ws.Cell(row, 5).Value = x.Amount ?? 0;
            ws.Cell(row, 6).Value = x.Price  ?? 0;
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, 6);
        return ExcelFile(wb, "acquisition-list.xlsx");
    }

    // ── 2. Phân bổ kho ───────────────────────────────────────────────────────

    [HttpPost("StoreAllocation")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> StoreAllocation([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        var q = Barcodes(scope).Where(x => x.Store != null);
        if (r.StoreId.HasValue) q = q.Where(x => x.Store == r.StoreId);

        var grouped = await q
            .GroupBy(x => x.Store)
            .Select(g => new { StoreId = g.Key, Count = g.Count() })
            .ToListAsync();

        var storeIds = grouped.Where(x => x.StoreId.HasValue).Select(x => (long)x.StoreId!.Value).Distinct().ToList();
        var storeMap = await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        var items = grouped.Select(x => new
        {
            storeId   = x.StoreId,
            storeName = x.StoreId.HasValue && storeMap.TryGetValue((long)x.StoreId.Value, out var sn) ? sn : null,
            amount    = x.Count
        }).OrderBy(x => x.storeId).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, totalCount = items.Count }));
    }

    [HttpPost("StoreAllocationExport")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> StoreAllocationExport([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        var q = Barcodes(scope).Where(x => x.Store != null);
        if (r.StoreId.HasValue) q = q.Where(x => x.Store == r.StoreId);
        var grouped  = await q.GroupBy(x => x.Store).Select(g => new { StoreId = g.Key, Count = g.Count() }).ToListAsync();
        var storeIds = grouped.Where(x => x.StoreId.HasValue).Select(x => (long)x.StoreId!.Value).Distinct().ToList();
        var storeMap = await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Phân bổ kho");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "BÁO CÁO PHÂN BỔ KHO", 2);
        ws.Cell(startRow, 1).Value = "Kho"; ws.Cell(startRow, 2).Value = "Số bản";
        int row = startRow + 1;
        foreach (var x in grouped.OrderBy(x => x.StoreId))
        {
            storeMap.TryGetValue(x.StoreId.HasValue ? (long)x.StoreId.Value : 0, out var sn);
            ws.Cell(row, 1).Value = sn ?? x.StoreId?.ToString() ?? "";
            ws.Cell(row, 2).Value = x.Count;
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, 2);
        return ExcelFile(wb, "store-allocation.xlsx");
    }

    // ── 3. Sổ ĐKCB ───────────────────────────────────────────────────────────

    [HttpPost("AccessionRegister")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> AccessionRegister([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        // Sổ ĐKCB chỉ của đơn vị người xem (trước đây không lọc — lẫn ĐKCB mọi đơn vị).
        var q = Barcodes(scope);
        if (r.StoreId.HasValue) q = q.Where(x => x.Store == r.StoreId);

        var total = await q.CountAsync();
        var page  = Math.Max(1, r.PageIndex ?? 1);
        var size  = Math.Clamp(r.PageSize ?? 20, 1, 500);

        var rows = await q.OrderBy(x => x.BarcodeValue).ThenBy(x => x.Id)
            .Skip((Math.Max(page, 1) - 1) * size).Take(size)
            .Select(x => new { x.BarcodeValue, x.BibId, x.Store })
            .ToListAsync();

        var bibIds  = rows.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var storeIds = rows.Where(x => x.Store.HasValue).Select(x => (long)x.Store!.Value).Distinct().ToList();
        var xmlMap   = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);
        var storeMap = await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        var items = rows.Select(x =>
        {
            xmlMap.TryGetValue(x.BibId ?? 0, out var bx);
            storeMap.TryGetValue(x.Store.HasValue ? (long)x.Store.Value : 0, out var sn);
            return new { barcode = x.BarcodeValue, title = bx?.Title, storeName = sn };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, totalCount = total }));
    }

    [HttpPost("AccessionRegisterExport")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> AccessionRegisterExport([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        // Sổ ĐKCB chỉ của đơn vị người xem (trước đây không lọc — lẫn ĐKCB mọi đơn vị).
        var q = Barcodes(scope);
        if (r.StoreId.HasValue) q = q.Where(x => x.Store == r.StoreId);
        var rows     = await q.OrderBy(x => x.BarcodeValue).ThenBy(x => x.Id).Select(x => new { x.BarcodeValue, x.BibId, x.Store }).Take(MaxExportRows).ToListAsync();
        var bibIds   = rows.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var storeIds = rows.Where(x => x.Store.HasValue).Select(x => (long)x.Store!.Value).Distinct().ToList();
        var xmlMap   = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);
        var storeMap = await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Sổ ĐKCB");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "SỔ ĐĂNG KÝ CÁ BIỆT", 3);
        ws.Cell(startRow, 1).Value = "Số KCB"; ws.Cell(startRow, 2).Value = "Nhan đề"; ws.Cell(startRow, 3).Value = "Kho";
        int row = startRow + 1;
        foreach (var x in rows)
        {
            xmlMap.TryGetValue(x.BibId ?? 0, out var bx);
            storeMap.TryGetValue(x.Store.HasValue ? (long)x.Store.Value : 0, out var sn);
            ws.Cell(row, 1).Value = x.BarcodeValue ?? "";
            ws.Cell(row, 2).Value = bx?.Title ?? "";
            ws.Cell(row, 3).Value = sn ?? "";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, 3);
        return ExcelFile(wb, "accession-register.xlsx");
    }

    // ── 4. Thư mục sách mới ───────────────────────────────────────────────────

    [HttpPost("NewBookCatalog")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> NewBookCatalog([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        var q = Bibs(scope);
        if (r.FromDate.HasValue) q = q.Where(x => (x.CreatedTime ?? x.CreatedRowDate) >= r.FromDate);
        if (r.ToDate.HasValue)   q = q.Where(x => (x.CreatedTime ?? x.CreatedRowDate) <= r.ToDate);

        var total = await q.CountAsync();
        var page  = Math.Max(1, r.PageIndex ?? 1);
        var size  = Math.Clamp(r.PageSize ?? 20, 1, 500);

        var bibs   = await NewestFirst(q)
            .Skip((Math.Max(page, 1) - 1) * size).Take(size).ToListAsync();
        var bibIds = bibs.Select(x => x.Bibid).Distinct().ToList();
        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);

        var items = bibs.Select(b =>
        {
            xmlMap.TryGetValue(b.Bibid, out var bx);
            return new { bibId = b.Bibid, title = bx?.Title, author = bx?.Author, publisher = bx?.Publisher, publishDate = bx?.PublishDate };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, totalCount = total }));
    }

    [HttpPost("NewBookCatalogExport")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> NewBookCatalogExport([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        var q = Bibs(scope);
        if (r.FromDate.HasValue) q = q.Where(x => (x.CreatedTime ?? x.CreatedRowDate) >= r.FromDate);
        if (r.ToDate.HasValue)   q = q.Where(x => (x.CreatedTime ?? x.CreatedRowDate) <= r.ToDate);
        var bibs   = await NewestFirst(q).Take(MaxExportRows).ToListAsync();
        var bibIds = bibs.Select(x => x.Bibid).Distinct().ToList();
        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Thư mục sách mới");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH MỤC SÁCH MỚI", 4);
        ws.Cell(startRow, 1).Value = "Nhan đề"; ws.Cell(startRow, 2).Value = "Tác giả";
        ws.Cell(startRow, 3).Value = "NXB";     ws.Cell(startRow, 4).Value = "Năm XB";
        int row = startRow + 1;
        foreach (var b in bibs)
        {
            xmlMap.TryGetValue(b.Bibid, out var bx);
            ws.Cell(row, 1).Value = bx?.Title       ?? "";
            ws.Cell(row, 2).Value = bx?.Author      ?? "";
            ws.Cell(row, 3).Value = bx?.Publisher   ?? "";
            ws.Cell(row, 4).Value = bx?.PublishDate ?? "";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, 4);
        return ExcelFile(wb, "new-book-catalog.xlsx");
    }

    // ── 5. In "Danh mục sách bổ sung" theo mẫu vật lý ─────────────────────────

    // Header dùng chung cho các báo cáo in (thư viện + phòng ban) — đọc từ dbo.SystemParameter
    [HttpGet("LibraryHeader")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> LibraryHeader()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            parentLibrary = await sysParam.GetValueAsync("ParentLibrary") ?? "",
            libraryName   = await sysParam.GetValueAsync("LibraryName") ?? "",
            deptName      = await sysParam.GetValueAsync("LibraryName") ?? "",
        }));
    }

    [HttpPost("AcquisitionListPrint")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> AcquisitionListPrint([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        var matchedBibIds = await GetMatchingBibIdsAsync(r);
        var q = db.AbReceiptDetails.Where(x => x.IsDelete != 2)
            .Join(Receipts(scope),
                d => d.Receipt_Id, ar => ar.Id, (d, ar) => new { Detail = d, Receipt = ar })
            .Where(x => (!r.FromDate.HasValue || x.Receipt.Receipt_Date >= r.FromDate)
                     && (!r.ToDate.HasValue   || x.Receipt.Receipt_Date <= r.ToDate)
                     && (!r.SupplierId.HasValue || x.Receipt.Supplier_Id == r.SupplierId)
                     && (!r.ReceiptCodeFrom.HasValue || x.Receipt.Code >= r.ReceiptCodeFrom)
                     && (!r.ReceiptCodeTo.HasValue   || x.Receipt.Code <= r.ReceiptCodeTo)
                     && (!r.StoreId.HasValue || x.Receipt.Store_Id == r.StoreId)
                     && (string.IsNullOrEmpty(r.ReceiptName) || x.Receipt.Receipt_Name!.Contains(r.ReceiptName))
                     && (!r.Status.HasValue   || x.Receipt.Status == r.Status)
                     && (!r.SourceId.HasValue || x.Receipt.Source_Id == r.SourceId)
                     && (!r.FundId.HasValue   || x.Receipt.FundId == r.FundId)
                     && (!r.CreatedBy.HasValue || x.Receipt.CreatedRowBy == r.CreatedBy)
                     && (!r.CreatedDateFrom.HasValue || x.Receipt.CreatedDate >= r.CreatedDateFrom)
                     && (!r.CreatedDateTo.HasValue   || x.Receipt.CreatedDate <= r.CreatedDateTo)
                     && (matchedBibIds == null || (x.Detail.Bibid.HasValue && matchedBibIds.Contains(x.Detail.Bibid.Value))));

        var rows = await q.OrderBy(x => x.Receipt.Receipt_Date)
            .Select(x => new { x.Receipt.Code, x.Receipt.Receipt_Date, x.Detail.Bibid, x.Detail.Amount, x.Detail.Price })
            .Take(5000).ToListAsync();

        var bibIds = rows.Where(x => x.Bibid.HasValue).Select(x => x.Bibid!.Value).Distinct().ToList();
        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);

        var items = rows.Select((x, i) =>
        {
            xmlMap.TryGetValue(x.Bibid ?? 0, out var bx);
            var amount = x.Amount ?? 0; var price = x.Price ?? 0;
            return new { stt = i + 1, title = bx?.Title, author = bx?.Author, price, amount, total = price * amount };
        }).ToList();

        var distinctCodes = rows.Select(x => x.Code).Distinct().ToList();
        var distinctDates = rows.Select(x => x.Receipt_Date).Distinct().ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            parentLibrary = await sysParam.GetValueAsync("ParentLibrary") ?? "",
            libraryName   = await sysParam.GetValueAsync("LibraryName") ?? "",
            deptName      = await sysParam.GetValueAsync("LibraryName") ?? "",
            receiptCode   = distinctCodes.Count == 1 ? distinctCodes[0] : (long?)null,
            receiptDate   = distinctDates.Count == 1 ? distinctDates[0] : (DateTime?)null,
            items,
            totalCount = items.Count,
            totalMoney = items.Sum(x => x.total),
        }));
    }

    // ── 6. In "Báo cáo phân bổ kho" theo mẫu vật lý ───────────────────────────

    [HttpPost("StoreAllocationPrint")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> StoreAllocationPrint([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        var matchedBibIds = await GetMatchingBibIdsAsync(r);
        var q = Barcodes(scope).Where(x => x.Receipt_Id != null)
            .Join(Receipts(scope), b => b.Receipt_Id, ar => ar.Id, (b, ar) => new { B = b, Receipt = ar })
            .Where(x => (!r.ReceiptCodeFrom.HasValue || x.Receipt.Code >= r.ReceiptCodeFrom)
                     && (!r.ReceiptCodeTo.HasValue   || x.Receipt.Code <= r.ReceiptCodeTo)
                     && (!r.StoreId.HasValue || x.B.Store == r.StoreId)
                     && (!r.FromDate.HasValue || x.Receipt.Receipt_Date >= r.FromDate)
                     && (!r.ToDate.HasValue   || x.Receipt.Receipt_Date <= r.ToDate)
                     && (!r.SupplierId.HasValue || x.Receipt.Supplier_Id == r.SupplierId)
                     && (string.IsNullOrEmpty(r.ReceiptName) || x.Receipt.Receipt_Name!.Contains(r.ReceiptName))
                     && (!r.Status.HasValue   || x.Receipt.Status == r.Status)
                     && (!r.SourceId.HasValue || x.Receipt.Source_Id == r.SourceId)
                     && (!r.FundId.HasValue   || x.Receipt.FundId == r.FundId)
                     && (!r.CreatedBy.HasValue || x.Receipt.CreatedRowBy == r.CreatedBy)
                     && (!r.CreatedDateFrom.HasValue || x.Receipt.CreatedDate >= r.CreatedDateFrom)
                     && (!r.CreatedDateTo.HasValue   || x.Receipt.CreatedDate <= r.CreatedDateTo)
                     && (matchedBibIds == null || (x.B.BibId.HasValue && matchedBibIds.Contains(x.B.BibId.Value))));

        var rows = await q.OrderBy(x => x.B.Id)
            .Select(x => new { x.B.Id, x.B.BarcodeValue, x.B.Store, x.B.BibId, x.Receipt.Code, x.Receipt.Receipt_Date })
            .Take(20000).ToListAsync();

        var bibIds   = rows.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var storeIds = rows.Where(x => x.Store.HasValue).Select(x => (long)x.Store!.Value).Distinct().ToList();
        var xmlMap   = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);
        var storeMap = await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        var marc082 = await db.BibDatas
            .Where(d => d.IsDelete != 2 && d.Field == "082" && d.BibId.HasValue && bibIds.Contains(d.BibId.Value))
            .Select(d => new { BibId = d.BibId!.Value, d.SubField, d.Data })
            .ToListAsync();
        var ddcMap    = marc082.Where(d => d.SubField == "a").GroupBy(d => d.BibId).ToDictionary(g => g.Key, g => g.First().Data);
        var cutterMap = marc082.Where(d => d.SubField == "b").GroupBy(d => d.BibId).ToDictionary(g => g.Key, g => g.First().Data);

        int stt = 0;
        var groups = rows.GroupBy(x => x.Store).OrderBy(g => g.Key).Select(storeGroup =>
        {
            var items = storeGroup.GroupBy(x => x.BibId).Select(bibGroup =>
            {
                xmlMap.TryGetValue(bibGroup.Key ?? 0, out var bx);
                ddcMap.TryGetValue(bibGroup.Key ?? 0, out var ddc);
                cutterMap.TryGetValue(bibGroup.Key ?? 0, out var cutter);
                var barcodes = bibGroup.Select(x => x.BarcodeValue ?? "").ToList();
                return new
                {
                    stt = ++stt,
                    title = bx?.Title, author = bx?.Author, publisher = bx?.Publisher,
                    mlCutter = string.IsNullOrEmpty(ddc) ? "" : (ddc + (string.IsNullOrEmpty(cutter) ? "" : "/" + cutter)),
                    sl = barcodes.Count,
                    barcodes,
                };
            }).ToList();
            storeMap.TryGetValue(storeGroup.Key.HasValue ? (long)storeGroup.Key.Value : 0, out var storeName);
            return new { storeId = storeGroup.Key, storeName, items };
        }).ToList();

        var distinctCodes = rows.Select(x => x.Code).Distinct().ToList();
        var distinctDates = rows.Select(x => x.Receipt_Date).Distinct().ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            parentLibrary = await sysParam.GetValueAsync("ParentLibrary") ?? "",
            deptName      = await sysParam.GetValueAsync("LibraryName") ?? "",
            receiptCode   = distinctCodes.Count == 1 ? distinctCodes[0] : (long?)null,
            receiptDate   = distinctDates.Count == 1 ? distinctDates[0] : (DateTime?)null,
            groups,
            totalCount = groups.Sum(g => g.items.Sum(i => i.sl)),
        }));
    }

    // ── 7. In "Sổ đăng ký cá biệt" theo mẫu vật lý ────────────────────────────

    [HttpPost("AccessionRegisterPrint")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> AccessionRegisterPrint([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        // Chỉ mặc định lọc theo NĂM HIỆN TẠI khi không có bộ lọc nào khác — tránh trường hợp
        // người dùng lọc theo "Số đơn nhận"/"Tác giả"/... nhưng bị mốc năm ẩn loại bỏ mất kết quả
        // (bản ghi thuộc năm khác vẫn hợp lệ theo các bộ lọc đó).
        var hasOtherFilter = r.ReceiptCodeFrom.HasValue || r.ReceiptCodeTo.HasValue || r.StoreId.HasValue || r.SupplierId.HasValue
            || !string.IsNullOrEmpty(r.ReceiptName) || r.Status.HasValue || r.SourceId.HasValue || r.FundId.HasValue || r.CreatedBy.HasValue
            || r.CreatedDateFrom.HasValue || r.CreatedDateTo.HasValue || r.MfnFrom.HasValue || r.MfnTo.HasValue
            || !string.IsNullOrEmpty(r.Title) || !string.IsNullOrEmpty(r.Author) || !string.IsNullOrEmpty(r.Publisher) || !string.IsNullOrEmpty(r.PublishYear);

        DateTime? fromDate = r.FromDate;
        DateTime? toDate   = r.ToDate;
        if (!fromDate.HasValue && !toDate.HasValue && !hasOtherFilter)
        {
            fromDate = new DateTime(DateTime.Now.Year, 1, 1);
            toDate   = new DateTime(DateTime.Now.Year, 12, 31, 23, 59, 59);
        }

        var matchedBibIds = await GetMatchingBibIdsAsync(r);
        var q = Barcodes(scope).Where(x => x.Receipt_Id != null)
            .Join(Receipts(scope), b => b.Receipt_Id, ar => ar.Id, (b, ar) => new { B = b, Receipt = ar })
            .Where(x => (!fromDate.HasValue || x.Receipt.Receipt_Date >= fromDate)
                     && (!toDate.HasValue   || x.Receipt.Receipt_Date <= toDate)
                     && (!r.StoreId.HasValue || x.B.Store == r.StoreId)
                     && (!r.ReceiptCodeFrom.HasValue || x.Receipt.Code >= r.ReceiptCodeFrom)
                     && (!r.ReceiptCodeTo.HasValue   || x.Receipt.Code <= r.ReceiptCodeTo)
                     && (!r.SupplierId.HasValue || x.Receipt.Supplier_Id == r.SupplierId)
                     && (string.IsNullOrEmpty(r.ReceiptName) || x.Receipt.Receipt_Name!.Contains(r.ReceiptName))
                     && (!r.Status.HasValue   || x.Receipt.Status == r.Status)
                     && (!r.SourceId.HasValue || x.Receipt.Source_Id == r.SourceId)
                     && (!r.FundId.HasValue   || x.Receipt.FundId == r.FundId)
                     && (!r.CreatedBy.HasValue || x.Receipt.CreatedRowBy == r.CreatedBy)
                     && (!r.CreatedDateFrom.HasValue || x.Receipt.CreatedDate >= r.CreatedDateFrom)
                     && (!r.CreatedDateTo.HasValue   || x.Receipt.CreatedDate <= r.CreatedDateTo)
                     && (matchedBibIds == null || (x.B.BibId.HasValue && matchedBibIds.Contains(x.B.BibId.Value))));

        var rows = await q.OrderBy(x => x.B.BarcodeNumber ?? 0).ThenBy(x => x.B.Id)
            .Select(x => new { x.B.BarcodeValue, x.B.BibId, x.Receipt.Receipt_Date, x.B.Receipt_Id })
            .Take(20000).ToListAsync();

        var bibIds = rows.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);

        var marc082 = await db.BibDatas
            .Where(d => d.IsDelete != 2 && d.Field == "082" && d.SubField == "a" && d.BibId.HasValue && bibIds.Contains(d.BibId.Value))
            .Select(d => new { BibId = d.BibId!.Value, d.Data })
            .ToListAsync();
        var ddcMap = marc082.GroupBy(d => d.BibId).ToDictionary(g => g.Key, g => g.First().Data);

        var receiptIds = rows.Where(x => x.Receipt_Id.HasValue).Select(x => x.Receipt_Id!.Value).Distinct().ToList();
        var priceLookup = (await db.AbReceiptDetails
            .Where(d => d.IsDelete != 2 && d.Receipt_Id.HasValue && receiptIds.Contains(d.Receipt_Id.Value) && d.Bibid.HasValue)
            .Select(d => new { d.Receipt_Id, d.Bibid, d.Price })
            .ToListAsync())
            .GroupBy(x => (x.Receipt_Id, x.Bibid))
            .ToDictionary(g => g.Key, g => g.First().Price);

        var items = rows.Select(x =>
        {
            xmlMap.TryGetValue(x.BibId ?? 0, out var bx);
            ddcMap.TryGetValue(x.BibId ?? 0, out var ddc);
            priceLookup.TryGetValue((x.Receipt_Id, x.BibId), out var price);
            return new
            {
                date = x.Receipt_Date, barcode = x.BarcodeValue, bibId = x.BibId,
                title = bx?.Title, author = bx?.Author,
                publisher = bx?.Publisher, publishDate = bx?.PublishDate,
                price, mlDigit = string.IsNullOrEmpty(ddc) ? "" : ddc.Substring(0, 1),
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            year = fromDate.HasValue && toDate.HasValue && fromDate.Value.Year == toDate.Value.Year
                ? fromDate.Value.Year : DateTime.Now.Year,
            items,
            totalCount = items.Count,
        }));
    }

    // ── 8. In "Nhãn môn loại + số ĐKCB" (nhãn có mã vạch thật, theo từng bản sao) ─────

    [HttpPost("ClassLabelAccessionPrint")]
    [Permission("ACQUISITION_REPORT", "view")]
    public async Task<IActionResult> ClassLabelAccessionPrint([FromBody] AcquisitionReportRequest r)
    {
        var scope = await PrepareAsync(r);
        var matchedBibIds = await GetMatchingBibIdsAsync(r);
        var q = Barcodes(scope).Where(x => x.Receipt_Id != null)
            .Join(Receipts(scope), b => b.Receipt_Id, ar => ar.Id, (b, ar) => new { B = b, Receipt = ar })
            .Where(x => (!r.FromDate.HasValue || x.Receipt.Receipt_Date >= r.FromDate)
                     && (!r.ToDate.HasValue   || x.Receipt.Receipt_Date <= r.ToDate)
                     && (!r.StoreId.HasValue || x.B.Store == r.StoreId)
                     && (!r.ReceiptCodeFrom.HasValue || x.Receipt.Code >= r.ReceiptCodeFrom)
                     && (!r.ReceiptCodeTo.HasValue   || x.Receipt.Code <= r.ReceiptCodeTo)
                     && (!r.SupplierId.HasValue || x.Receipt.Supplier_Id == r.SupplierId)
                     && (string.IsNullOrEmpty(r.ReceiptName) || x.Receipt.Receipt_Name!.Contains(r.ReceiptName))
                     && (!r.Status.HasValue   || x.Receipt.Status == r.Status)
                     && (!r.SourceId.HasValue || x.Receipt.Source_Id == r.SourceId)
                     && (!r.FundId.HasValue   || x.Receipt.FundId == r.FundId)
                     && (!r.CreatedBy.HasValue || x.Receipt.CreatedRowBy == r.CreatedBy)
                     && (!r.CreatedDateFrom.HasValue || x.Receipt.CreatedDate >= r.CreatedDateFrom)
                     && (!r.CreatedDateTo.HasValue   || x.Receipt.CreatedDate <= r.CreatedDateTo)
                     && (matchedBibIds == null || (x.B.BibId.HasValue && matchedBibIds.Contains(x.B.BibId.Value))));

        var rows = await q.OrderBy(x => x.B.BarcodeNumber ?? 0).ThenBy(x => x.B.Id)
            .Select(x => new { x.B.BarcodeValue, x.B.BibId })
            .Take(20000).ToListAsync();

        var bibIds = rows.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var marc082 = await db.BibDatas
            .Where(d => d.IsDelete != 2 && d.Field == "082" && d.BibId.HasValue && bibIds.Contains(d.BibId.Value))
            .Select(d => new { BibId = d.BibId!.Value, d.SubField, d.Data })
            .ToListAsync();
        var ddcMap    = marc082.Where(d => d.SubField == "a").GroupBy(d => d.BibId).ToDictionary(g => g.Key, g => g.First().Data);
        var cutterMap = marc082.Where(d => d.SubField == "b").GroupBy(d => d.BibId).ToDictionary(g => g.Key, g => g.First().Data);

        var items = rows.Select(x =>
        {
            ddcMap.TryGetValue(x.BibId ?? 0, out var ddc);
            cutterMap.TryGetValue(x.BibId ?? 0, out var cutter);
            return new { barcode = x.BarcodeValue, classSymbol = ddc, authorMark = cutter };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items, totalCount = items.Count }));
    }

    // Lọc theo Bib (Mfn/Title/Author/Publisher/PublishYear) — trả về null nếu không có bộ lọc nào
    private Task<HashSet<long>?> GetMatchingBibIdsAsync(AcquisitionReportRequest r) =>
        BibFilterHelper.GetMatchingBibIdsAsync(db, r.MfnFrom, r.MfnTo, r.Title, r.Author, r.Publisher, r.PublishYear,
            elastic, configuration);

    // ── Helper ────────────────────────────────────────────────────────────────

    private static FileContentResult ExcelFile(XLWorkbook wb, string fileName)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new FileContentResult(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        { FileDownloadName = fileName };
    }
}

public class AcquisitionReportRequest
{
    /// <summary>Đơn vị do tài khoản đặc quyền chọn (PublicId); user thường bị ép theo JWT.</summary>
    public Guid?     TenantId       { get; set; }
    public DateTime? FromDate       { get; set; }
    public DateTime? ToDate         { get; set; }
    public int?      StoreId        { get; set; }
    public long?     SupplierId     { get; set; }
    public long?     ReceiptCodeFrom { get; set; }
    public long?     ReceiptCodeTo   { get; set; }
    public int?      PageIndex      { get; set; }
    public int?      PageSize       { get; set; }
    public string?   ReceiptName     { get; set; }
    public long?     MfnFrom         { get; set; }
    public long?     MfnTo           { get; set; }
    public int?      Status          { get; set; }
    public long?     SourceId        { get; set; }
    public long?     FundId          { get; set; }
    public long?     CreatedBy       { get; set; }
    public DateTime? CreatedDateFrom { get; set; }
    public DateTime? CreatedDateTo   { get; set; }
    public string?   Title           { get; set; }
    public string?   Author          { get; set; }
    public string?   Publisher       { get; set; }
    public string?   PublishYear     { get; set; }
}
