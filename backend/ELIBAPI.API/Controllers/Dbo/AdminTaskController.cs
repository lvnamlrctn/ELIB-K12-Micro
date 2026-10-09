using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Dbo;

/// <summary>Điều khiển tác vụ nền của CHÍNH người gọi (Đợt 10 — "tác vụ của tôi"). Giám sát tác vụ của
/// người khác (AdminTaskMonitorController) và dọn payload/result cũ (AdminTaskRetentionController) chưa
/// làm ở đợt này — xem roadmap Đợt 13.
/// Quyền theo module nguồn của TỪNG kind (READERS/RE_REGISTER/INVENTORY — <see cref="AdminTaskService.Module"/>)
/// được kiểm tra bên trong <c>AdminTaskService.Authorize</c> (lúc tạo tác vụ ở <c>Enqueue</c>, và lại lúc
/// worker thực thi mỗi chunk ở <c>RunNext</c> — phòng quyền bị thu hồi giữa chừng). Các action tự phục vụ
/// dưới đây (xem/tạm dừng/tiếp tục/huỷ/xác nhận tác vụ CỦA CHÍNH MÌNH) vì vậy chỉ cần <c>[Authorize]</c>
/// (đã áp ở <see cref="BaseApiController"/>) + luôn lọc <c>ActorId == người gọi</c> — không gắn cứng
/// <c>[Permission]</c> theo 1 module cụ thể ở đây nữa (Đợt 22.5: trước đó gắn cứng READERS, chặn nhầm cán bộ
/// chỉ có quyền RE_REGISTER/INVENTORY không xem được chính tác vụ đánh mã/kiểm kê hàng loạt của họ).</summary>
[Route("api/Dbo/AdminTask")]
public class AdminTaskController(AdminTaskService service, ELIBAPIDbContext db) : BaseApiController
{
    [HttpGet("Health")]
    public IActionResult Health([FromServices] Microsoft.Extensions.Configuration.IConfiguration config)
        => Ok(ApiResponse<object>.Ok(new { enabled = config.GetValue<bool>("AdminTasks:Enabled") }));

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var tenantId = GetTenantId();
        var query = db.AdminTasks.AsNoTracking().Where(x => x.ActorId == userId);
        if (tenantId != null) query = query.Where(x => x.TenantId == tenantId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { total, items = items.Select(AdminTaskService.View) }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var task = await db.AdminTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ActorId == GetCurrentUserId());
        if (task == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tác vụ.", 404));
        return Ok(ApiResponse<object>.Ok(AdminTaskService.View(task)));
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id)
    {
        var task = await service.Pause(GetCurrentUserId(), id);
        if (task == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tác vụ hoặc không ở trạng thái có thể tạm dừng.", 404));
        return Ok(ApiResponse<object>.Ok(AdminTaskService.View(task)));
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id)
    {
        var task = await service.Resume(GetCurrentUserId(), id);
        if (task == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tác vụ hoặc không ở trạng thái có thể tiếp tục.", 404));
        return Ok(ApiResponse<object>.Ok(AdminTaskService.View(task)));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var task = await service.Cancel(GetCurrentUserId(), id);
        if (task == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tác vụ hoặc không ở trạng thái có thể huỷ.", 404));
        return Ok(ApiResponse<object>.Ok(AdminTaskService.View(task)));
    }

    [HttpPost("{id:guid}/preview-remaining")]
    public async Task<IActionResult> PreviewRemaining(Guid id)
    {
        try
        {
            var task = await service.PreviewRemaining(GetCurrentUserId(), GetTenantId(), id);
            return Accepted(ApiResponse<object>.Ok(AdminTaskService.View(task)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
        }
    }

    /// <summary>Xác nhận 1 tác vụ xem trước đã hoàn tất — chỉ cần Id, không cần gửi lại dữ liệu (token
    /// được tra từ chính tác vụ xem trước trong DB).</summary>
    /// <summary>Tải file Excel các dòng lỗi của tác vụ nhập bạn đọc (Đợt 20).</summary>
    [HttpGet("{id:guid}/import-errors")]
    public async Task<IActionResult> ImportErrors(Guid id)
    {
        var bytes = await service.ImportErrorsWorkbook(GetCurrentUserId(), id);
        if (bytes == null) return NotFound(ApiResponse<object>.Fail("Không có dữ liệu dòng lỗi cho tác vụ này.", 404));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"doc-gia-dong-loi-{id:N}.xlsx");
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var preview = await db.AdminTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ActorId == GetCurrentUserId());
        if (preview == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tác vụ.", 404));
        if (!preview.Preview || string.IsNullOrEmpty(preview.ReviewToken))
            return BadRequest(ApiResponse<object>.Fail("Tác vụ này không phải phiên xem trước hoặc chưa hoàn tất.", 400));

        try
        {
            var task = await service.Enqueue(GetCurrentUserId(), GetTenantId(),
                new AdminTaskRequest { Kind = preview.Kind, Preview = false, Token = preview.ReviewToken });
            return Accepted(ApiResponse<object>.Ok(AdminTaskService.View(task)));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
        }
    }
}
