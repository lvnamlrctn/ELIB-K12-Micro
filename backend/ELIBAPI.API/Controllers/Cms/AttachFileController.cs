using ELIBAPI.API.Filters;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

/// <summary>File đính kèm tin tức (cms.AttachFile) — CRUD chuẩn + tải lên kho riêng tư, tải về qua API, đồng bộ file cũ
/// (port ELIB-LRC 10-04, xem <see cref="IAttachFileService"/>).</summary>
[Route("api/Cms/[controller]")]
public class AttachFileController(IAttachFileRepository repo, IAttachFileService attachFiles)
    : GenericController<AttachFile, AttachFileSearchRequest, AttachFileRequest>(repo)
{
    private long? ScopeTenantId() => IsPrivilegedRole() ? null : GetTenantId();

    [HttpPost("Add")]    [Permission("NEWS_MANAGE", "add")]
    public override async Task<IActionResult> Add([FromBody] AttachFileRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("NEWS_MANAGE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AttachFileRequest request) => await base.Update(publicId, request);

    /// <summary>Xoá mềm dòng + xoá object trong kho (file cũ "Upload/..." chỉ xoá dòng).</summary>
    [HttpDelete("Delete/{publicId:guid}")] [Permission("NEWS_MANAGE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) =>
        this.FromServiceResult(await attachFiles.DeleteAsync(publicId), _ => (object?)null);

    [HttpPut("ChangeStatus")]
    [Permission("NEWS_MANAGE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> Search([FromBody] AttachFileSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AttachFileSearchRequest request) => await base.SearchAll(request);

    // ── Tải lên / tải về / đổi tên ───────────────────────────────────────────

    [HttpPost("Upload")]
    [Permission("NEWS_MANAGE", "add")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] Guid newsPublicId, [FromForm] string? name = null)
    {
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<object>.Fail("Không tìm thấy file"));
        await using var stream = file.OpenReadStream();
        var result = await attachFiles.UploadAsync(stream, file.FileName, file.ContentType, file.Length, newsPublicId, name,
            ScopeTenantId(), GetCurrentUserId());
        return this.FromServiceResult(result, f => f);
    }

    /// <summary>Trả nội dung file qua API (không lộ URL kho lưu trữ); PDF/ảnh trình duyệt tự mở.</summary>
    [HttpGet("Download/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "view")]
    public async Task<IActionResult> Download(Guid publicId)
    {
        var result = await attachFiles.OpenAsync(publicId);
        return result.IsOk ? File(result.Value!.Content, result.Value.ContentType, result.Value.DownloadName)
                           : this.FromServiceResult(result, x => x);
    }

    [HttpPut("Rename/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "edit")]
    public async Task<IActionResult> Rename(Guid publicId, [FromBody] AttachFileRenameRequest request) =>
        this.FromServiceResult(await attachFiles.RenameAsync(publicId, request.Name ?? ""), _ => (object?)null);

    /// <summary>Chép file cũ (Url "Upload/...") của đơn vị từ thư mục <c>PathSettings:PathAttachment</c> lên kho — chỉ chạy khi admin bấm.</summary>
    [HttpPost("SyncFiles")]
    [Permission("NEWS_MANAGE", "edit")]
    public async Task<IActionResult> SyncFiles() =>
        this.FromServiceResult(await attachFiles.SyncLegacyFilesAsync(ScopeTenantId()),
            r => new { synced = r.Synced, failed = r.Failed, total = r.Total, errors = r.Errors });
}

public class AttachFileRenameRequest
{
    public string? Name { get; set; }
}

