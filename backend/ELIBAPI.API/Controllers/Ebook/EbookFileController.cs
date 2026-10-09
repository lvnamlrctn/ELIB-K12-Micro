using System.Security.Claims;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Jobs;
using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookFileController : GenericController<EbookFile, EbookFileSearchRequest, EbookFileRequest>
{
    private readonly IEbookFileRepository _ebookFileRepo;
    private readonly IMinioService        _minio;
    private readonly IConfiguration       _config;
    private readonly IEbookItemLoanRepository _loanRepo;

    private readonly ELIBAPIDbContext _dbContext;
    private readonly ILogger<EbookFileController> _logger;

    public EbookFileController(
        IEbookFileRepository repo,
        IMinioService minio,
        IConfiguration config,
        IEbookItemLoanRepository loanRepo,
        ELIBAPIDbContext dbContext,
        ILogger<EbookFileController> logger) : base(repo)
    {
        _ebookFileRepo = repo;
        _minio         = minio;
        _config        = config;
        _loanRepo      = loanRepo;
        _dbContext     = dbContext;
        _logger        = logger;
    }

    // ── Bạn đọc hiện tại — Reader đăng nhập qua PublicReaderController có NameIdentifier = Reader.PublicId
    // (Guid), khác nhân viên (Users.Id, long) mà GetCurrentUserId() giả định. Nhân viên/admin xem tài liệu
    // không bị áp giới hạn mượn (trả về null → bỏ qua toàn bộ luồng Loan).
    private async Task<long?> GetCurrentReaderIdAsync()
    {
        if (User.FindFirstValue("Type") != "Reader") return null;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var readerPublicId)) return null;
        return await _dbContext.Readers
            .Where(r => r.PublicId == readerPublicId && r.IsDelete != 2)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync();
    }

    // ── CRUD chuẩn ────────────────────────────────────────────────────────────

    [HttpPost("Add")]
    [Permission("DIGITAL_DOC", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookFileRequest request)
    {
        var result = await base.Add(request);

        var fileExt = (request.FileExt ?? "").ToLower().TrimStart('.');
        if ((fileExt == "pdf" || request.FileType == "application/pdf") && request.EbookId.HasValue)
        {
            var itemPublicId = await _dbContext.EbookItems
                .Where(e => e.Id == request.EbookId.Value && e.IsDelete != 2)
                .Select(e => (Guid?)e.PublicId)
                .FirstOrDefaultAsync();
            if (itemPublicId.HasValue)
                BackgroundJobs.TryEnqueue<EbookIndexingJob>(HttpContext.RequestServices, x => x.RunAsync(itemPublicId.Value), _logger);
            BackgroundJobs.TryEnqueue<DigitalUnifiedIndexingJob>(HttpContext.RequestServices, x => x.RunAsync(request.EbookId.Value), _logger);
        }

        return result;
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookFileRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        var file = await _repo.GetByPublicIdAsync(publicId);
        var result = await base.Delete(publicId);
        if (result is OkObjectResult && !string.IsNullOrEmpty(file?.Url))
            await _minio.DeletePrivateAsync(file.Url);
        return result;
    }

    [HttpPut("ChangeStatus")]
    [Permission("DIGITAL_DOC", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookFileSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookFileSearchRequest request) => await base.SearchAll(request);

    // ── Bulk sync: download old HTTP URLs → re-upload MinIO private ──────────

    [HttpPost("SyncFiles")]
    [Permission("DIGITAL_DOC", "edit")]
    public async Task<IActionResult> SyncFiles()
    {
        var basePath = _config["PathSettings:PathEbook"] ?? "";
        var files = await _ebookFileRepo.GetFilesWithOldUrlsAsync();
        int synced = 0, failed = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            var fullPath = Path.Combine(basePath, file.Url!.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
            {
                failed++;
                errors.Add($"Id={file.Id}: File not found: {fullPath}");
                continue;
            }

            try
            {
                var ext = !string.IsNullOrEmpty(file.FileExt)
                    ? file.FileExt
                    : Path.GetExtension(file.Url).TrimStart('.');
                if (string.IsNullOrEmpty(ext)) ext = "bin";

                var contentType = file.FileType ?? "application/octet-stream";

                await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var objectName = await _minio.UploadPrivateAsync(stream, $"file.{ext}", contentType);
                var updated = await _ebookFileRepo.UpdateFileUrlAsync(file.Id, objectName);
                if (updated)
                    synced++;
                else
                {
                    failed++;
                    errors.Add($"Id={file.Id}: DB update failed after MinIO upload");
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Id={file.Id} {ex.GetType().Name}: {ex.Message} | Path={fullPath}");
            }
        }

        return Ok(ApiResponse<object>.Ok(
            new { synced, failed, total = files.Count, errors },
            $"Đã sync {synced}/{files.Count} file lên MinIO"));
    }

    // ── Upload file lên MinIO private ─────────────────────────────────────────

    [HttpPost("Upload")]
    [Permission("DIGITAL_DOC", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] long?     ebookId     = null,
        [FromForm] string?   type        = null,
        [FromForm] string?   description = null,
        [FromForm] int?      sortOrder   = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"]));

        string objectName;
        await using (var stream = file.OpenReadStream())
            objectName = await _minio.UploadPrivateAsync(stream, file.FileName, file.ContentType);

        var request = new EbookFileRequest
        {
            EbookId     = ebookId,
            Url         = objectName,
            FileType    = file.ContentType,
            FileExt     = Path.GetExtension(file.FileName).TrimStart('.').ToLower(),
            FileSize    = Math.Round(file.Length / 1024.0, 2),
            Type        = type,
            Description = description,
            SortOrder   = sortOrder,
            CreatedDate = DateTime.Now,
        };

        var entity = await _repo.AddAsync(request);

        // Nếu file PDF → enqueue Hangfire job để index chunks vào Elasticsearch
        var fileExt = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
        if ((fileExt == "pdf" || file.ContentType == "application/pdf") && ebookId.HasValue)
        {
            var itemPublicId = await _dbContext.EbookItems
                .Where(e => e.Id == ebookId.Value && e.IsDelete != 2)
                .Select(e => (Guid?)e.PublicId)
                .FirstOrDefaultAsync();
            if (itemPublicId.HasValue)
                BackgroundJobs.TryEnqueue<EbookIndexingJob>(HttpContext.RequestServices, x => x.RunAsync(itemPublicId.Value), _logger);
            BackgroundJobs.TryEnqueue<DigitalUnifiedIndexingJob>(HttpContext.RequestServices, x => x.RunAsync(ebookId.Value), _logger);
        }

        return Ok(ApiResponse<EbookFile>.Ok(entity, "Upload thành công"));
    }

    // ── Tạo token truy cập có thời hạn ───────────────────────────────────────

    [HttpPost("GetToken")]
    public async Task<IActionResult> GetToken([FromBody] EbookFileGetTokenRequest request)
    {
        var file = await _repo.GetByPublicIdAsync(request.PublicId);
        if (file == null)
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));

        var userId   = GetCurrentUserId();
        var fullName = User.FindFirstValue("FullName") ?? User.FindFirstValue(ClaimTypes.Name) ?? "";
        var secret   = EbookFileTokenHelper.ResolveSecret(_config["EbookFileSettings:TokenSecret"], _config["Jwt:Key"]);
        var expiry   = int.TryParse(_config["EbookFileSettings:TokenExpiryMinutes"], out var m) ? m : 60;

        var readerId = await GetCurrentReaderIdAsync();
        if (readerId.HasValue && file.EbookId.HasValue)
        {
            var (ok, error, statusCode, _) = await _loanRepo.CheckoutOrResumeAsync(file.EbookId.Value, readerId.Value);
            if (!ok)
                return StatusCode(statusCode, ApiResponse<object>.Fail(error ?? "Không thể mượn tài liệu.", statusCode));
        }

        var payload   = new EbookFileTokenPayload(file.PublicId, userId, fullName, 0, readerId);
        var token     = EbookFileTokenHelper.Generate(payload, secret, expiry);
        var expiresAt = DateTime.Now.AddMinutes(expiry);

        return Ok(ApiResponse<object>.Ok(new { token, expiresAt }));
    }

    // ── Stream file (PDF: có watermark; khác: redirect presigned URL) ─────────

    [HttpGet("View/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> View(string token)
    {
        var secret  = EbookFileTokenHelper.ResolveSecret(_config["EbookFileSettings:TokenSecret"], _config["Jwt:Key"]);
        var payload = EbookFileTokenHelper.Validate(token, secret);
        if (payload == null)
            return Unauthorized(ApiResponse<object>.Fail("Token không hợp lệ hoặc đã hết hạn.", 401));

        var file = await _repo.GetByPublicIdAsync(payload.FilePublicId);
        if (file == null || file.IsDelete == 2)
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));

        if (payload.ReaderId.HasValue && file.EbookId.HasValue)
        {
            var (valid, error, statusCode) = await _loanRepo.ValidateAccessAsync(file.EbookId.Value, payload.ReaderId.Value);
            if (!valid)
                return StatusCode(statusCode, ApiResponse<object>.Fail(error ?? "Không có quyền truy cập.", statusCode));
        }

        var objectName    = file.Url ?? "";
        // Dữ liệu cũ lưu FileExt dạng ".pdf"/".PDF" → chuẩn hoá, nếu không PDF bị coi là "khác" và trả link tải không đóng dấu
        var ext           = (string.IsNullOrWhiteSpace(file.FileExt) ? Path.GetExtension(objectName) : file.FileExt)
                                .Trim().TrimStart('.').ToLowerInvariant();
        var watermarkText = $"{payload.UserName}  |  {DateTime.Now:dd/MM/yyyy HH:mm}";

        try
        {
            if (ext == "pdf")
            {
                var (pdfStream, _) = await _minio.GetObjectStreamAsync(objectName);
                await using (pdfStream)
                {
                    var watermarked = PdfWatermarkHelper.Watermark(pdfStream, watermarkText);
                    return File(watermarked, "application/pdf");
                }
            }

            var presigned = await _minio.GetPresignedUrlAsync(objectName, 900);
            return Redirect(presigned);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EbookFile/View: không đọc được object {Object}", objectName);
            return NotFound(ApiResponse<object>.Fail("File không tồn tại trong kho lưu trữ.", 404));
        }
    }
}
