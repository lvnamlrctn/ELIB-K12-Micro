using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

/// <summary>Sách mất. Nghiệp vụ ở <see cref="ILostBookService"/> (port ELIB-LRC 10-04); controller phân giải phạm vi đơn vị
/// (JWT / đơn vị tài khoản đặc quyền chọn) rồi truyền xuống.</summary>
[Route("api/PrintBook/Store/LostBook")]
public class StoreLostBookController : GenericController<LostBook, LostBookSearchRequest, LostBookRequest>
{
    private readonly ELIBAPIDbContext _db;
    private readonly ILostBookService _lostBooks;

    public StoreLostBookController(
        IGenericRepository<LostBook, LostBookSearchRequest, LostBookRequest> repo,
        ELIBAPIDbContext db,
        ILostBookService lostBooks) : base(repo)
    {
        _db = db;
        _lostBooks = lostBooks;
    }

    private long? CurrentUserId => GetCurrentUserId() is var id and > 0 ? id : null;

    [HttpGet("{id:long}")] [Permission("LOST_BOOKS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("LOST_BOOKS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    // Danh sách gồm cả ĐKCB đang "Mất" chưa có bản ghi báo mất (dữ liệu cũ / phiếu phạt) — xem ILostBookService.
    [HttpPost("Search")] [Permission("LOST_BOOKS", "view")]
    public override async Task<IActionResult> Search([FromBody] LostBookSearchRequest r) =>
        Ok(ApiResponse<PagedResult<LostBookRow>>.Ok(await SearchScopedAsync(r, paged: true)));

    [HttpPost("SearchAll")] [Permission("LOST_BOOKS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] LostBookSearchRequest r) => await base.SearchAll(r);

    [HttpPost("Add")]
    [Permission("LOST_BOOKS", "add")]
    public override async Task<IActionResult> Add([FromBody] LostBookRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("LOST_BOOKS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] LostBookRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("LOST_BOOKS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("LOST_BOOKS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    // Tra cứu theo Đăng ký cá biệt để tự động điền MFN / Kho / Trạng thái / ISBD lên form báo mất
    [HttpGet("LookupBarcode/{barcode}")]
    [Permission("LOST_BOOKS", "view")]
    public async Task<IActionResult> LookupBarcode(string barcode) =>
        this.FromServiceResult(await _lostBooks.LookupAsync(barcode, GetTenantId()), x => x);

    // Báo mất: gán Barcode.Status = 'L' (Mất) + lưu bản ghi thất lạc. Chặn bản đang mượn (xử lý qua phiếu phạt) / đã thanh lý.
    [HttpPost("MarkLost")]
    [Permission("LOST_BOOKS", "add")]
    public async Task<IActionResult> MarkLost([FromBody] MarkLostRequest r) =>
        this.FromServiceResult(await _lostBooks.MarkLostAsync(r.Barcode, r.LossDate, r.Reason, CurrentUserId, GetTenantId()), x => x);

    // Khôi phục mất: xoá bản ghi sách mất, ĐKCB đang "Mất" trả về 'R'
    [HttpPost("RestoreLost")]
    [Permission("LOST_BOOKS", "edit")]
    public async Task<IActionResult> RestoreLost([FromBody] UndoLostRequest r) =>
        this.FromServiceResult(await _lostBooks.RestoreAsync(r.Barcode, CurrentUserId, GetTenantId()), _ => "Đã khôi phục mất tài liệu");

    // Xuất Excel danh sách sách mất theo đúng bộ lọc đang tìm kiếm
    [HttpPost("Export")]
    [Permission("LOST_BOOKS", "view")]
    public async Task<IActionResult> Export([FromBody] LostBookSearchRequest r, [FromServices] ISystemParameterService sysParam)
    {
        var items = (await SearchScopedAsync(r, paged: false)).Items;

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Sách mất");
        var headers = new[] { "STT", "Đăng ký cá biệt", "Nhan đề", "Quản lý Kho", "Ngày mất", "Lý do mất" };
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH TÀI LIỆU MẤT", headers.Length);
        for (int i = 0; i < headers.Length; i++) ws.Cell(startRow, i + 1).Value = headers[i];
        var headerRow = ws.Row(startRow);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

        int row = startRow + 1;
        foreach (var x in items)
        {
            ws.Cell(row, 1).Value = row - startRow;
            ws.Cell(row, 2).Value = x.Barcode ?? "";
            ws.Cell(row, 3).Value = x.BibTitle ?? "";
            ws.Cell(row, 4).Value = x.StoreName ?? "";
            ws.Cell(row, 5).Value = x.Submited?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(row, 6).Value = x.Reason ?? (x.Recorded ? "" : "(chưa có bản ghi báo mất)");
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"sach-mat-{LibraryClock.Now:yyyyMMddHHmmss}.xlsx");
    }

    private async Task<PagedResult<LostBookRow>> SearchScopedAsync(LostBookSearchRequest r, bool paged)
    {
        var scope = await TenantScopeHelper.ResolveScopeAsync(_db, r.TenantId, GetTenantId(), IsPrivilegedRole());
        return await _lostBooks.SearchAsync(r, paged, scope.TenantId, scope.IncludeShared, scope.All);
    }
}

public class MarkLostRequest
{
    public string?   Barcode  { get; set; }
    public DateTime? LossDate { get; set; }
    public string?   Reason   { get; set; }
}

public class UndoLostRequest { public string? Barcode { get; set; } }
