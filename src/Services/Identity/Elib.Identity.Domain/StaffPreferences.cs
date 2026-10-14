using Elib.BuildingBlocks.Domain;

namespace Elib.Identity.Domain;

/// <summary>
/// Tuỳ chọn giao diện theo tài khoản cán bộ (ELIB-LRC: AdminPreference) — cột hiện/ẩn của từng màn danh sách... Giá trị là chuỗi JSON
/// do giao diện quản trị tự diễn giải; identity chỉ lưu. <see cref="TenantId"/> null = tài khoản hệ thống.
/// </summary>
public sealed class StaffPreference : Entity
{
    public const int MaxKeyLength = 100;
    public const int MaxValueLength = 4000;
    public const int MaxPerUser = 200;

    public long? TenantId { get; set; }
    public long UserId { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Khoá hợp lệ: chữ thường, số, dấu chấm, gạch ngang, gạch dưới — vd. "columns.readers".</summary>
    public static bool IsValidKey(string? key) =>
        key is { Length: > 0 and <= MaxKeyLength } && key.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_');
}
