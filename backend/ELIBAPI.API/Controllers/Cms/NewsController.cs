using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class NewsController : GenericController<News, NewsSearchRequest, NewsRequest>
{
    private readonly INewsRepository _newsRepo;
    private readonly IMinioService   _minio;
    private readonly IConfiguration  _config;

    public NewsController(INewsRepository repo, IMinioService minio, IConfiguration config) : base(repo)
    {
        _newsRepo = repo;
        _minio    = minio;
        _config   = config;
    }

    [HttpPost("UploadImage")]
    [Permission("NEWS_MANAGE", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidFile"], 400));

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var header = ms.ToArray()[..Math.Min(12, (int)ms.Length)];
        var ext    = DetectImageExt(header);
        if (ext == null)
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidFile"], 400));

        ms.Seek(0, SeekOrigin.Begin);
        var url = await _minio.UploadAsync(ms, $"img.{ext}", file.ContentType);
        return Ok(ApiResponse<object>.Ok(new { path = url }, Localizer["UploadSuccess"]));
    }

    [HttpPost("Add")]
    [Permission("NEWS_MANAGE", "add")]
    public override async Task<IActionResult> Add([FromBody] NewsRequest request)
    {
        request.Images = await UploadIfBase64Async(request.Images);
        request.Thumb  = await UploadIfBase64Async(request.Thumb);
        return await base.Add(request);
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] NewsRequest request)
    {
        var existing  = await _repo.GetByPublicIdAsync(publicId);
        var oldImages = existing?.Images;
        var oldThumb  = existing?.Thumb;

        request.Images = await UploadIfBase64Async(request.Images);
        request.Thumb  = await UploadIfBase64Async(request.Thumb);

        if (request.Images != oldImages && IsMinioUrl(oldImages))
            _ = _minio.DeleteAsync(oldImages!);
        if (request.Thumb != oldThumb && IsMinioUrl(oldThumb))
            _ = _minio.DeleteAsync(oldThumb!);

        return await base.Update(publicId, request);
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("NEWS_MANAGE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> GetById(long id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound(ApiResponse<News>.Fail(Localizer["NotFound"], 404));
        ResolveImages(entity);
        return Ok(ApiResponse<News>.Ok(entity));
    }

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId)
    {
        var entity = await _repo.GetByPublicIdAsync(publicId);
        if (entity == null) return NotFound(ApiResponse<News>.Fail(Localizer["NotFound"], 404));
        ResolveImages(entity);
        return Ok(ApiResponse<News>.Ok(entity));
    }

    [HttpPost("Search")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> Search([FromBody] NewsSearchRequest request)
    {
        var result = await _repo.SearchAsync(request);
        foreach (var item in result.Items) ResolveImages(item);
        return Ok(ApiResponse<PagedResult<News>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] NewsSearchRequest request)
    {
        var items = await _repo.SearchAllAsync(request);
        foreach (var item in items) ResolveImages(item);
        return Ok(ApiResponse<List<News>>.Ok(items));
    }

    private void ResolveImages(News entity)
    {
        entity.Images = ResolveImageUrl(entity.Images);
        entity.Thumb  = ResolveImageUrl(entity.Thumb);
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";

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

    [HttpPost("SyncImages")]
    [Permission("NEWS_MANAGE", "edit")]
    public async Task<IActionResult> SyncImages()
    {
        var legacyBase = (_config["LegacyFileServer:BaseUrl"] ?? "http://103.97.134.58:8089").TrimEnd('/');
        var items = await _newsRepo.GetNewsWithOldImagesAsync(_minio.PublicBaseUrl);
        int synced = 0, failed = 0;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        foreach (var item in items)
        {
            string? newImages = null, newThumb = null;
            try
            {
                if (item.Images != null && !IsMinioUrl(item.Images))
                    newImages = await DownloadAndUploadAsync(http, ToFullUrl(item.Images, legacyBase));

                if (item.Thumb != null && !IsMinioUrl(item.Thumb))
                    newThumb = await DownloadAndUploadAsync(http, ToFullUrl(item.Thumb, legacyBase));

                if (newImages != null || newThumb != null)
                {
                    var updated = await _newsRepo.UpdateNewsImagesAsync(item.Id, newImages, newThumb);
                    if (updated) synced++;
                    else failed++;
                }
            }
            catch { failed++; }
        }

        return Ok(ApiResponse<object>.Ok(
            new { synced, failed, total = items.Count },
            $"Đã sync {synced}/{items.Count} bản ghi lên MinIO"));
    }

    private static string ToFullUrl(string url, string legacyBase) =>
        url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{legacyBase}/{url.TrimStart('/')}";

    private async Task<string?> DownloadAndUploadAsync(HttpClient http, string url)
    {
        using var resp = await http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var ext = Path.GetExtension(url.Split('?')[0]).TrimStart('.');
        if (string.IsNullOrEmpty(ext)) ext = "jpg";
        await using var stream = await resp.Content.ReadAsStreamAsync();
        return await _minio.UploadAsync(stream, $"img.{ext}", contentType);
    }

    private bool IsMinioUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        (url.StartsWith(_minio.PublicBaseUrl, StringComparison.OrdinalIgnoreCase)
         || (url.Length >= 8 && char.IsDigit(url[0]) && url[4] == '/'));

    private static string? DetectImageExt(byte[] h) =>
        h.Length >= 3  && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF ? "jpg" :
        h.Length >= 8  && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 ? "png" :
        h.Length >= 4  && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 ? "gif" :
        h.Length >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                       && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50 ? "webp" : null;
}
