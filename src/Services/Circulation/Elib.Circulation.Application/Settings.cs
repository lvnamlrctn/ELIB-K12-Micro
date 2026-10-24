using System.Text.RegularExpressions;
using Elib.Circulation.Domain;

namespace Elib.Circulation.Application;

/// <summary>
/// Tham số lưu thông của đơn vị (service tenant, tham số service=circulation): C_RENEW_DATE ("1" = gia hạn tính từ hôm nay, "0" = từ hạn trả cũ)
/// và OVERDUE_METHOD (cách đếm số ngày quá hạn). Chính sách lưu thông theo loại bạn đọc đặt "gia hạn từ hôm nay" thì vẫn ghi đè.
/// </summary>
public sealed partial record CirculationSettings(bool RenewFromToday, OverdueMethod OverdueMethod)
{
    public const string RenewDateCode = "C_RENEW_DATE";
    public const string OverdueMethodCode = "OVERDUE_METHOD";

    public static CirculationSettings Default { get; } = new(false, OverdueMethod.Calendar);

    /// <summary>Đọc từ tham số thô — giá trị migrate từ ELIB-LRC có thể bọc thẻ HTML ("&lt;p&gt;1&lt;/p&gt;"), giá trị lạ coi như mặc định.</summary>
    public static CirculationSettings From(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var renew = Clean(values.GetValueOrDefault(RenewDateCode)) == "1";
        var method = Clean(values.GetValueOrDefault(OverdueMethodCode)) switch
        {
            "1" => OverdueMethod.SkipWeekends,
            "2" => OverdueMethod.SkipSundays,
            _ => OverdueMethod.Calendar,
        };
        return new CirculationSettings(renew, method);
    }

    private static string Clean(string? value) => Tags().Replace(value ?? "", "").Replace("&nbsp;", " ", StringComparison.Ordinal).Trim();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();
}

public interface ICirculationSettings
{
    /// <summary>Tham số của đơn vị hiện tại; không đọc được thì dùng mặc định (không làm hỏng mượn/trả).</summary>
    Task<CirculationSettings> GetAsync(CancellationToken ct);
}
