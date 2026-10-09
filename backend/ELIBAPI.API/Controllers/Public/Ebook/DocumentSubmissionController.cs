using System.Security.Claims;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers;

/// <summary>
/// Tài liệu nội sinh do bạn đọc nộp qua OPAC, chờ thủ thư kiểm duyệt. File lưu vào MinIO bucket
/// private, cùng cách EbookFileController.Upload. GET api/public/DocumentSubmission/MyList · POST /Add
/// </summary>
[ApiController]
[Route("api/public/[controller]")]
[Authorize]
public class DocumentSubmissionController(ELIBAPIDbContext db, IMinioService minio) : ControllerBase
{
    private static readonly string[] ValidDocTypes = ["luan_van", "luan_an", "bai_bao", "de_tai"];

    private async Task<long?> GetCurrentReaderIdAsync()
    {
        if (User.FindFirstValue("Type") != "Reader") return null;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var readerPublicId)) return null;
        return await db.Readers
            .Where(r => r.PublicId == readerPublicId && r.IsDelete != 2)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync();
    }

    [HttpGet("MyList")]
    public async Task<IActionResult> MyList()
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var items = await db.DocumentSubmissions
            .Where(s => s.ReaderId == readerId && s.IsDelete != 2)
            .OrderByDescending(s => s.CreatedDate)
            .Select(s => new
            {
                id          = s.Id.ToString(),
                title       = s.Title ?? "",
                author      = s.Author ?? "",
                docType     = s.DocType ?? "",
                fileName    = s.FileName,
                createdDate = s.CreatedDate,
                status      = s.Status ?? ""
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    [HttpPost("Add")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Add(
        [FromForm] string title,
        [FromForm] string author,
        [FromForm] string docType,
        [FromForm] string? @abstract,
        IFormFile? file)
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author))
            return BadRequest(ApiResponse<object>.Fail("Vui lòng nhập đầy đủ tên tài liệu và tác giả."));
        if (!ValidDocTypes.Contains(docType))
            return BadRequest(ApiResponse<object>.Fail("Loại tài liệu không hợp lệ."));
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn tệp đính kèm."));

        string objectName;
        await using (var stream = file.OpenReadStream())
            objectName = await minio.UploadPrivateAsync(stream, file.FileName, file.ContentType);

        var readerTenantId = await db.Readers
            .Where(r => r.Id == readerId.Value)
            .Select(r => r.TenantId)
            .FirstOrDefaultAsync();

        var submission = new DocumentSubmission
        {
            ReaderId    = readerId.Value,
            Title       = title.Trim(),
            Author      = author.Trim(),
            DocType     = docType,
            Abstract    = @abstract,
            FileUrl     = objectName,
            FileName    = file.FileName,
            Status      = "Chờ duyệt",
            CreatedDate = DateTime.Now,
            TenantId    = readerTenantId,
            PublicId    = Guid.NewGuid()
        };
        db.DocumentSubmissions.Add(submission);
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { id = submission.Id.ToString() }, "Đã gửi tài liệu thành công."));
    }
}
