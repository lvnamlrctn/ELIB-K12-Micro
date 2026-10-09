using ELIBAPI.API.Filters;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

/// <summary>Phiếu phạt lưu thông. CRUD chung kế thừa GenericController; nghiệp vụ (danh sách kèm tên bạn đọc, tổng thu,
/// lập/gom/lưu phiếu) ở <see cref="IFineTicketService"/>, lọc theo đơn vị JWT. Đường dẫn và JSON trả về giữ nguyên.</summary>
[Route("api/PrintBook/Circulation/FineTicket")]
public class CFineTicketController(
    IGenericRepository<CFineTicket, CFineTicketSearchRequest, CFineTicketRequest> repo,
    IFineTicketService fines) : GenericController<CFineTicket, CFineTicketSearchRequest, CFineTicketRequest>(repo)
{
    private long? CurrentUserId => GetCurrentUserId() is var id and > 0 ? id : null;

    [HttpPost("Add")]
    [Permission("FINES", "add")]
    public override async Task<IActionResult> Add([FromBody] CFineTicketRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("FINES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CFineTicketRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("FINES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("FINES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> Search([FromBody] CFineTicketSearchRequest request) =>
        Ok(ApiResponse<PagedResult<FineTicketListRow>>.Ok(await fines.SearchAsync(request)));

    [HttpPost("SearchAll")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CFineTicketSearchRequest request) => await base.SearchAll(request);

    /// <summary>Tổng Phải thu/Đã thu/Còn lại trên TOÀN BỘ kết quả khớp bộ lọc (không chỉ trang hiện tại).</summary>
    [HttpPost("Totals")]
    [Permission("FINES", "view")]
    public async Task<IActionResult> Totals([FromBody] CFineTicketSearchRequest request) =>
        Ok(ApiResponse<FineTicketTotals>.Ok(await fines.TotalsAsync(request)));

    /// <summary>Phiếu phạt thủ công (không sinh dòng tài liệu) — nút "Thêm mới" trên trang danh sách.</summary>
    [HttpPost("Create")]
    [Permission("FINES", "add")]
    public async Task<IActionResult> Create([FromBody] CreateFineTicketRequest r) =>
        this.FromServiceResult(await fines.CreateAsync(r, CurrentUserId, GetTenantId()), d => d);

    /// <summary>Gom tài liệu đang mượn quá hạn (+ phiếu mượn được tích chọn) của bạn đọc vào 1 phiếu phạt đang mở.</summary>
    [HttpPost("BuildForReader")]
    [Permission("FINES", "add")]
    public async Task<IActionResult> BuildForReader([FromBody] BuildFineTicketRequest r) =>
        this.FromServiceResult(await fines.BuildForReaderAsync(r, CurrentUserId, GetTenantId()), d => d);

    [HttpGet("Detail/{publicId:guid}")]
    [Permission("FINES", "view")]
    public async Task<IActionResult> Detail(Guid publicId) =>
        this.FromServiceResult(await fines.GetDetailAsync(publicId, GetTenantId()), d => d);

    [HttpPut("Save/{publicId:guid}")]
    [Permission("FINES", "edit")]
    public async Task<IActionResult> Save(Guid publicId, [FromBody] SaveFineTicketRequest r) =>
        this.FromServiceResult(await fines.SaveAsync(publicId, r, CurrentUserId, GetTenantId()), d => d);
}
