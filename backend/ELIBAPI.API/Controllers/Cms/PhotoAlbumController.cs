using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class PhotoAlbumController : GenericController<PhotoAlbum, PhotoAlbumSearchRequest, PhotoAlbumRequest>
{
    private readonly IPhotoAlbumRepository _photoAlbumRepo;
    private readonly IMinioService         _minio;

    public PhotoAlbumController(IPhotoAlbumRepository repo, IMinioService minio) : base(repo)
    {
        _photoAlbumRepo = repo;
        _minio          = minio;
    }

    [HttpPost("Add")]
    [Permission("IMAGE_COLLECTIONS", "add")]
    [Consumes("multipart/form-data")]
    public override async Task<IActionResult> Add([FromForm] PhotoAlbumRequest request)
    {
        var imageFile = Request.Form.Files.GetFile("imageFile");
        if (imageFile != null && imageFile.Length > 0)
            request.Image = await UploadToMinioAsync(imageFile);

        var entity = await _repo.AddAsync(request);
        return Ok(ApiResponse<PhotoAlbum>.Ok(entity, Localizer["AddSuccess"]));
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("IMAGE_COLLECTIONS", "edit")]
    [Consumes("multipart/form-data")]
    public override async Task<IActionResult> Update(Guid publicId, [FromForm] PhotoAlbumRequest request)
    {
        var imageFile = Request.Form.Files.GetFile("imageFile");
        if (imageFile != null && imageFile.Length > 0)
        {
            var existing = await _repo.GetByPublicIdAsync(publicId);
            var oldImage = existing?.Image;

            request.Image = await UploadToMinioAsync(imageFile);

            if (IsMinioUrl(oldImage))
                _ = _minio.DeleteAsync(oldImage!);
        }

        try
        {
            var entity = await _repo.UpdateAsync(publicId, request);
            return Ok(ApiResponse<PhotoAlbum>.Ok(entity, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<PhotoAlbum>.Fail(Localizer["NotFound"], 404));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<PhotoAlbum>.Fail(ex.Message));
        }
    }

    [HttpPut("ChangeIsSpecial")]
    [Permission("IMAGE_COLLECTIONS", "edit")]
    public async Task<IActionResult> ChangeIsSpecial([FromBody] ChangeIsSpecialRequest request)
    {
        try
        {
            await _photoAlbumRepo.ChangeIsSpecialAsync(request);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("IMAGE_COLLECTIONS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("IMAGE_COLLECTIONS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("IMAGE_COLLECTIONS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("IMAGE_COLLECTIONS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("IMAGE_COLLECTIONS", "view")]
    public override async Task<IActionResult> Search([FromBody] PhotoAlbumSearchRequest request)
    {
        var result = await _repo.SearchAsync(request);
        foreach (var item in result.Items) item.Image = ResolveImageUrl(item.Image);
        return Ok(ApiResponse<PagedResult<PhotoAlbum>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("IMAGE_COLLECTIONS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PhotoAlbumSearchRequest request)
    {
        var items = await _repo.SearchAllAsync(request);
        foreach (var item in items) item.Image = ResolveImageUrl(item.Image);
        return Ok(ApiResponse<List<PhotoAlbum>>.Ok(items));
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
