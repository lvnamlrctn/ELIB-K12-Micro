using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class BannerController : GenericController<Banner, BannerSearchRequest, BannerRequest>
{
    private readonly IMinioService _minio;
    private readonly ICacheInvalidator _cacheInvalidator;

    // Đợt 22 — báo OPAC (PublicBannerRepository) biết cache cũ đã lỗi thời ngay sau khi ghi, thay vì đợi
    // hết TTL 5 phút (xem PublicBaseRepository.CacheScope, mặc định = tên TEntity phía đọc).
    public BannerController(
        IGenericRepository<Banner, BannerSearchRequest, BannerRequest> repo,
        IMinioService minio,
        ICacheInvalidator cacheInvalidator) : base(repo)
    {
        _minio = minio;
        _cacheInvalidator = cacheInvalidator;
    }

    [HttpPost("Add")]
    [Permission("BANNERS", "add")]
    [Consumes("multipart/form-data")]
    public override async Task<IActionResult> Add([FromForm] BannerRequest request)
    {
        var imageFile = Request.Form.Files.GetFile("imageFile");
        if (imageFile != null && imageFile.Length > 0)
            request.Url = await UploadToMinioAsync(imageFile);

        var entity = await _repo.AddAsync(request);
        _cacheInvalidator.Invalidate(nameof(PublicBannerResponse));
        return Ok(ApiResponse<Banner>.Ok(entity, Localizer["AddSuccess"]));
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("BANNERS", "edit")]
    [Consumes("multipart/form-data")]
    public override async Task<IActionResult> Update(Guid publicId, [FromForm] BannerRequest request)
    {
        var imageFile = Request.Form.Files.GetFile("imageFile");
        if (imageFile != null && imageFile.Length > 0)
        {
            var existing = await _repo.GetByPublicIdAsync(publicId);
            var oldUrl   = existing?.Url;

            request.Url = await UploadToMinioAsync(imageFile);

            if (IsMinioUrl(oldUrl))
                _ = _minio.DeleteAsync(oldUrl!);
        }

        try
        {
            var entity = await _repo.UpdateAsync(publicId, request);
            _cacheInvalidator.Invalidate(nameof(PublicBannerResponse));
            return Ok(ApiResponse<Banner>.Ok(entity, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<Banner>.Fail(Localizer["NotFound"], 404));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<Banner>.Fail(ex.Message));
        }
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("BANNERS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        var result = await base.Delete(publicId);
        _cacheInvalidator.Invalidate(nameof(PublicBannerResponse));
        return result;
    }

    [HttpPut("ChangeStatus")]
    [Permission("BANNERS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
    {
        var result = await base.ChangeStatus(request);
        _cacheInvalidator.Invalidate(nameof(PublicBannerResponse));
        return result;
    }

    [HttpGet("{id:long}")]
    [Permission("BANNERS", "view")]
    public override async Task<IActionResult> GetById(long id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound(ApiResponse<Banner>.Fail(Localizer["NotFound"], 404));
        entity.Url = ResolveImageUrl(entity.Url);
        return Ok(ApiResponse<Banner>.Ok(entity));
    }

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("BANNERS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId)
    {
        var entity = await _repo.GetByPublicIdAsync(publicId);
        if (entity == null) return NotFound(ApiResponse<Banner>.Fail(Localizer["NotFound"], 404));
        entity.Url = ResolveImageUrl(entity.Url);
        return Ok(ApiResponse<Banner>.Ok(entity));
    }

    [HttpPost("Search")]
    [Permission("BANNERS", "view")]
    public override async Task<IActionResult> Search([FromBody] BannerSearchRequest request)
    {
        var result = await _repo.SearchAsync(request);
        foreach (var item in result.Items) item.Url = ResolveImageUrl(item.Url);
        return Ok(ApiResponse<PagedResult<Banner>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("BANNERS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BannerSearchRequest request)
    {
        var items = await _repo.SearchAllAsync(request);
        foreach (var item in items) item.Url = ResolveImageUrl(item.Url);
        return Ok(ApiResponse<List<Banner>>.Ok(items));
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
