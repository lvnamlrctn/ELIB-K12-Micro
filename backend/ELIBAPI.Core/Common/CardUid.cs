namespace ELIBAPI.Core.Common;

/// <summary>Chuẩn hoá UID thẻ chip: bỏ khoảng trắng, ':' và '-', viết hoa ("04 a1:b2-c3" → "04A1B2C3"). Trống → null.
/// Đầu đọc USB (gõ như bàn phím) và phần mềm kiểm soát cửa có thể gửi UID với dấu phân cách khác nhau.</summary>
public static class CardUid
{
    public const int MaxLength = 64;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = new string(raw.Where(c => !char.IsWhiteSpace(c) && c != ':' && c != '-').ToArray()).ToUpperInvariant();
        return s.Length == 0 ? null : s;
    }

    public static bool IsValid(string? normalized) =>
        normalized != null && normalized.Length <= MaxLength && normalized.All(char.IsLetterOrDigit);
}
