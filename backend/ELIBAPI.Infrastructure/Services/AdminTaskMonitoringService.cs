using System.Text;
using System.Text.Json;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Truy vấn giám sát tác vụ toàn hệ thống (Đợt 13 — port từ ELIB-LRC `AdminTaskMonitoringService`).
/// Đọc mọi actor, không chỉ actor đang đăng nhập — tách khỏi <see cref="AdminTaskService"/> để không mở
/// rộng phạm vi dữ liệu của endpoint cá nhân hiện có (Đợt 10). Chỉ trả metadata, không giải mã/trả
/// Payload hoặc Result thô. Khác LRC (đơn-tenant): mọi method nhận thêm <c>callerTenantId</c> — khi khác
/// null (nhân viên tenant, không đặc quyền), chỉ trả tác vụ cùng tenant; null (tài khoản đặc quyền) thấy
/// tất cả, đúng quy ước đã dùng cho "tác vụ của tôi" ở Đợt 10.</summary>
public sealed class AdminTaskMonitoringService(ELIBAPIDbContext db)
{
    private static readonly JsonSerializerOptions Json = AdminTaskService.Json;

    public sealed record Cursor(DateTime CreatedAt, Guid Id);

    public static string Encode(DateTime createdAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Cursor(createdAt, id), Json)));

    public static Cursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try { return JsonSerializer.Deserialize<Cursor>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), Json); }
        catch { throw new InvalidOperationException("Con trỏ phân trang không hợp lệ. Vui lòng tải lại từ đầu."); }
    }

    public async Task<object> List(long? callerTenantId, long? actorId, string? kind, string? state,
        DateTime? from, DateTime? to, string? cursor, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit <= 0 ? 25 : limit, 1, 100);
        var boundary = Decode(cursor);

        var query = db.AdminTasks.AsNoTracking().AsQueryable();
        if (callerTenantId != null) query = query.Where(x => x.TenantId == callerTenantId);
        if (actorId.HasValue) query = query.Where(x => x.ActorId == actorId.Value);
        if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(state)) query = query.Where(x => x.State == state);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to.Value);
        // Biên keyset: đơn giản hóa collision cùng CreatedAt bằng loại trừ đúng Id của cursor — đủ ổn định
        // ở quy mô tác vụ quản trị hiện tại (không phải luồng ghi tần suất cao).
        if (boundary != null) query = query.Where(x =>
            x.CreatedAt < boundary.CreatedAt || (x.CreatedAt == boundary.CreatedAt && x.Id != boundary.Id));

        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1)
            .Select(x => new { x.Id, x.ActorId, x.TenantId, x.Kind, x.State, x.Preview, x.Attempts, x.Version,
                x.TotalChunks, x.CompletedChunks, x.TotalItems, x.CompletedItems, x.StopRequested, x.CreatedAt,
                x.UpdatedAt, x.Error })
            .ToListAsync(ct);

        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        var actorIds = rows.Select(r => r.ActorId).Distinct().ToList();
        var owners = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.LoginName }).ToDictionaryAsync(u => u.Id, u => (u.LoginName ?? "").Trim(), ct);

        var staleCutoff = DateTime.UtcNow.AddMinutes(-10);
        var items = rows.Select(x => new {
            x.Id, x.ActorId, ownerName = owners.GetValueOrDefault(x.ActorId, "#" + x.ActorId), x.TenantId, x.Kind,
            x.State, x.Preview, x.Attempts, x.Version, x.TotalChunks, x.CompletedChunks, x.TotalItems,
            x.CompletedItems, x.StopRequested, x.CreatedAt, x.UpdatedAt, x.Error,
            // "Cần chú ý": tác vụ đang chờ/chạy nhưng lâu không cập nhật — khác nghĩa với heartbeat worker.
            needsAttention = (x.State == "Queued" || x.State == "Running") && x.UpdatedAt < staleCutoff
        });
        var nextCursor = hasMore ? Encode(rows[^1].CreatedAt, rows[^1].Id) : null;
        return new { items, nextCursor };
    }

    public async Task<object> Summary(long? callerTenantId, long? actorId, string? kind, DateTime? from,
        DateTime? to, CancellationToken ct)
    {
        var query = db.AdminTasks.AsNoTracking().AsQueryable();
        if (callerTenantId != null) query = query.Where(x => x.TenantId == callerTenantId);
        if (actorId.HasValue) query = query.Where(x => x.ActorId == actorId.Value);
        if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(x => x.Kind == kind);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to.Value);
        var counts = await query.GroupBy(x => x.State).Select(g => new { State = g.Key, Count = g.Count() }).ToListAsync(ct);
        return new { counts = counts.ToDictionary(x => x.State, x => x.Count) };
    }

    /// <summary>Trả null khi không tìm thấy HOẶC khi tác vụ thuộc tenant khác người gọi (không đặc quyền)
    /// — controller convert cả 2 case thành cùng 1 phản hồi từ chối, không lộ sự tồn tại của tác vụ tenant
    /// khác qua sự khác biệt 403 và 404.</summary>
    public async Task<object?> Detail(long? callerTenantId, Guid id, CancellationToken ct)
    {
        var task = await db.AdminTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task == null) return null;
        if (callerTenantId != null && task.TenantId != callerTenantId) return null;

        var ownerName = (await db.Users.AsNoTracking().Where(u => u.Id == task.ActorId)
            .Select(u => u.LoginName).FirstOrDefaultAsync(ct))?.Trim();

        var events = await db.AdminTaskControlEvents.AsNoTracking().Where(x => x.TaskId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.OperatorId, x.Action, x.Reason, x.StateBefore, x.StateAfter, x.CreatedAt })
            .ToListAsync(ct);
        var operatorIds = events.Select(x => x.OperatorId).Distinct().ToList();
        var operators = await db.Users.AsNoTracking().Where(u => operatorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.LoginName }).ToDictionaryAsync(u => u.Id, u => (u.LoginName ?? "").Trim(), ct);

        return new {
            task.Id, task.ActorId, ownerName = string.IsNullOrEmpty(ownerName) ? "#" + task.ActorId : ownerName,
            task.TenantId, task.Kind, task.State, task.Preview, task.Attempts, task.Version, task.TotalChunks,
            task.CompletedChunks, task.TotalItems, task.CompletedItems, task.StopRequested, task.CreatedAt,
            task.UpdatedAt, task.Error,
            events = events.Select(e => new { e.Id, e.OperatorId,
                operatorName = operators.GetValueOrDefault(e.OperatorId, "#" + e.OperatorId),
                e.Action, e.Reason, e.StateBefore, e.StateAfter, e.CreatedAt })
        };
    }
}
