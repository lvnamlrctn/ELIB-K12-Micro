using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookCollectionController : GenericController<EbookCollection, EbookCollectionSearchRequest, EbookCollectionRequest>
{
    private readonly IEbookCollectionRepository _collectionRepo;
    private readonly IPolicyDigitalByCollectionRepository _permissionRepo;
    private readonly IMinioService _minio;
    private readonly ICacheInvalidator _cacheInvalidator;

    public EbookCollectionController(IEbookCollectionRepository repo, IPolicyDigitalByCollectionRepository permissionRepo, IMinioService minio, ICacheInvalidator cacheInvalidator)
        : base(repo)
    {
        _collectionRepo = repo;
        _permissionRepo = permissionRepo;
        _minio = minio;
        _cacheInvalidator = cacheInvalidator;
    }

    [HttpPost("Add")]    [Permission("EBOOK_COLLECTION", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookCollectionRequest request)
    {
        request.Images = await UploadIfBase64Async(request.Images);
        var result = await base.Add(request);
        _cacheInvalidator.Invalidate(nameof(EbookCollection));
        return result;
    }

    [HttpPut("Update/{publicId:guid}")] [Permission("EBOOK_COLLECTION", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookCollectionRequest request)
    {
        var existing  = await _repo.GetByPublicIdAsync(publicId);
        var oldImages = existing?.Images;

        request.Images = await UploadIfBase64Async(request.Images);

        if (request.Images != null && request.Images != oldImages && IsMinioUrl(oldImages))
            _ = _minio.DeleteAsync(oldImages!);

        var result = await base.Update(publicId, request);
        _cacheInvalidator.Invalidate(nameof(EbookCollection));
        return result;
    }

    [HttpDelete("Delete/{publicId:guid}")] [Permission("EBOOK_COLLECTION", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        var result = await base.Delete(publicId);
        _cacheInvalidator.Invalidate(nameof(EbookCollection));
        return result;
    }

    [HttpPut("ChangeStatus")] [Permission("EBOOK_COLLECTION", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
    {
        var result = await base.ChangeStatus(request);
        _cacheInvalidator.Invalidate(nameof(EbookCollection));
        return result;
    }

    [HttpGet("{id:long}")] [Permission("EBOOK_COLLECTION", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("EBOOK_COLLECTION", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("EBOOK_COLLECTION", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookCollectionSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")] [Permission("EBOOK_COLLECTION", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookCollectionSearchRequest request) => await base.SearchAll(request);

    [HttpPost("GetTree")] [Permission("EBOOK_COLLECTION", "view")]
    public async Task<IActionResult> GetTree([FromBody] EbookCollectionSearchRequest request)
    {
        var tree = await _collectionRepo.GetTreeAsync(request);
        return Ok(ApiResponse<List<EbookCollectionTreeResponse>>.Ok(tree));
    }

    [HttpPut("UpdateOrder")] [Permission("EBOOK_COLLECTION", "edit")]
    public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderRequest request)
    {
        try
        {
            await _collectionRepo.UpdateOrderAsync(request.PublicId, request.NewOrder);
            _cacheInvalidator.Invalidate(nameof(EbookCollection));
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    [HttpPut("Move/{publicId:guid}")] [Permission("EBOOK_COLLECTION", "edit")]
    public async Task<IActionResult> Move(Guid publicId, [FromBody] MoveCategoryRequest request)
    {
        try
        {
            var result = await _collectionRepo.MoveAsync(publicId, request.NewParentId, request.NewOrder);
            _cacheInvalidator.Invalidate(nameof(EbookCollection));
            return Ok(ApiResponse<EbookCollection>.Ok(result, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<EbookCollection>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<EbookCollection>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    [HttpDelete("DeleteWithChildren/{publicId:guid}")] [Permission("EBOOK_COLLECTION", "delete")]
    public async Task<IActionResult> DeleteWithChildren(Guid publicId)
    {
        try
        {
            await _collectionRepo.DeleteWithChildrenAsync(publicId);
            _cacheInvalidator.Invalidate(nameof(EbookCollection));
            return Ok(ApiResponse<object>.Ok(null!, Localizer["DeleteSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    [HttpGet("GetPermissions/{publicId:guid}")]
    public async Task<IActionResult> GetPermissions(Guid publicId)
    {
        try
        {
            var result = await _permissionRepo.GetByCollectionPublicIdAsync(publicId);
            return Ok(ApiResponse<List<PolicyDigitalByCollection>>.Ok(result));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
    }

    [HttpPost("SavePermissions")] [Permission("EBOOK_COLLECTION", "edit")]
    public async Task<IActionResult> SavePermissions([FromBody] SavePermissionsRequest request)
    {
        try
        {
            await _permissionRepo.SaveByCollectionPublicIdAsync(request.CollectionPublicId, request.Permissions);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
    }

    private async Task<string?> UploadIfBase64Async(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith("data:image/")) return value;
        var comma = value.IndexOf(',');
        if (comma < 0) return value;
        var mime  = value[5..comma].Split(';')[0];
        var ext   = mime.Split('/')[1];
        var bytes = Convert.FromBase64String(value[(comma + 1)..]);
        await using var ms = new MemoryStream(bytes);
        return await _minio.UploadAsync(ms, $"img.{ext}", mime);
    }

    private bool IsMinioUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        (url.StartsWith(_minio.PublicBaseUrl, StringComparison.OrdinalIgnoreCase)
         || (url.Length >= 8 && char.IsDigit(url[0]) && url[4] == '/'));
}
