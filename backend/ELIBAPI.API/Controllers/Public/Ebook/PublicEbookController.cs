using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Security.Claims;
using PdfSharp.Pdf.IO;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicEbookController(
    IPublicEbookRepository repo,
    IStringLocalizer<SharedResource> loc,
    IConfiguration config,
    IMinioService minio) : PublicBaseController
{
    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicEbookSearchRequest r)
    {
        var paged = await repo.SearchAsync(r);
        foreach (var (item, i) in paged.Items.Select((x, i) => (x, i)))
        {
            item.Index  = i + 1;
            item.Images = ResolveImageUrl(item.Images);
        }
        return Ok(ApiResponse<PagedResult<PublicEbookResponse>>.Ok(paged));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicEbookSearchRequest r)
    {
        var items = await repo.SearchAllAsync(r);
        foreach (var (item, i) in items.Select((x, i) => (x, i)))
        {
            item.Index  = i + 1;
            item.Images = ResolveImageUrl(item.Images);
        }
        return Ok(ApiResponse<List<PublicEbookResponse>>.Ok(items));
    }

    [HttpGet("GetLastedEbook")]
    public async Task<IActionResult> GetLastedEbook([FromQuery] PublicEbookLatestRequest r)
    {
        var items = await repo.GetLatestEbooksAsync(r);
        foreach (var (item, i) in items.Select((x, i) => (x, i)))
        {
            item.Index  = i + 1;
            item.Images = ResolveImageUrl(item.Images);
        }
        return Ok(ApiResponse<List<PublicEbookResponse>>.Ok(items));
    }

    [HttpGet("{EbookId}")]
    public async Task<IActionResult> GetEbookDetail(Guid EbookId)
    {
        var ebook = await repo.GetEbookByPublicIdAsync(EbookId);
        if (ebook == null) return NotFound(ApiResponse<PublicEbookResponse>.Fail(loc["NotFound"]));
        ebook.Images = ResolveImageUrl(ebook.Images);
        return Ok(ApiResponse<PublicEbookResponse>.Ok(ebook));
    }

    [HttpGet("{EbookId}/Files")]
    public async Task<IActionResult> GetFiles(Guid EbookId)
    {
        var files = await repo.GetEbookFilesByEbookPublicIdAsync(EbookId);
        return Ok(ApiResponse<List<PublicEbookFileResponse>>.Ok(files));
    }

    [HttpGet("CheckFilePermission/{ebookPublicId}")]
    public async Task<IActionResult> CheckFilePermission(Guid ebookPublicId)
    {
        Guid? readerPublicId = null;
        if (User.Identity!.IsAuthenticated && User.FindFirstValue("Type") == "Reader"
            && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var rg))
            readerPublicId = rg;

        var result = await repo.CheckFilePermissionAsync(ebookPublicId, readerPublicId);
        return Ok(ApiResponse<FilePermissionResult>.Ok(result));
    }

    [HttpGet("GetFilePdf/{publicId}")]
    public async Task<IActionResult> GetFilePdf(Guid publicId)
    {
        var (file, book) = await repo.GetEbookFileWithItemAsync(publicId);
        if (file == null)
            return NotFound(ApiResponse<string>.Fail("EbookFile không tìm thấy hoặc đã bị xoá"));
        if (book == null)
            return NotFound(ApiResponse<string>.Fail("EbookItem không tìm thấy hoặc đã bị xoá"));

        if ((book.Free == 1 || book.Free == null) && !User.Identity!.IsAuthenticated)
            return Unauthorized(ApiResponse<string>.Fail(loc["Unauthorized"]));

        string filePath = Path.Combine(config["PathSettings:PathEbook"] ?? "", file.Url ?? "");

        if (!System.IO.File.Exists(filePath))
            return NotFound(ApiResponse<string>.Fail($"File vật lý không tồn tại: {filePath}"));

        Guid? readerPublicId = null;
        if (User.Identity!.IsAuthenticated && User.FindFirstValue("Type") == "Reader"
            && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var rg))
        {
            readerPublicId = rg;
        }

        int? pageCount = null;
        try
        {
            using var doc = PdfReader.Open(filePath, PdfDocumentOpenMode.InformationOnly);
            pageCount = doc.PageCount;
        }
        catch { }

        long fileSize = new FileInfo(filePath).Length;

        var ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? HttpContext.Connection.RemoteIpAddress?.ToString();

        var rangeHeader = Request.Headers.Range.ToString().Trim();
        if (string.IsNullOrEmpty(rangeHeader) || rangeHeader == "bytes=0-0")
        {
            await repo.IncrementTotalViewAsync(book.Id);
            await repo.AddEbookLogAsync(book.Id, readerPublicId, ip, pageCount, fileSize, book.TenantId);
        }

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(fileStream, "application/pdf", enableRangeProcessing: true);
    }

    [HttpGet("GetFileMinio/{publicId}")]
    public async Task<IActionResult> GetFileMinio(Guid publicId)
    {
        var (file, book) = await repo.GetEbookFileWithItemAsync(publicId);
        if (file == null)
            return NotFound(ApiResponse<string>.Fail("EbookFile không tìm thấy hoặc đã bị xoá"));
        if (book == null)
            return NotFound(ApiResponse<string>.Fail("EbookItem không tìm thấy hoặc đã bị xoá"));

        if ((book.Free == 1 || book.Free == null) && !User.Identity!.IsAuthenticated)
            return Unauthorized(ApiResponse<string>.Fail(loc["Unauthorized"]));

        var objectName = file.Url ?? "";
        if (string.IsNullOrEmpty(objectName))
            return NotFound(ApiResponse<string>.Fail("File chưa được upload lên MinIO"));

        Guid? readerPublicId = null;
        if (User.Identity!.IsAuthenticated && User.FindFirstValue("Type") == "Reader"
            && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var rg))
        {
            readerPublicId = rg;
        }

        var ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? HttpContext.Connection.RemoteIpAddress?.ToString();

        Stream minioStream;
        string minioContentType;
        try
        {
            (minioStream, minioContentType) = await minio.GetObjectStreamAsync(objectName);
        }
        catch (Exception ex)
        {
            return NotFound(ApiResponse<string>.Fail($"Không thể tải file từ MinIO: {ex.Message}"));
        }

        var ms = new MemoryStream();
        await using (minioStream) await minioStream.CopyToAsync(ms);
        ms.Position = 0;

        long fileSize = ms.Length;

        int? pageCount = null;
        var ext = (file.FileExt ?? Path.GetExtension(objectName).TrimStart('.')).ToLower();
        if (ext == "pdf")
        {
            try
            {
                using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
                pageCount = doc.PageCount;
            }
            catch { }
            ms.Position = 0;
        }

        var rangeHeader = Request.Headers.Range.ToString().Trim();
        if (string.IsNullOrEmpty(rangeHeader) || rangeHeader == "bytes=0-0")
        {
            await repo.IncrementTotalViewAsync(book.Id);
            await repo.AddEbookLogAsync(book.Id, readerPublicId, ip, pageCount, fileSize, book.TenantId);
        }

        var mimeType = string.IsNullOrEmpty(minioContentType) ? "application/octet-stream" : minioContentType;
        return File(ms, mimeType, enableRangeProcessing: true);
    }

    [HttpGet("GetPageMinio/{publicId:guid}/{page:int}")]
    public async Task<IActionResult> GetPageMinio(Guid publicId, int page)
    {
        var (file, book) = await repo.GetEbookFileWithItemAsync(publicId);
        if (file == null)
            return NotFound(ApiResponse<string>.Fail("EbookFile không tìm thấy hoặc đã bị xoá"));
        if (book == null)
            return NotFound(ApiResponse<string>.Fail("EbookItem không tìm thấy hoặc đã bị xoá"));

        if ((book.Free == 1 || book.Free == null) && !User.Identity!.IsAuthenticated)
            return Unauthorized(ApiResponse<string>.Fail(loc["Unauthorized"]));

        var objectName = file.Url ?? "";
        if (string.IsNullOrEmpty(objectName))
            return NotFound(ApiResponse<string>.Fail("File chưa được upload lên MinIO"));

        var ext = (file.FileExt ?? Path.GetExtension(objectName).TrimStart('.')).ToLower();
        if (ext != "pdf")
            return BadRequest(ApiResponse<string>.Fail("Chức năng xem theo trang chỉ hỗ trợ file PDF"));

        Stream minioStream;
        try
        {
            (minioStream, _) = await minio.GetObjectStreamAsync(objectName);
        }
        catch (Exception ex)
        {
            return NotFound(ApiResponse<string>.Fail($"Không thể tải file từ MinIO: {ex.Message}"));
        }

        byte[]? pageBytes;
        await using (minioStream)
        {
            pageBytes = PdfPageExtractHelper.ExtractPage(minioStream, page);
        }

        if (pageBytes == null)
            return NotFound(ApiResponse<string>.Fail("Trang không tồn tại"));

        return File(pageBytes, "application/pdf");
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{minio.PublicBaseUrl}/{value}";
}
