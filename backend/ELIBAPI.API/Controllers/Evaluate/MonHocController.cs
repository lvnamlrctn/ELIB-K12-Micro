using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Evaluate;

[Route("api/Evaluate/[controller]")]
public class MonHocController : GenericController<MonHoc, MonHocSearchRequest, MonHocRequest>
{
    private readonly IMinioService _minio;

    public MonHocController(IGenericRepository<MonHoc, MonHocSearchRequest, MonHocRequest> repo, IMinioService minio) : base(repo)
    {
        _minio = minio;
    }

    // ── Upload file đính kèm ─────────────────────────────────────────────────
    [HttpPost("UploadAttachment")]
    [Permission("MONHOC", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidFile"], 400));

        await using var stream = file.OpenReadStream();
        var objectName = await _minio.UploadAsync(stream, file.FileName, file.ContentType);
        return Ok(ApiResponse<object>.Ok(new { path = objectName }, Localizer["UploadSuccess"]));
    }

    [HttpPost("Add")]
    [Permission("MONHOC", "add")]
    public override async Task<IActionResult> Add([FromBody] MonHocRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("MONHOC", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MonHocRequest request) => await base.Update(publicId, request);

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
    public override async Task<IActionResult> Search([FromBody] MonHocSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MonHocSearchRequest request) => await base.SearchAll(request);
}

