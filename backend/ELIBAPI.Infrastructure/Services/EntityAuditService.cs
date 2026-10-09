using System.Text.Json;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Lịch sử thay đổi theo hồ sơ (Đợt 16 — port từ ELIB-LRC "Nhật ký thay đổi chi tiết"). Không
/// tạo bảng/cột mới — tận dụng <see cref="UserLog"/> với <c>ActionType="EntityChange"</c>,
/// <c>Object="{EntityType}:{PublicId}"</c>, <c>Action</c> là JSON có schemaVersion/entity/actor/action/
/// reason/changes. Ghi NGUYÊN TỬ cùng transaction chính: <see cref="QueueEntityChangeLog"/> chỉ
/// <c>UserLogs.Add(...)</c> (không tự SaveChanges) — caller phải gọi ngay trước lệnh SaveChangesAsync có
/// sẵn của hành động chính, để lỗi ghi audit làm rollback luôn cả thay đổi nghiệp vụ (khác hẳn quy ước
/// "log lỗi không chặn luồng chính" của <c>WriteUserLogAsync</c>/<c>WriteLoanUserLogAsync</c> — đây là lựa
/// chọn có chủ đích cho riêng audit bắt buộc, không áp dụng ngược lại cho 2 cơ chế log cũ).</summary>
public static class EntityAuditService
{
    private const int MaxValueLength = 2000;
    private const int MaxReasonLength = 500;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public sealed class FieldChange
    {
        public string Field { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    private sealed class EntityChangePayload
    {
        public int SchemaVersion { get; set; } = 1;
        public string EntityType { get; set; } = "";
        public Guid PublicId { get; set; }
        public long ActorId { get; set; }
        public string? ActorName { get; set; }
        public string Action { get; set; } = "";
        public string? Reason { get; set; }
        public List<FieldChange> Changes { get; set; } = [];
    }

    /// <summary>Định dạng bất biến văn hoá — cùng lý do với <see cref="AdminMutationGuard"/> (tránh chuỗi
    /// khác nhau giữa 2 lần tính cùng 1 giá trị do văn hoá luồng gọi khác nhau). Chép lại logic thay vì
    /// tái dùng trực tiếp vì hàm gốc private trong class khác.</summary>
    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        DateTime dt => dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>Rút gọn giá trị field dài (2.000 ký tự) có đánh dấu — dùng chung cho cả diff qua
    /// ChangeTracker lẫn diff thủ công (ví dụ mức tag MARC ở CatalogueBookController, không đi qua
    /// ChangeTracker được vì BibData lưu kiểu xóa-rồi-tạo-lại).</summary>
    public static string? Truncate(string? s)
        => s != null && s.Length > MaxValueLength ? s[..MaxValueLength] + "…(đã rút gọn)" : s;

    /// <summary>Diff entity đang TRACKED (đã MapRequestToEntity, TRƯỚC SaveChangesAsync) theo allowlist —
    /// dùng <c>ChangeTracker</c> nên chỉ áp dụng được cho path đi qua <c>DbContext</c> generic (Reader qua
    /// BaseRepository). Field trong <paramref name="masked"/> bị thay giá trị bằng "(đã ẩn)" nếu có đổi —
    /// vẫn so sánh giá trị thật để phát hiện thay đổi, chỉ che khi LƯU/HIỂN THỊ.</summary>
    public static List<FieldChange> DiffTrackedEntity<TEntity>(
        ELIBAPIDbContext db, TEntity entity, string[] allowlist, IReadOnlySet<string> masked)
        where TEntity : class
    {
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(entity);
        var isNew = entry.State == EntityState.Added;
        var list = new List<FieldChange>();

        foreach (var name in allowlist)
        {
            var prop = entry.Property(name);
            if (!isNew && !prop.IsModified) continue;

            var before = isNew ? null : FormatValue(prop.OriginalValue);
            var after  = FormatValue(prop.CurrentValue);
            if (!isNew && string.Equals(before, after, StringComparison.Ordinal)) continue;

            if (masked.Contains(name))
            {
                before = before == null ? null : "(đã ẩn)";
                after  = after  == null ? null : "(đã ẩn)";
            }
            list.Add(new FieldChange { Field = name, OldValue = Truncate(before), NewValue = Truncate(after) });
        }
        return list;
    }

    /// <summary>Đọc lý do từ header <c>X-Change-Reason</c> (URL-encoded phía client), giải mã và cắt tối
    /// đa 500 ký tự. Không bắt buộc — thiếu header trả về null (đúng LRC, giữ tương thích client cũ).</summary>
    public static string? ReadReasonHeader(Microsoft.AspNetCore.Http.HttpContext? httpContext)
    {
        var raw = httpContext?.Request.Headers["X-Change-Reason"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string decoded;
        try { decoded = Uri.UnescapeDataString(raw); }
        catch { decoded = raw; }
        decoded = decoded.Trim();
        if (decoded.Length == 0) return null;
        return decoded.Length > MaxReasonLength ? decoded[..MaxReasonLength] : decoded;
    }

    /// <summary>Xếp hàng 1 dòng UserLog dạng EntityChange — KHÔNG tự SaveChanges, caller phải gọi
    /// SaveChangesAsync ngay sau (cùng transaction với thay đổi nghiệp vụ chính) để atomic. Bỏ qua nếu
    /// không có thay đổi thực sự khi action là "Update" (không ghi sự kiện giả).</summary>
    public static void QueueEntityChangeLog(
        ELIBAPIDbContext db, string entityType, Guid publicId, long actorId, string? actorName,
        long? tenantId, string action, string? reason, List<FieldChange> changes, string? ip)
    {
        if (changes.Count == 0 && action == "Update") return;

        var payload = new EntityChangePayload
        {
            EntityType = entityType, PublicId = publicId, ActorId = actorId, ActorName = actorName,
            Action = action, Reason = reason, Changes = changes,
        };
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        db.UserLogs.Add(new UserLog
        {
            UserId      = actorId,
            ActionType  = "EntityChange",
            Object      = $"{entityType}:{publicId}",
            Action      = json,
            Submited    = DateTime.Now,
            Ip          = ip,
            Application = "ELIBAPI",
            TenantId    = tenantId,
        });
    }
}
