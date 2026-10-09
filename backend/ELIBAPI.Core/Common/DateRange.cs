using System.Globalization;

namespace ELIBAPI.Core.Common;

/// <summary>
/// Khoảng ngày lọc từ chuỗi người dùng nhập. Ngày "đến" chỉ có ngày (vd "2026-10-04") được tính tới hết ngày đó (dùng
/// <see cref="ToExclusive"/>, so sánh &lt;) — trước đây nhiều màn hình hiểu là 00:00 nên mất trọn ngày cuối. Có giờ thì giữ
/// nguyên giờ (<see cref="ToInclusive"/>, so sánh ≤).
/// </summary>
public readonly record struct DateRange(DateTime? From, DateTime? ToExclusive, DateTime? ToInclusive)
{
    public static DateRange Parse(string? from, string? to)
    {
        DateTime? f = DateTime.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) ? a : null;
        if (!DateTime.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out var b)) return new(f, null, null);
        var dateOnly = b.TimeOfDay == TimeSpan.Zero && !(to ?? "").Contains(':');
        return dateOnly ? new(f, b.Date.AddDays(1), null) : new(f, null, b);
    }

    /// <summary>Cùng quy ước cho tham số đã là DateTime (JSON "2026-10-04" đọc ra 00:00): 00:00 hiểu là trọn ngày.</summary>
    public static DateRange Of(DateTime? from, DateTime? to) =>
        to is not { } b ? new(from, null, null)
        : b.TimeOfDay == TimeSpan.Zero ? new(from, b.Date.AddDays(1), null) : new(from, null, b);
}
