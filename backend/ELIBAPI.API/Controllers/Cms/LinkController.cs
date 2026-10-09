using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class LinkController : GenericController<Link, LinkSearchRequest, LinkRequest>
{
    private readonly IMinioService _minio;
    private readonly ICacheInvalidator _cacheInvalidator;

    public LinkController(
        IGenericRepository<Link, LinkSearchRequest, LinkRequest> repo,
        IMinioService minio,
        ICacheInvalidator cacheInvalidator) : base(repo)
    {
        _minio = minio;
        _cacheInvalidator = cacheInvalidator;
    }

    [HttpPost("Add")]
    [Permission("LINK", "add")]
    [Consumes("multipart/form-data")]
    public override async Task<IActionResult> Add([FromForm] LinkRequest request)
    {
        var imageFile = Request.Form.Files.GetFile("imageFile");
        if (imageFile != null && imageFile.Length > 0)
            request.Images = await UploadToMinioAsync(imageFile);

        var entity = await _repo.AddAsync(request);
        _cacheInvalidator.Invalidate(nameof(PublicHyperLinkResponse));
        return Ok(ApiResponse<Link>.Ok(entity, Localizer["AddSuccess"]));
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("LINK", "edit")]
    [Consumes("multipart/form-data")]
    public override async Task<IActionResult> Update(Guid publicId, [FromForm] LinkRequest request)
    {
        var imageFile = Request.Form.Files.GetFile("imageFile");
        if (imageFile != null && imageFile.Length > 0)
        {
            var existing = await _repo.GetByPublicIdAsync(publicId);
            var oldImage = existing?.Images;

            request.Images = await UploadToMinioAsync(imageFile);

            if (IsMinioUrl(oldImage))
                _ = _minio.DeleteAsync(oldImage!);
        }

        try
        {
            var entity = await _repo.UpdateAsync(publicId, request);
            _cacheInvalidator.Invalidate(nameof(PublicHyperLinkResponse));
            return Ok(ApiResponse<Link>.Ok(entity, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<Link>.Fail(Localizer["NotFound"], 404));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<Link>.Fail(ex.Message));
        }
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("LINK", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        var result = await base.Delete(publicId);
        _cacheInvalidator.Invalidate(nameof(PublicHyperLinkResponse));
        return result;
    }

    [HttpPut("ChangeStatus")]
    [Permission("LINK", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
    {
        var result = await base.ChangeStatus(request);
        _cacheInvalidator.Invalidate(nameof(PublicHyperLinkResponse));
        return result;
    }

    [HttpGet("{id:long}")]
    [Permission("LINK", "view")]
    public override async Task<IActionResult> GetById(long id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound(ApiResponse<Link>.Fail(Localizer["NotFound"], 404));
        entity.Images = ResolveImageUrl(entity.Images);
        return Ok(ApiResponse<Link>.Ok(entity));
    }

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("LINK", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId)
    {
        var entity = await _repo.GetByPublicIdAsync(publicId);
        if (entity == null) return NotFound(ApiResponse<Link>.Fail(Localizer["NotFound"], 404));
        entity.Images = ResolveImageUrl(entity.Images);
        return Ok(ApiResponse<Link>.Ok(entity));
    }

    [HttpPost("Search")]
    [Permission("LINK", "view")]
    public override async Task<IActionResult> Search([FromBody] LinkSearchRequest request)
    {
        var result = await _repo.SearchAsync(request);
        foreach (var item in result.Items) item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<PagedResult<Link>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("LINK", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] LinkSearchRequest request)
    {
        var items = await _repo.SearchAllAsync(request);
        foreach (var item in items) item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<List<Link>>.Ok(items));
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";

    private async Task<string> UploadToMinioAsync(IFormFile file)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return await _minio.UploadAsync(ms, file.FileName, file.ContentType);
    }

    private bool IsMinioUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        (url.StartsWith(_minio.PublicBaseUrl, StringComparison.OrdinalIgnoreCase)
         || (url.Length >= 8 && char.IsDigit(url[0]) && url[4] == '/'));
}
