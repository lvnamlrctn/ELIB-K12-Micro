using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Circulation/Fine")]
public class CirculationFineController : GenericController<CFine, CFineSearchRequest, CFineRequest>
{
    private readonly ELIBAPIDbContext _db;

    public CirculationFineController(
        IGenericRepository<CFine, CFineSearchRequest, CFineRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    [HttpPost("Add")]
    [Permission("FINES", "add")]
    public override async Task<IActionResult> Add([FromBody] CFineRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("FINES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CFineRequest request) => await base.Update(publicId, request);

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
    public override async Task<IActionResult> Search([FromBody] CFineSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CFineSearchRequest request) => await base.SearchAll(request);

    // Thu phạt — đánh dấu Returndate = now; dùng chung IFineSettlementService với cổng thanh toán QR (port ELIB-LRC 09-25).
    [HttpPost("Pay/{publicId:guid}")]
    [Permission("FINES", "edit")]
    public async Task<IActionResult> Pay(Guid publicId, [FromServices] IFineSettlementService settlement)
    {
        var tenantId = GetTenantId();
        var fineId = await _db.CFines.Where(x => x.PublicId == publicId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId)).Select(x => (long?)x.Id).FirstOrDefaultAsync();
        if (fineId == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy khoản phạt"));
        await settlement.SettleFineAsync(fineId.Value, LibraryClock.Now, GetCurrentUserId());
        return Ok(ApiResponse<string>.Ok("Đã thu phạt"));
    }

    // Miễn phạt — soft delete
    [HttpPost("Waive/{publicId:guid}")]
    [Permission("FINES", "edit")]
    public async Task<IActionResult> Waive(Guid publicId)
    {
        var tenantId = GetTenantId();
        var fine = await _db.CFines.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (fine == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy khoản phạt"));
        fine.IsDelete      = 2;
        fine.UpdateRowBy   = GetCurrentUserId();
        fine.UpdatedRowDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã miễn phạt"));
    }

    private long? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}
