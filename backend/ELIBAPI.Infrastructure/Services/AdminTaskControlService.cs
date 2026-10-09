using System.Data;
using System.Text.Json;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Can thiệp tác vụ của NGƯỜI KHÁC (tạm dừng/tiếp tục) từ trang giám sát (Đợt 13 — port từ
/// ELIB-LRC). Ghi <see cref="AdminTaskControlEvent"/> cùng giao dịch với đổi trạng thái; chống gửi lặp
/// bằng RequestId (unique index) — gửi lại đúng RequestId trả lại kết quả cũ, không tạo sự kiện thứ hai.
/// Resume phải xác thực lại quyền nghiệp vụ HIỆN TẠI của người tạo tác vụ trước khi cho tiếp tục (không
/// dùng quyền của người vận hành thay cho quyền của người tạo). Khác LRC: nhận thêm
/// <c>callerTenantId</c> — người vận hành không đặc quyền chỉ can thiệp được tác vụ cùng tenant. Không
/// port điều kiện <c>Version == 2</c> của LRC — dấu vết phân biệt tác vụ "legacy"/"mới" mà ELIB không có
/// (mọi AdminTask của ELIB đều Version=2 từ Đợt 10).</summary>
public sealed class AdminTaskControlService(ELIBAPIDbContext db, AdminTaskCrypto crypto, AdminTaskService tasks)
{
    private static readonly JsonSerializerOptions Json = AdminTaskService.Json;

    private async Task<object?> Replay(string requestId, CancellationToken ct)
    {
        var evt = await db.AdminTaskControlEvents.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
        return evt == null ? null : new { evt.TaskId, evt.Action, evt.StateBefore, evt.StateAfter, replay = true };
    }

    public async Task<object> Pause(long operatorId, long? callerTenantId, Guid taskId, string reason, string requestId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Cần nhập lý do trước khi tạm dừng.");
        if (string.IsNullOrWhiteSpace(requestId)) throw new InvalidOperationException("Thiếu mã yêu cầu.");
        if (await Replay(requestId, ct) is { } replay) return replay;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var before = await db.AdminTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new KeyNotFoundException();
        if (callerTenantId != null && before.TenantId != callerTenantId)
            throw new UnauthorizedAccessException("Không có quyền can thiệp tác vụ của tenant khác.");

        var changed = await db.AdminTasks.Where(x => x.Id == taskId && (x.State == "Queued" || x.State == "Running"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StopRequested, true), ct);
        if (changed == 0) throw new InvalidOperationException("Chỉ tạm dừng được tác vụ đang chờ hoặc đang chạy.");

        db.AdminTaskControlEvents.Add(new AdminTaskControlEvent { TaskId = taskId, OperatorId = operatorId, OwnerId = before.ActorId,
            Action = "Pause", Reason = reason, StateBefore = before.State, StateAfter = before.State, RequestId = requestId });
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return await Replay(requestId, ct) ?? throw new InvalidOperationException("Không ghi được sự kiện can thiệp. Vui lòng thử lại."); }
        return new { taskId, action = "Pause", stateBefore = before.State, stateAfter = before.State, replay = false };
    }

    public async Task<object> Resume(long operatorId, long? callerTenantId, Guid taskId, string reason, string requestId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Cần nhập lý do trước khi tiếp tục.");
        if (string.IsNullOrWhiteSpace(requestId)) throw new InvalidOperationException("Thiếu mã yêu cầu.");
        if (await Replay(requestId, ct) is { } replay) return replay;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var task = await db.AdminTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new KeyNotFoundException();
        if (callerTenantId != null && task.TenantId != callerTenantId)
            throw new UnauthorizedAccessException("Không có quyền can thiệp tác vụ của tenant khác.");

        var request = JsonSerializer.Deserialize<AdminTaskRequest>(crypto.Unprotect(task.Payload!), Json)!;
        // Ném UnauthorizedAccessException nếu người TẠO tác vụ đã bị thu hồi quyền — resume không được
        // mượn quyền của người vận hành đang thao tác.
        await tasks.Authorize(task.ActorId, request);

        var changed = await db.AdminTasks.Where(x => x.Id == taskId && (x.State == "Paused" || x.State == "Failed"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Queued").SetProperty(x => x.StopRequested, false)
                .SetProperty(x => x.ConsecutiveFailures, 0).SetProperty(x => x.Error, (string?)null)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        if (changed == 0) throw new InvalidOperationException("Chỉ tiếp tục được tác vụ đã tạm dừng hoặc thất bại.");

        db.AdminTaskControlEvents.Add(new AdminTaskControlEvent { TaskId = taskId, OperatorId = operatorId, OwnerId = task.ActorId,
            Action = "Resume", Reason = reason, StateBefore = task.State, StateAfter = "Queued", RequestId = requestId });
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return await Replay(requestId, ct) ?? throw new InvalidOperationException("Không ghi được sự kiện can thiệp. Vui lòng thử lại."); }
        return new { taskId, action = "Resume", stateBefore = task.State, stateAfter = "Queued", replay = false };
    }
}
