using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.API.Controllers.Dbo;

public sealed class AdminTaskRetentionHoldRequest
{
    public bool Hold { get; set; }
}

/// <summary>Quản lý dung lượng và thời gian lưu lịch sử AdminTask (Đợt 13 — port từ ELIB-LRC
/// `AdminTaskRetentionController`), route riêng tách khỏi <see cref="AdminTaskController"/>. Không lọc
/// theo tenant — dọn dữ liệu đã mã hoá/hết hạn lưu, không phải xem nội dung nghiệp vụ, xem thiết kế ở kế
/// hoạch Đợt 13. Kiểm tra quyền module ADMIN_TASK_RETENTION (xem/sửa) — chưa cấp mặc định cho ai ngoài
/// admin/admin_pdp. Tắt hoàn toàn (404) khi AdminTasks:Retention:Enabled chưa bật.</summary>
/// Đợt 18: dọn dữ liệu của MỌI đơn vị → chỉ tài khoản hệ thống ([SystemAdminOnly]); admin 1 đơn vị dù được
/// cấp ADMIN_TASK_RETENTION cũng nhận 403 (trước đây xem/xoá được lịch sử của đơn vị khác).
[SystemAdminOnly]
[Route("api/Dbo/AdminTask/retention")]
public sealed class AdminTaskRetentionController(AdminTaskRetentionService retention, IConfiguration config) : BaseApiController
{
    private bool Enabled => config.GetValue<bool>("AdminTasks:Retention:Enabled");

    [HttpGet("preview")]
    [Permission("ADMIN_TASK_RETENTION", "view")]
    public async Task<IActionResult> Preview(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        return Ok(ApiResponse<object>.Ok(await retention.PreviewAsync(ct)));
    }

    [HttpGet("runs")]
    [Permission("ADMIN_TASK_RETENTION", "view")]
    public async Task<IActionResult> Runs(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        return Ok(ApiResponse<object>.Ok(await retention.RecentRunsAsync(ct)));
    }

    [HttpPost("run")]
    [Permission("ADMIN_TASK_RETENTION", "edit")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        return Ok(ApiResponse<object>.Ok(await retention.RunSweepAsync(dryRunOverride: null, ct)));
    }

    [HttpPost("hold/{id:guid}")]
    [Permission("ADMIN_TASK_RETENTION", "edit")]
    public async Task<IActionResult> Hold(Guid id, AdminTaskRetentionHoldRequest body, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var changed = await retention.SetHoldAsync(id, body.Hold, ct);
        return changed == 1 ? Ok(ApiResponse<object>.Ok(new { id, hold = body.Hold })) : NotFound(ApiResponse<object>.Fail("Không tìm thấy tác vụ.", 404));
    }
}
