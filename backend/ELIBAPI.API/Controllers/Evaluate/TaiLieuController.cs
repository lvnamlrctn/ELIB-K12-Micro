using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Evaluate;

[Route("api/Evaluate/[controller]")]
public class TaiLieuController : GenericController<TaiLieu, TaiLieuSearchRequest, TaiLieuRequest>
{
    private readonly ELIBAPIDbContext _db;

    public TaiLieuController(IGenericRepository<TaiLieu, TaiLieuSearchRequest, TaiLieuRequest> repo, ELIBAPIDbContext db) : base(repo)
    {
        _db = db;
    }

    [HttpPost("Add")]
    [Permission("MONHOC", "add")]
    public override async Task<IActionResult> Add([FromBody] TaiLieuRequest request)
    {
        var error = await ValidateSingleMainDocument(request, excludePublicId: null);
        if (error != null) return error;
        return await base.Add(request);
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("MONHOC", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] TaiLieuRequest request)
    {
        var error = await ValidateSingleMainDocument(request, excludePublicId: publicId);
        if (error != null) return error;
        return await base.Update(publicId, request);
    }

    // ── Mỗi môn học chỉ được có 1 Tài liệu chính (LoaiTaiLieu = 1) ──────────────
    private async Task<IActionResult?> ValidateSingleMainDocument(TaiLieuRequest request, Guid? excludePublicId)
    {
        if (request.LoaiTaiLieu != 1 || request.MonHocId == null) return null;

        var query = _db.TaiLieus.Where(x => x.IsDelete != 2 && x.MonHocId == request.MonHocId && x.LoaiTaiLieu == 1);
        if (excludePublicId.HasValue) query = query.Where(x => x.PublicId != excludePublicId.Value);

        var exists = await query.AnyAsync();
        return exists ? BadRequest(ApiResponse<object>.Fail(Localizer["OnlyOneMainDocumentPerSubject"], 400)) : null;
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("MONHOC", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("MONHOC", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);



    [HttpGet("{id:long}")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> Search([FromBody] TaiLieuSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] TaiLieuSearchRequest request) => await base.SearchAll(request);
}
