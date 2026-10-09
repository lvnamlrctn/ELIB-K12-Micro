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

// Sách tạm thời ra khỏi kho vì lý do khác cho mượn (đi photo, triển lãm, xuất Huyện...) — quét ĐKCB
// từng mã, không có bước tìm kiếm riêng (nghiệp vụ ở FE: quét mã -> xử lý ngay 1 giao dịch).
[Route("api/PrintBook/Store/BookOutStore")]
public class BookOutStoreController : GenericController<BookOutStore, BookOutStoreSearchRequest, BookOutStoreRequest>
{
    private readonly ELIBAPIDbContext _db;

    public BookOutStoreController(
        IGenericRepository<BookOutStore, BookOutStoreSearchRequest, BookOutStoreRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    [HttpPost("Add")]
    [Permission("BOOK_OUT_STORE", "add")]
    public override async Task<IActionResult> Add([FromBody] BookOutStoreRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("BOOK_OUT_STORE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BookOutStoreRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("BOOK_OUT_STORE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        // Port ELIB-LRC 10-04: xoá giao dịch xuất kho CÒN MỞ trước đây để ĐKCB kẹt ở "X" (không xuất vì đã X, không nhập
        // vì hết giao dịch mở) — nay trả ĐKCB về kho nếu không còn giao dịch mở nào khác. Repo kiểm tra đơn vị khi xoá.
        var tx = await _repo.GetByPublicIdAsync(publicId);
        var result = await base.Delete(publicId);
        if (result is not OkObjectResult || tx is not { Status: OpenTx, BarcodeId: long barcodeId }) return result;

        var barcode = await _db.Barcodes.FirstOrDefaultAsync(b => b.Id == barcodeId);
        if (barcode?.Status == OutStatus
            && !await _db.BookOutStores.AnyAsync(x => x.BarcodeId == barcodeId && x.Status == OpenTx && x.IsDelete != 2 && x.Id != tx.Id))
        {
            barcode.Status         = InStoreStatus;
            barcode.UpdateRowBy    = GetCurrentUserId();
            barcode.UpdatedRowDate = LibraryClock.Now;
            await _db.SaveChangesAsync();
        }
        return result;
    }

    [HttpPut("ChangeStatus")]
    [Permission("BOOK_OUT_STORE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("BOOK_OUT_STORE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("BOOK_OUT_STORE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("BOOK_OUT_STORE", "view")]
    public override async Task<IActionResult> Search([FromBody] BookOutStoreSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("BOOK_OUT_STORE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BookOutStoreSearchRequest request) => await base.SearchAll(request);

    // Áp toàn bộ bộ lọc tra cứu (Loại/Đơn vị/Lý do/Địa điểm/Người giao-nhận/Số ĐKCB/Trả tài liệu/
    // khoảng ngày Giao-Nhận/Thư viện) lên BookOutStore — dùng chung cho History (phân trang) và Export
    // (toàn bộ). Đợt 24.4: r.TenantId (chọn đơn vị cụ thể, chỉ có tác dụng với user đặc quyền) trước đây
    // bị bỏ qua hoàn toàn — chỉ lọc theo JWT.
    private async Task<IQueryable<BookOutStore>> BuildFilteredQueryAsync(BookOutStoreSearchRequest r)
    {
        var jwtTenantId   = GetTenantId();
        var isPrivileged  = IsPrivilegedRole();
        var requestTenantId = await TenantScopeHelper.ResolveRequestTenantIdAsync(_db, r.TenantId, jwtTenantId, isPrivileged);

        var query = _db.BookOutStores.Where(x => x.IsDelete != 2);
        query = isPrivileged
            ? (requestTenantId.HasValue ? query.Where(x => x.TenantId == requestTenantId || x.TenantId == null) : query)
            : query.Where(x => x.TenantId == jwtTenantId);

        if (!string.IsNullOrEmpty(r.Keyword))
        {
            var kw = r.Keyword.Trim().ToLower();
            query = query.Where(x => (x.DelivererName != null && x.DelivererName.ToLower().Contains(kw))
                                   || (x.ReceiverName != null && x.ReceiverName.ToLower().Contains(kw)));
        }
        if (!string.IsNullOrEmpty(r.TypeStatus)) query = query.Where(x => x.Status == r.TypeStatus);
        if (r.UnitId.HasValue) query = query.Where(x => x.UnitId == r.UnitId);
        if (r.ReasonId.HasValue) query = query.Where(x => x.ReasonId == r.ReasonId);
        if (r.ExhibitionLocationId.HasValue) query = query.Where(x => x.ExhibitionLocationId == r.ExhibitionLocationId);
        if (!string.IsNullOrEmpty(r.DelivererName)) { var dn = r.DelivererName.Trim().ToLower(); query = query.Where(x => x.DelivererName != null && x.DelivererName.ToLower().Contains(dn)); }
        if (!string.IsNullOrEmpty(r.ReceiverName)) { var rn = r.ReceiverName.Trim().ToLower(); query = query.Where(x => x.ReceiverName != null && x.ReceiverName.ToLower().Contains(rn)); }
        if (r.ReturnBarcodeAtLibrary.HasValue) query = query.Where(x => x.ReturnBarcodeAtLibrary == r.ReturnBarcodeAtLibrary);
        if (r.ExportDateFrom.HasValue) query = query.Where(x => x.ExportDate >= r.ExportDateFrom.Value.Date);
        if (r.ExportDateTo.HasValue) query = query.Where(x => x.ExportDate < r.ExportDateTo.Value.Date.AddDays(1));
        if (r.ImportDateFrom.HasValue) query = query.Where(x => x.ImportDate >= r.ImportDateFrom.Value.Date);
        if (r.ImportDateTo.HasValue) query = query.Where(x => x.ImportDate < r.ImportDateTo.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(r.Barcode))
        {
            var bcKw = r.Barcode.Trim().ToLower();
            var barcodeIds = _db.Barcodes.Where(b => b.BarcodeValue != null && b.BarcodeValue.ToLower().Contains(bcKw)).Select(b => b.Id);
            query = query.Where(x => x.BarcodeId.HasValue && barcodeIds.Contains(x.BarcodeId.Value));
        }

        return query.OrderByDescending(x => x.Id);
    }

    // Lịch sử giao dịch — enrich barcode/nhan đề/tên lý do/đơn vị/địa điểm để hiển thị trực tiếp lên bảng.
    [HttpPost("History")]
    [Permission("BOOK_OUT_STORE", "view")]
    public async Task<IActionResult> History([FromBody] BookOutStoreSearchRequest r)
    {
        var query = await BuildFilteredQueryAsync(r);

        var total = await query.CountAsync();
        var page  = Math.Max(1, r.PageIndex);
        var size  = r.PageSize <= 0 ? 10 : Math.Min(r.PageSize, 200);

        var raw = await query
            .Skip((Math.Max(page, 1) - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = await EnrichAsync(raw);
        return Ok(ApiResponse<object>.Ok(new { items, recordsTotal = total }));
    }

    // Xuất Excel toàn bộ kết quả đang lọc (không phân trang, không phụ thuộc dòng đã chọn).
    [HttpPost("Export")]
    [Permission("BOOK_OUT_STORE", "view")]
    public async Task<IActionResult> Export([FromBody] BookOutStoreSearchRequest r, [FromServices] ISystemParameterService sysParam)
    {
        var raw = await (await BuildFilteredQueryAsync(r)).ToListAsync();
        var items = await EnrichAsync(raw);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Sách ra vào kho");
        var headers = new[] { "STT", "Số ĐKCB", "Nhan đề", "Ngày giao", "Ngày nhận", "Trạng thái", "Lý do" };
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH SÁCH RA VÀO KHO", headers.Length);
        for (int i = 0; i < headers.Length; i++) ws.Cell(startRow, i + 1).Value = headers[i];
        var headerRow = ws.Row(startRow);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

        int row = startRow + 1;
        foreach (var x in items)
        {
            ws.Cell(row, 1).Value = row - startRow;
            ws.Cell(row, 2).Value = x.barcode ?? "";
            ws.Cell(row, 3).Value = x.bibTitle ?? "";
            ws.Cell(row, 4).Value = x.ExportDate?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(row, 5).Value = x.ImportDate?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(row, 6).Value = x.Status == "R" ? "Nhập kho" : "Xuất kho";
            ws.Cell(row, 7).Value = x.reasonName ?? "";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"sach-ra-vao-kho-{LibraryClock.Now:yyyyMMddHHmmss}.xlsx");
    }

    private async Task<List<dynamic>> EnrichAsync(List<BookOutStore> raw)
    {
        var barcodeIds = raw.Where(x => x.BarcodeId.HasValue).Select(x => x.BarcodeId!.Value).Distinct().ToList();
        var barcodes   = await _db.Barcodes.Where(b => barcodeIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b);
        var bibIds     = barcodes.Values.Where(b => b.BibId.HasValue).Select(b => b.BibId!.Value).Distinct().ToList();
        var bibTitles  = await _db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId, x => x.Title);

        var reasonIds   = raw.Where(x => x.ReasonId.HasValue).Select(x => x.ReasonId!.Value).Distinct().ToList();
        var reasonNames = await _db.DExportReasons.Where(x => reasonIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        var tenantIds   = raw.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value);
        var tenantNames = await TenantScopeHelper.GetTenantNamesAsync(_db, tenantIds);

        return raw.Select(x =>
        {
            Barcode? bc = x.BarcodeId.HasValue && barcodes.TryGetValue(x.BarcodeId.Value, out var b) ? b : null;
            return (dynamic)new
            {
                x.Id,
                x.PublicId,
                barcode     = bc?.BarcodeValue,
                bibTitle    = bc?.BibId.HasValue == true && bibTitles.TryGetValue(bc.BibId!.Value, out var t) ? t : null,
                x.ExportDate,
                x.ImportDate,
                x.Status,
                reasonName  = x.ReasonId.HasValue && reasonNames.TryGetValue(x.ReasonId.Value, out var rn) ? rn : null,
                tenantName  = x.TenantId.HasValue && tenantNames.TryGetValue(x.TenantId.Value, out var tn) ? tn : null,
            };
        }).ToList();
    }

    // Xuất kho: quét 1 ĐKCB, tạo giao dịch mới + khoá Barcode.Status = 'X' (không cho mượn khi đang ra ngoài).
    // Port ELIB-LRC 10-04: chặn bản đã thanh lý (S); chặn bản "R" nhưng phiếu mượn còn mở (dữ liệu lệch — trước đây
    // vẫn xuất kho và khoá về "X" dù bạn đọc đang giữ sách); "X" không có giao dịch mở thì hướng dẫn nhập kho trước.
    [HttpPost("ScanOut")]
    [Permission("BOOK_OUT_STORE", "edit")]
    public async Task<IActionResult> ScanOut([FromBody] ScanOutRequest r)
    {
        var code = (r.Barcode ?? "").Trim();
        if (code == "")
            return BadRequest(ApiResponse<string>.Fail("Số ĐKCB không được để trống"));

        var tenantId = GetTenantId();
        var (barcode, barcodeAmbiguous) = await BarcodeTenantLookup.ByValueAsync(_db.Barcodes, code, tenantId, normalize: true);
        if (barcodeAmbiguous) return BadRequest(ApiResponse<string>.Fail(BarcodeTenantLookup.AmbiguousMessage));
        if (barcode == null)
            return NotFound(ApiResponse<string>.Fail("Không tìm thấy Đăng ký cá biệt"));

        if (barcode.Status != null && Blocked.TryGetValue(barcode.Status, out var reason))
            return BadRequest(ApiResponse<string>.Fail(reason));
        if (await OpenTransactionAsync(barcode.Id) != null)
            return BadRequest(ApiResponse<string>.Fail("Tài liệu đã xuất kho, chưa nhập lại"));
        if (barcode.Status == OutStatus)
            return BadRequest(ApiResponse<string>.Fail("Tài liệu đang ở trạng thái xử lý kỹ thuật (X) — quét Nhập kho để đưa về kho trước khi xuất"));
        // Phiếu mượn mở cùng mã + cùng đơn vị (mã ĐKCB chỉ duy nhất trong 1 đơn vị).
        if (await PrintLoans.Open(_db).AnyAsync(o => o.Barcode == barcode.BarcodeValue && (o.TenantId ?? 0) == (barcode.TenantId ?? 0)))
            return BadRequest(ApiResponse<string>.Fail(Blocked["B"]));

        var userId = GetCurrentUserId();
        var now    = LibraryClock.Now;
        var entity = new BookOutStore
        {
            BarcodeId              = barcode.Id,
            DelivererName          = r.DelivererName,
            ReceiverName           = r.ReceiverName,
            ReasonId               = r.ReasonId,
            UnitId                 = r.UnitId,
            ExhibitionLocationId   = r.ExhibitionLocationId,
            ReturnBarcodeAtLibrary = r.ReturnBarcodeAtLibrary,
            Store                  = barcode.Store,
            ExportDate             = now,
            Status                 = OpenTx,
            PublicId               = Guid.NewGuid(),
            TenantId               = barcode.TenantId ?? tenantId,
            CreatedRowBy           = userId,
            CreatedRowDate         = now
        };
        _db.BookOutStores.Add(entity);

        barcode.Status         = OutStatus;
        barcode.UpdateRowBy    = userId;
        barcode.UpdatedRowDate = now;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<BookOutStore>.Ok(entity));
    }

    // Nhập kho: quét lại ĐKCB đang xuất kho, đóng giao dịch + trả Barcode.Status = 'R'.
    // Port ELIB-LRC 10-04: ĐKCB "X" của dữ liệu cũ không có giao dịch xuất kho trước đây kẹt vĩnh viễn (không xuất được
    // vì đã "X", không nhập được vì không có giao dịch mở) — nay nhập về kho và ghi 1 giao dịch nhập để có lịch sử.
    [HttpPost("ScanIn")]
    [Permission("BOOK_OUT_STORE", "edit")]
    public async Task<IActionResult> ScanIn([FromBody] ScanInRequest r)
    {
        var code = (r.Barcode ?? "").Trim();
        if (code == "")
            return BadRequest(ApiResponse<string>.Fail("Số ĐKCB không được để trống"));

        var tenantId = GetTenantId();
        var (barcode, barcodeAmbiguous) = await BarcodeTenantLookup.ByValueAsync(_db.Barcodes, code, tenantId, normalize: true);
        if (barcodeAmbiguous) return BadRequest(ApiResponse<string>.Fail(BarcodeTenantLookup.AmbiguousMessage));
        if (barcode == null)
            return NotFound(ApiResponse<string>.Fail("Không tìm thấy Đăng ký cá biệt"));

        var userId = GetCurrentUserId();
        var now    = LibraryClock.Now;
        var open   = await OpenTransactionAsync(barcode.Id);
        if (open == null)
        {
            if (barcode.Status != OutStatus)
                return BadRequest(ApiResponse<string>.Fail("Tài liệu không có giao dịch xuất kho đang mở"));
            open = new BookOutStore
            {
                BarcodeId = barcode.Id, Store = barcode.Store, Status = OpenTx, PublicId = Guid.NewGuid(),
                TenantId = barcode.TenantId ?? tenantId, CreatedRowBy = userId, CreatedRowDate = now
            };
            _db.BookOutStores.Add(open);
        }
        open.ImportDate     = now;
        open.Status         = ClosedTx;
        open.UpdateRowBy    = userId;
        open.UpdatedRowDate = now;

        barcode.Status         = InStoreStatus;
        barcode.UpdateRowBy    = userId;
        barcode.UpdatedRowDate = now;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<BookOutStore>.Ok(open));
    }

    private const string OpenTx = "O", ClosedTx = "R", OutStatus = "X", InStoreStatus = "R";

    private static readonly Dictionary<string, string> Blocked = new()
    {
        ["B"] = "Tài liệu đang được bạn đọc mượn",
        ["L"] = "Tài liệu đang ở trạng thái mất",
        ["S"] = "Tài liệu đã thanh lý",
    };

    private Task<BookOutStore?> OpenTransactionAsync(long barcodeId) =>
        _db.BookOutStores.Where(x => x.BarcodeId == barcodeId && x.Status == OpenTx && x.IsDelete != 2)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
}
