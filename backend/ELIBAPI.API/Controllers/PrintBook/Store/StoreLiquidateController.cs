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

/// <summary>Thanh lý tài liệu. Nghiệp vụ ở <see cref="ILiquidateService"/> (port ELIB-LRC 10-04); controller phân giải phạm
/// vi đơn vị rồi truyền xuống.</summary>
[Route("api/PrintBook/Store/Liquidate")]
public class StoreLiquidateController : GenericController<Thanhly, ThanhlySearchRequest, ThanhlyRequest>
{
    private readonly ELIBAPIDbContext _db;
    private readonly ILiquidateService _liquidates;

    public StoreLiquidateController(
        IGenericRepository<Thanhly, ThanhlySearchRequest, ThanhlyRequest> repo,
        ELIBAPIDbContext db,
        ILiquidateService liquidates) : base(repo)
    {
        _db = db;
        _liquidates = liquidates;
    }

    private long? CurrentUserId => GetCurrentUserId() is var id and > 0 ? id : null;

    [HttpGet("{id:long}")] [Permission("LIQUIDATES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("LIQUIDATES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    // Trả đủ trường màn hình đọc (barcode, nhan đề, tác giả, kho, ngày, lý do) — trước đây entity thô nên mọi cột trống.
    [HttpPost("Search")] [Permission("LIQUIDATES", "view")]
    public override async Task<IActionResult> Search([FromBody] ThanhlySearchRequest r) =>
        Ok(ApiResponse<PagedResult<LiquidateRow>>.Ok(await SearchScopedAsync(r, paged: true)));

    [HttpPost("SearchAll")] [Permission("LIQUIDATES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ThanhlySearchRequest r) => await base.SearchAll(r);

    [HttpPost("Add")]
    [Permission("LIQUIDATES", "add")]
    public override async Task<IActionResult> Add([FromBody] ThanhlyRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("LIQUIDATES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ThanhlyRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("LIQUIDATES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("LIQUIDATES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPost("Liquidate")]
    [Permission("LIQUIDATES", "add")]
    public async Task<IActionResult> Liquidate([FromBody] LiquidateRequest r) =>
        this.FromServiceResult(await _liquidates.LiquidateAsync(r.Barcode, r.Reason, r.LiquidateDate, CurrentUserId, GetTenantId()), x => x);

    [HttpPost("ReLiquidate")]
    [Permission("LIQUIDATES", "edit")]
    public async Task<IActionResult> ReLiquidate([FromBody] ReLiquidateRequest r) =>
        this.FromServiceResult(await _liquidates.CancelAsync(r.Barcode, CurrentUserId, GetTenantId()), _ => "Đã hủy thanh lý");

    // Xuất Excel danh sách thanh lý theo đúng bộ lọc đang tìm kiếm
    [HttpPost("Export")]
    [Permission("LIQUIDATES", "view")]
    public async Task<IActionResult> Export([FromBody] ThanhlySearchRequest r, [FromServices] ISystemParameterService sysParam)
    {
        var items = (await SearchScopedAsync(r, paged: false)).Items;

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Thanh lý");
        var headers = new[] { "STT", "Đăng ký cá biệt", "Nhan đề", "Tác giả", "Nhà xuất bản", "Quản lý Kho", "Ngày thanh lý", "Lý do thanh lý" };
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH TÀI LIỆU THANH LÝ", headers.Length);
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
            ws.Cell(row, 4).Value = x.Author ?? "";
            ws.Cell(row, 5).Value = x.Publisher ?? "";
            ws.Cell(row, 6).Value = x.StoreName ?? "";
            ws.Cell(row, 7).Value = x.LiquidateDate?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(row, 8).Value = x.Reason ?? "";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"thanh-ly-{LibraryClock.Now:yyyyMMddHHmmss}.xlsx");
    }

    private async Task<PagedResult<LiquidateRow>> SearchScopedAsync(ThanhlySearchRequest r, bool paged)
    {
        var scope = await TenantScopeHelper.ResolveScopeAsync(_db, r.TenantId, GetTenantId(), IsPrivilegedRole());
        return await _liquidates.SearchAsync(r, paged, scope.TenantId, scope.IncludeShared, scope.All);
    }
}

public class LiquidateRequest
{
    public string?   Barcode       { get; set; }
    public string?   Reason        { get; set; }
    public DateTime? LiquidateDate { get; set; }
}

public class ReLiquidateRequest { public string? Barcode { get; set; } }
