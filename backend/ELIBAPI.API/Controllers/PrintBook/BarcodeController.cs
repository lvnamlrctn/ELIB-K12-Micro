using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class BarcodeController : GenericController<Barcode, BarcodeSearchRequest, BarcodeRequest>
{
    private readonly ELIBAPIDbContext _db;

    public BarcodeController(IGenericRepository<Barcode, BarcodeSearchRequest, BarcodeRequest> repo, ELIBAPIDbContext db) : base(repo)
    {
        _db = db;
    }

    // Đợt 20: mã ĐKCB duy nhất THEO ĐƠN VỊ — đường Add/Update generic trước đây không kiểm tra trùng gì cả.
    private Task<bool> ValueTakenAsync(string? value, long? tenantId, long? exceptId = null)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0) return Task.FromResult(false);
        return _db.Barcodes.AnyAsync(x => x.BarcodeValue == v && x.TenantId == tenantId && x.IsDelete != 2
                                       && (!exceptId.HasValue || x.Id != exceptId.Value));
    }

    [HttpPost("Add")]
    [Permission("AB_RECEIPTS", "add")]
    public override async Task<IActionResult> Add([FromBody] BarcodeRequest request)
    {
        if (await ValueTakenAsync(request.BarcodeValue, GetTenantId()))
            return BadRequest(ApiResponse<string>.Fail($"Số ĐKCB '{request.BarcodeValue?.Trim()}' đã tồn tại"));
        return await base.Add(request);
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BarcodeRequest request)
    {
        var current = await _db.Barcodes.AsNoTracking()
            .Where(x => x.PublicId == publicId && x.IsDelete != 2)
            .Select(x => new { x.Id, x.TenantId }).FirstOrDefaultAsync();
        if (current != null && await ValueTakenAsync(request.BarcodeValue, current.TenantId, current.Id))
            return BadRequest(ApiResponse<string>.Fail($"Số ĐKCB '{request.BarcodeValue?.Trim()}' đã tồn tại"));
        return await base.Update(publicId, request);
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AB_RECEIPTS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> Search([FromBody] BarcodeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BarcodeSearchRequest request) => await base.SearchAll(request);
}
