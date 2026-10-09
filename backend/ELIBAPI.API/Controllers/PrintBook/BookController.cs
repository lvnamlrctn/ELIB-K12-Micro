using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

// Module "Tìm kiếm tài liệu in ấn": Search/ExportDetail = tìm theo bản sao vật lý (item-level, ảnh 1),
// SearchTitles/ExportSummary = tìm theo đầu sách gộp số lượng (title-level, ảnh 2). Không có Add/Update/Delete
// nên không kế thừa GenericController — đây là 2 view tổng hợp trên Bib/Barcode/BibXml đã có sẵn.
[Route("api/PrintBook/Book")]
public class BookController(
    ELIBAPIDbContext db,
    ISystemParameterService sysParam,
    IElasticsearchService elastic,
    IConfiguration configuration) : BaseApiController
{
    private sealed class ItemRow
    {
        public long?   Mfn         { get; set; }
        public string? DonNhan     { get; set; }
        public string? Dkcb        { get; set; }
        public string? Title       { get; set; }
        public string? Author      { get; set; }
        public string? Publisher   { get; set; }
        public string? PublishYear { get; set; }
        public int?    StoreId     { get; set; }
        public string? StoreName   { get; set; }
        public long?   DocTypeId   { get; set; }
        public string? Status      { get; set; }
        public string? StatusName  { get; set; }
        public Guid    PublicId    { get; set; }
        public string? TenantName  { get; set; }
    }

    private sealed class TitleRow
    {
        public long?   Mfn         { get; set; }
        public string? Title       { get; set; }
        public string? Author      { get; set; }
        public string? Publisher   { get; set; }
        public string? PublishYear { get; set; }
        public int     Quantity    { get; set; }
        public long?   DocTypeId   { get; set; }
        public string? Status      { get; set; }
        public string? StatusName  { get; set; }
        public Guid    PublicId    { get; set; }
        public string? TenantName  { get; set; }
    }

    // Đợt 24.4 — gộp jwtTenantId (ép cho user thường)/requestTenantId (đơn vị user đặc quyền chủ động
    // chọn)/isPrivileged (jwtTenantId==null hoặc RoleCode trong ReadOnlyPolicy) 1 chỗ, mirror
    // BaseRepository.ApplyTenantFilter — truyền xuyên suốt Search/SearchTitles/ExportDetail/ExportSummary
    // vì các hàm dựng query này dùng chung cho cả 4 action.
    private readonly record struct TenantScope(long? JwtTenantId, long? RequestTenantId, bool IsPrivileged);

    private async Task<TenantScope> ResolveTenantScopeAsync(BookDocSearchRequest r)
    {
        var jwtTenantId  = GetTenantId();
        var isPrivileged = IsPrivilegedRole();
        var requestTenantId = await TenantScopeHelper.ResolveRequestTenantIdAsync(db, r.TenantId, jwtTenantId, isPrivileged);
        return new TenantScope(jwtTenantId, requestTenantId, isPrivileged);
    }

    // Bib khớp các bộ lọc cấp title (Mfn/DocType/Title/Author/Publisher/PublishYear/Keyword/Summary/CallNumber)
    private IQueryable<Bib> FilterBibs(BookDocSearchRequest r, TenantScope scope)
    {
        var q = db.Bibs.Where(b => b.IsDelete != 2);
        q = scope.IsPrivileged
            ? (scope.RequestTenantId.HasValue ? q.Where(b => b.TenantId == scope.RequestTenantId || b.TenantId == null) : q)
            : q.Where(b => b.TenantId == scope.JwtTenantId);

        if (r.MfnFrom.HasValue)   q = q.Where(b => b.Mfn >= r.MfnFrom);
        if (r.MfnTo.HasValue)     q = q.Where(b => b.Mfn <= r.MfnTo);
        if (r.DocTypeId.HasValue) q = q.Where(b => b.Bib_type_id == r.DocTypeId);

        var hasXmlFilter = !string.IsNullOrEmpty(r.Title) || !string.IsNullOrEmpty(r.Author)
            || !string.IsNullOrEmpty(r.Publisher) || !string.IsNullOrEmpty(r.PublishYear) || !string.IsNullOrEmpty(r.Keyword);
        if (hasXmlFilter)
        {
            var xmlQuery = db.BibXmls.AsQueryable();
            if (!string.IsNullOrEmpty(r.Title))       xmlQuery = xmlQuery.Where(x => x.Title       != null && x.Title.Contains(r.Title));
            if (!string.IsNullOrEmpty(r.Author))      xmlQuery = xmlQuery.Where(x => x.Author      != null && x.Author.Contains(r.Author));
            if (!string.IsNullOrEmpty(r.Publisher))   xmlQuery = xmlQuery.Where(x => x.Publisher   != null && x.Publisher.Contains(r.Publisher));
            if (!string.IsNullOrEmpty(r.PublishYear)) xmlQuery = xmlQuery.Where(x => x.PublishDate != null && x.PublishDate.Contains(r.PublishYear));
            if (!string.IsNullOrEmpty(r.Keyword))     xmlQuery = xmlQuery.Where(x => x.Keyword     != null && x.Keyword.Contains(r.Keyword));
            var bibIds = xmlQuery.Select(x => x.BibId);
            q = q.Where(b => bibIds.Contains(b.Bibid));
        }

        if (!string.IsNullOrEmpty(r.Summary))
        {
            var summaryBibIds = db.BibDatas.Where(d => d.Field == "520" && d.SubField == "a" && d.IsDelete != 2
                && d.Data != null && d.Data.Contains(r.Summary)).Select(d => d.BibId);
            q = q.Where(b => summaryBibIds.Contains(b.Bibid));
        }

        if (!string.IsNullOrEmpty(r.CallNumber))
        {
            var cnBibIds = db.Barcodes.Where(bc => bc.IsDelete != 2 && bc.BarcodeValue != null && bc.BarcodeValue.Contains(r.CallNumber))
                .Select(bc => bc.BibId);
            q = q.Where(b => cnBibIds.Contains(b.Bibid));
        }

        return q;
    }

    // Tập bibId khớp bộ lọc cấp title, lấy qua Elasticsearch (tách từ + bỏ dấu tiếng Việt).
    // Trả về null khi không có tiêu chí nào ở cấp title → bên gọi giữ nguyên truy vấn.
    private async Task<HashSet<long>?> FilterBibIdsElasticAsync(BookDocSearchRequest r, TenantScope scope)
    {
        var f = new PrintBibFilter
        {
            MfnFrom = r.MfnFrom, MfnTo = r.MfnTo, BibTypeId = r.DocTypeId,
            Title = r.Title, Author = r.Author, Publisher = r.Publisher,
            PublishDate = r.PublishYear, Keyword = r.Keyword, Summary = r.Summary,
        };
        if (scope.IsPrivileged)
        {
            if (scope.RequestTenantId.HasValue) { f.TenantId = scope.RequestTenantId; f.TenantIncludeShared = true; }
        }
        else
        {
            f.TenantId = scope.JwtTenantId;
        }
        if (!f.HasAnyCriteria) return null;
        return await elastic.SearchPrintBibIdsAsync(f);
    }

    private bool UseElastic => BibFilterHelper.UseElastic(configuration);

    // Bản title-level: giữ nguyên kiểu trả về IQueryable<Bib> để 2 action bên dưới
    // không phải đổi cách phân trang/sắp xếp, chỉ đổi cách xác định tập bibId.
    private async Task<IQueryable<Bib>> FilterBibsAsync(BookDocSearchRequest r, TenantScope scope)
    {
        if (!UseElastic) return FilterBibs(r, scope);

        var ids = await FilterBibIdsElasticAsync(r, scope);
        var q = db.Bibs.Where(b => b.IsDelete != 2);
        q = scope.IsPrivileged
            ? (scope.RequestTenantId.HasValue ? q.Where(b => b.TenantId == scope.RequestTenantId || b.TenantId == null) : q)
            : q.Where(b => b.TenantId == scope.JwtTenantId);
        if (ids != null) q = q.Where(b => ids.Contains(b.Bibid));
        // Port ELIB-LRC 10-03: ô ĐKCB/ký hiệu không nằm trong chỉ mục ES — trước đây bị bỏ qua khi bật ES (trả mọi đầu sách).
        // ĐKCB khớp theo (mã, đơn vị của biểu ghi).
        if (!string.IsNullOrEmpty(r.CallNumber))
        {
            var cn = r.CallNumber;
            q = q.Where(b => db.Barcodes.Any(bc => bc.IsDelete != 2 && bc.BibId == b.Bibid && bc.TenantId == b.TenantId
                                                 && bc.BarcodeValue != null && bc.BarcodeValue.Contains(cn)));
        }
        return q;
    }

    // ── Module 1: item-level (mỗi dòng = 1 bản sao/ĐKCB) ────────────────────────
    private async Task<IQueryable<Barcode>> BuildItemQueryAsync(BookDocSearchRequest r, TenantScope scope)
    {
        var q = db.Barcodes.Where(x => x.IsDelete != 2);
        q = scope.IsPrivileged
            ? (scope.RequestTenantId.HasValue ? q.Where(x => x.TenantId == scope.RequestTenantId || x.TenantId == null) : q)
            : q.Where(x => x.TenantId == scope.JwtTenantId);
        if (r.StoreId.HasValue)              q = q.Where(x => x.Store == r.StoreId);
        if (!string.IsNullOrEmpty(r.Status)) q = q.Where(x => x.Status == r.Status);
        if (!string.IsNullOrEmpty(r.CallNumber)) q = q.Where(x => x.BarcodeValue != null && x.BarcodeValue.Contains(r.CallNumber));

        var hasBibFilter = r.MfnFrom.HasValue || r.MfnTo.HasValue || r.DocTypeId.HasValue
            || !string.IsNullOrEmpty(r.Title) || !string.IsNullOrEmpty(r.Author) || !string.IsNullOrEmpty(r.Publisher)
            || !string.IsNullOrEmpty(r.PublishYear) || !string.IsNullOrEmpty(r.Keyword) || !string.IsNullOrEmpty(r.Summary);
        if (hasBibFilter)
        {
            if (UseElastic)
            {
                var ids = await FilterBibIdsElasticAsync(r, scope) ?? [];
                q = q.Where(x => x.BibId != null && ids.Contains(x.BibId.Value));
            }
            else
            {
                var bibIds = FilterBibs(r, scope).Select(b => b.Bibid);
                q = q.Where(x => x.BibId != null && bibIds.Contains(x.BibId.Value));
            }
        }

        return q.OrderByDescending(x => x.Id);
    }

    private async Task<List<ItemRow>> MapItemRowsAsync(List<Barcode> barcodes)
    {
        var bibIds     = barcodes.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var receiptIds = barcodes.Where(x => x.Receipt_Id.HasValue).Select(x => x.Receipt_Id!.Value).Distinct().ToList();
        var storeIds   = barcodes.Where(x => x.Store.HasValue).Select(x => (long)x.Store!.Value).Distinct().ToList();
        var statusIds  = barcodes.Where(x => x.Status != null).Select(x => x.Status!).Distinct().ToList();

        var bibMap     = await db.Bibs.Where(b => bibIds.Contains(b.Bibid)).ToDictionaryAsync(b => b.Bibid);
        var xmlMap     = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);
        var receiptMap = await db.AbReceipts.Where(r => receiptIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        // Port ELIB-LRC 10-03: ĐKCB nhập từ hệ thống cũ không có Receipt_Id — hệ thống cũ chỉ lưu đơn nhận theo BIỂU GHI
        // (ab_receipt_detail.Bibid). Suy số đơn nhận từ các dòng đơn nhận chứa biểu ghi (cùng đơn vị); biểu ghi thuộc nhiều
        // đơn thì liệt kê tất cả. Chỉ hiển thị, không đổi dữ liệu.
        var legacyBibIds = barcodes.Where(x => !x.Receipt_Id.HasValue && x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var legacyReceipts = await db.AbReceiptDetails
            .Where(d => d.IsDelete != 2 && d.Bibid != null && legacyBibIds.Contains(d.Bibid.Value))
            .Join(db.AbReceipts.Where(r => r.IsDelete != 2), d => d.Receipt_Id, r => r.Id,
                  (d, r) => new { BibId = d.Bibid!.Value, r.Code, r.Id, r.TenantId })
            .Distinct().ToListAsync();
        var storeMap   = await db.Stores.Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        var statusMap  = await db.BarcodeStatuses.Where(s => s.Id != null && statusIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id!);
        var tenantIds  = barcodes.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value);
        var tenantMap  = await TenantScopeHelper.GetTenantNamesAsync(db, tenantIds);

        return barcodes.Select(bc =>
        {
            Bib? bib = null; BibXml? xml = null;
            if (bc.BibId.HasValue)
            {
                bibMap.TryGetValue(bc.BibId.Value, out bib);
                xmlMap.TryGetValue(bc.BibId.Value, out xml);
            }
            string? donNhan = null;
            if (bc.Receipt_Id.HasValue)
            {
                if (receiptMap.TryGetValue(bc.Receipt_Id.Value, out var receipt)) donNhan = receipt.Code?.ToString();
            }
            else if (bc.BibId.HasValue)
            {
                var codes = legacyReceipts.Where(x => x.BibId == bc.BibId.Value && x.TenantId == bc.TenantId && x.Code.HasValue)
                    .Select(x => x.Code!.Value).Distinct().OrderBy(c => c).ToList();
                if (codes.Count > 0) donNhan = string.Join(", ", codes);
            }
            Store? store = null;
            if (bc.Store.HasValue) storeMap.TryGetValue(bc.Store.Value, out store);

            BarcodeStatus? status = null;
            if (bc.Status != null) statusMap.TryGetValue(bc.Status, out status);

            return new ItemRow
            {
                Mfn         = bib?.Mfn,
                DonNhan     = donNhan,
                Dkcb        = bc.BarcodeValue,
                Title       = xml?.Title,
                Author      = xml?.Author,
                Publisher   = xml?.Publisher,
                PublishYear = xml?.PublishDate,
                StoreId     = bc.Store,
                StoreName   = store?.Name,
                DocTypeId   = bib?.Bib_type_id,
                Status      = bc.Status,
                StatusName  = status?.CommentStatus,
                PublicId    = bc.PublicId,
                TenantName  = bc.TenantId.HasValue && tenantMap.TryGetValue(bc.TenantId.Value, out var tn) ? tn : null,
            };
        }).ToList();
    }

    [HttpPost("Search")]
    [Permission("DOC_SEARCH", "view")]
    public async Task<IActionResult> Search([FromBody] BookDocSearchRequest request)
    {
        var scope = await ResolveTenantScopeAsync(request);
        var query = await BuildItemQueryAsync(request, scope);
        var totalCount = await query.CountAsync();
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize  = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 500);
        var page = await query.Skip((Math.Max(pageIndex, 1) - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = await MapItemRowsAsync(page);
        return Ok(ApiResponse<object>.Ok(new { items, totalCount, pageIndex, pageSize }));
    }

    [HttpPost("ExportDetail")]
    [Permission("DOC_SEARCH", "view")] // port ELIB-LRC 10-03: không có quyền "export" → trước đây luôn 403
    public async Task<IActionResult> ExportDetail([FromBody] BookDocSearchRequest request)
    {
        var scope = await ResolveTenantScopeAsync(request);
        var all = await (await BuildItemQueryAsync(request, scope)).Take(20000).ToListAsync();
        var rows = await MapItemRowsAsync(all);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Chi tiết");
        string[] headers = ["Mfn", "Đơn nhận", "ĐKCB", "Nhan đề", "Tác giả", "Nhà XB", "Năm XB", "Kho", "Trạng thái"];
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH TÀI LIỆU (CHI TIẾT)", headers.Length);
        for (var i = 0; i < headers.Length; i++) ws.Cell(startRow, i + 1).Value = headers[i];
        var row = startRow + 1;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Mfn?.ToString() ?? "";
            ws.Cell(row, 2).Value = r.DonNhan ?? "";
            ws.Cell(row, 3).Value = r.Dkcb ?? "";
            ws.Cell(row, 4).Value = r.Title ?? "";
            ws.Cell(row, 5).Value = r.Author ?? "";
            ws.Cell(row, 6).Value = r.Publisher ?? "";
            ws.Cell(row, 7).Value = r.PublishYear ?? "";
            ws.Cell(row, 8).Value = r.StoreName ?? "";
            ws.Cell(row, 9).Value = r.StatusName ?? "";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TaiLieu_ChiTiet.xlsx");
    }

    // ── Module 2: title-level (mỗi dòng = 1 đầu sách, gộp Số lượng) ─────────────
    private async Task<List<TitleRow>> MapTitleRowsAsync(List<Bib> bibs)
    {
        var bibIds = bibs.Select(b => b.Bibid).ToList();
        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);
        var counts = await db.Barcodes.Where(bc => bc.IsDelete != 2 && bc.BibId.HasValue && bibIds.Contains(bc.BibId.Value))
            .GroupBy(bc => bc.BibId!.Value)
            .Select(g => new { BibId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BibId, x => x.Count);
        var statusIds = bibs.Where(b => b.Status != null).Select(b => b.Status!).Distinct().ToList();
        var statusMap = await db.DBibStatuses.Where(s => s.Id != null && statusIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id!);
        var tenantIds = bibs.Where(b => b.TenantId.HasValue).Select(b => b.TenantId!.Value);
        var tenantMap = await TenantScopeHelper.GetTenantNamesAsync(db, tenantIds);

        return bibs.Select(b =>
        {
            xmlMap.TryGetValue(b.Bibid, out var xml);
            counts.TryGetValue(b.Bibid, out var qty);
            DBibStatus? status = null;
            if (b.Status != null) statusMap.TryGetValue(b.Status, out status);
            return new TitleRow
            {
                Mfn         = b.Mfn,
                Title       = xml?.Title,
                Author      = xml?.Author,
                Publisher   = xml?.Publisher,
                PublishYear = xml?.PublishDate,
                Quantity    = qty,
                DocTypeId   = b.Bib_type_id,
                Status      = b.Status,
                StatusName  = status?.Name,
                PublicId    = b.PublicId,
                TenantName  = b.TenantId.HasValue && tenantMap.TryGetValue(b.TenantId.Value, out var tn) ? tn : null,
            };
        }).ToList();
    }

    [HttpPost("SearchTitles")]
    [Permission("DOC_SEARCH", "view")]
    public async Task<IActionResult> SearchTitles([FromBody] BookDocSearchRequest request)
    {
        var scope = await ResolveTenantScopeAsync(request);
        var query = await FilterBibsAsync(request, scope);
        if (!string.IsNullOrEmpty(request.Status)) query = query.Where(b => b.Status == request.Status);
        query = query.OrderByDescending(b => b.Bibid);

        var totalCount = await query.CountAsync();
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize  = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 500);
        var page = await query.Skip((Math.Max(pageIndex, 1) - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = await MapTitleRowsAsync(page);
        return Ok(ApiResponse<object>.Ok(new { items, totalCount, pageIndex, pageSize }));
    }

    [HttpPost("ExportSummary")]
    [Permission("DOC_SEARCH", "view")] // port ELIB-LRC 10-03: không có quyền "export" → trước đây luôn 403
    public async Task<IActionResult> ExportSummary([FromBody] BookDocSearchRequest request)
    {
        var scope = await ResolveTenantScopeAsync(request);
        var query = await FilterBibsAsync(request, scope);
        if (!string.IsNullOrEmpty(request.Status)) query = query.Where(b => b.Status == request.Status);
        var all = await query.OrderByDescending(b => b.Bibid).Take(20000).ToListAsync();
        var rows = await MapTitleRowsAsync(all);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Tóm tắt");
        string[] headers = ["Mfn", "Nhan đề", "Tác giả", "Nhà XB", "Năm XB", "Số lượng"];
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH TÀI LIỆU (TÓM TẮT)", headers.Length);
        for (var i = 0; i < headers.Length; i++) ws.Cell(startRow, i + 1).Value = headers[i];
        var row = startRow + 1;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Mfn?.ToString() ?? "";
            ws.Cell(row, 2).Value = r.Title ?? "";
            ws.Cell(row, 3).Value = r.Author ?? "";
            ws.Cell(row, 4).Value = r.Publisher ?? "";
            ws.Cell(row, 5).Value = r.PublishYear ?? "";
            ws.Cell(row, 6).Value = r.Quantity;
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TaiLieu_TomTat.xlsx");
    }
}
