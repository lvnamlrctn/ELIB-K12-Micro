using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Circulation/Request")]
public class CirculationRequestController : GenericController<BookRequest, BookRequestSearchRequest, BookRequestRequest>
{
    private readonly ELIBAPIDbContext _db;

    public CirculationRequestController(
        IGenericRepository<BookRequest, BookRequestSearchRequest, BookRequestRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    [HttpPost("Add")]
    [Permission("REQUEST_BOOKS", "add")]
    public override async Task<IActionResult> Add([FromBody] BookRequestRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("REQUEST_BOOKS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BookRequestRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("REQUEST_BOOKS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("REQUEST_BOOKS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("REQUEST_BOOKS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("REQUEST_BOOKS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("REQUEST_BOOKS", "view")]
    public override async Task<IActionResult> Search([FromBody] BookRequestSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("REQUEST_BOOKS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BookRequestSearchRequest request) => await base.SearchAll(request);

    [HttpPost("Approve")]
    [Permission("REQUEST_BOOKS", "edit")]
    public async Task<IActionResult> Approve([FromBody] ApproveRejectRequest r)
    {
        var tenantId = GetTenantId();
        var req = await _db.BookRequests.FirstOrDefaultAsync(x => x.Id == r.RequestId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (req == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy yêu cầu mượn"));
        req.Status       = "A";
        req.UpdateRowBy  = GetCurrentUserId();
        req.UpdatedRowDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã duyệt yêu cầu"));
    }

    [HttpPost("Reject")]
    [Permission("REQUEST_BOOKS", "edit")]
    public async Task<IActionResult> Reject([FromBody] ApproveRejectRequest r)
    {
        var tenantId = GetTenantId();
        var req = await _db.BookRequests.FirstOrDefaultAsync(x => x.Id == r.RequestId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (req == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy yêu cầu mượn"));
        req.Status       = "R";
        req.UpdateRowBy  = GetCurrentUserId();
        req.UpdatedRowDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã từ chối yêu cầu"));
    }

    private long? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}

public class ApproveRejectRequest
{
    public long RequestId { get; set; }
}
