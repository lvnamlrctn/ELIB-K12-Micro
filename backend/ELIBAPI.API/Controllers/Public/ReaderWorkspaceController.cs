using System.Security.Claims;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Public;

/// <summary>Không gian nghiên cứu (đồng bộ đa thiết bị) + Tìm kiếm đã lưu của bạn đọc — Đợt 9, port từ
/// ELIB-LRC ReaderWorkspaceController. Công khai theo JWT bạn đọc (khác JWT nhân viên — không có claim
/// TenantId, phải tự tra Reader.TenantId mỗi request, đúng khuôn MyLibraryController).</summary>
[ApiController]
[Authorize]
[Route("api/public/ReaderWorkspace")]
public class ReaderWorkspaceController(ELIBAPIDbContext db, ReaderWorkspaceService service) : ControllerBase
{
    private async Task<(long Id, long? TenantId)?> GetCurrentReaderAsync()
    {
        if (User.FindFirstValue("Type") != "Reader") return null;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var readerPublicId)) return null;
        var reader = await db.Readers
            .Where(r => r.PublicId == readerPublicId && r.IsDelete != 2)
            .Select(r => new { r.Id, r.TenantId })
            .FirstOrDefaultAsync();
        return reader == null ? null : (reader.Id, reader.TenantId);
    }

    // ── Không gian nghiên cứu ────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));
        return Ok(ApiResponse<object>.Ok(await service.GetWorkspaceAsync(reader.Value.Id)));
    }

    [HttpPut]
    [RequestSizeLimit(1_100_000)]
    public async Task<IActionResult> Save([FromBody] SaveWorkspaceRequest request)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var (accepted, error) = await service.SaveWorkspaceAsync(reader.Value.Id, reader.Value.TenantId, request);
        if (error != null) return BadRequest(ApiResponse<object>.Fail(error, 400));
        if (accepted == null) return Conflict(ApiResponse<object>.Fail("Sổ tay đã được sửa trên thiết bị khác. Hãy tải bản mới trước khi lưu.", 409));
        return Ok(ApiResponse<object>.Ok(accepted));
    }

    // ── Tìm kiếm đã lưu ──────────────────────────────────────────────────────

    [HttpGet("Searches")]
    public async Task<IActionResult> ListSearches()
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));
        return Ok(ApiResponse<object>.Ok(await service.ListSearchesAsync(reader.Value.Id)));
    }

    [HttpPost("Searches")]
    [RequestSizeLimit(12000)]
    public async Task<IActionResult> AddSearch([FromBody] SaveReaderSearchRequest request)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var (id, error) = await service.AddSearchAsync(reader.Value.Id, reader.Value.TenantId, request);
        if (error != null) return BadRequest(ApiResponse<object>.Fail(error, 400));
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("Searches/{searchId:guid}")]
    public async Task<IActionResult> DeleteSearch(Guid searchId)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));
        var ok = await service.DeleteSearchAsync(reader.Value.Id, searchId);
        return ok ? Ok(ApiResponse<object>.Ok(null!)) : NotFound(ApiResponse<object>.Fail("Không tìm thấy tìm kiếm đã lưu.", 404));
    }

    [HttpPut("Searches/{searchId:guid}/Alerts")]
    public async Task<IActionResult> ToggleAlerts(Guid searchId, [FromBody] bool enabled)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));
        var ok = await service.ToggleAlertsAsync(reader.Value.Id, searchId, enabled);
        return ok ? Ok(ApiResponse<object>.Ok(null!)) : NotFound(ApiResponse<object>.Fail("Không tìm thấy tìm kiếm đã lưu.", 404));
    }

    [HttpPut("Searches/{searchId:guid}/Read")]
    public async Task<IActionResult> MarkRead(Guid searchId)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));
        var ok = await service.MarkReadAsync(reader.Value.Id, searchId);
        return ok ? Ok(ApiResponse<object>.Ok(null!)) : NotFound(ApiResponse<object>.Fail("Không tìm thấy tìm kiếm đã lưu.", 404));
    }
}
