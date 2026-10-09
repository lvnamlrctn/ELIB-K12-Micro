using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.API.Controllers.Dbo;

public sealed class AdminTaskControlRequest
{
    public string Reason { get; set; } = "";
    public string RequestId { get; set; } = "";
}

/// <summary>Trung tâm giám sát tác vụ AdminTask toàn hệ thống (Đợt 13 — port từ ELIB-LRC
/// `AdminTaskMonitorController`), route riêng tách khỏi <see cref="AdminTaskController"/> (Đợt 10, chỉ
/// thao tác tác vụ của chính actor) để không mở rộng phạm vi API cá nhân hiện có. Đọc/can thiệp tác vụ
/// của MỌI actor cùng tenant (đặc quyền: mọi tenant) — kiểm tra quyền module ADMIN_TASK_MONITOR (xem)/
/// ADMIN_TASK_CONTROL (sửa), chưa cấp mặc định cho ai ngoài admin/admin_pdp. Tắt hoàn toàn (404) khi
/// AdminTasks:Monitoring:Enabled chưa bật.</summary>
[Route("api/Dbo/AdminTask/monitor")]
public sealed class AdminTaskMonitorController(AdminTaskMonitoringService monitor, AdminTaskControlService control, IConfiguration config) : BaseApiController
{
    private bool Enabled => config.GetValue<bool>("AdminTasks:Monitoring:Enabled");

    [HttpGet]
    [Permission("ADMIN_TASK_MONITOR", "view")]
    public async Task<IActionResult> List(long? actorId, string? kind, string? state, DateTime? from, DateTime? to,
        string? cursor, int limit = 25, CancellationToken ct = default)
    {
        if (!Enabled) return NotFound();
        try { return Ok(ApiResponse<object>.Ok(await monitor.List(GetTenantId(), actorId, kind, state, from, to, cursor, limit, ct))); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message, 400)); }
    }

    [HttpGet("summary")]
    [Permission("ADMIN_TASK_MONITOR", "view")]
    public async Task<IActionResult> Summary(long? actorId, string? kind, DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        return Ok(ApiResponse<object>.Ok(await monitor.Summary(GetTenantId(), actorId, kind, from, to, ct)));
    }

    [HttpGet("{id:guid}")]
    [Permission("ADMIN_TASK_MONITOR", "view")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var detail = await monitor.Detail(GetTenantId(), id, ct);
        return detail == null ? NotFound(ApiResponse<object>.Fail("Không tìm thấy tác vụ.", 404)) : Ok(ApiResponse<object>.Ok(detail));
    }

    [HttpPost("{id:guid}/pause")]
    [Permission("ADMIN_TASK_CONTROL", "edit")]
    public async Task<IActionResult> Pause(Guid id, AdminTaskControlRequest body, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        try { return Ok(ApiResponse<object>.Ok(await control.Pause(GetCurrentUserId(), GetTenantId(), id, body.Reason, body.RequestId, ct))); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse<object>.Fail(ex.Message, 409)); }
    }

    [HttpPost("{id:guid}/resume")]
    [Permission("ADMIN_TASK_CONTROL", "edit")]
    public async Task<IActionResult> Resume(Guid id, AdminTaskControlRequest body, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        try { return Ok(ApiResponse<object>.Ok(await control.Resume(GetCurrentUserId(), GetTenantId(), id, body.Reason, body.RequestId, ct))); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Conflict(ApiResponse<object>.Fail("Người tạo tác vụ không còn quyền thực hiện, hoặc bạn không có quyền can thiệp tác vụ này; không thể tiếp tục.", 409)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse<object>.Fail(ex.Message, 409)); }
    }
}
