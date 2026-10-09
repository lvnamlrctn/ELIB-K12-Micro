namespace ELIBAPI.Core.Common;

/// <summary>
/// Tiện ích trích xuất dữ liệu từ MARC — dùng chung giữa index job (PrintBookIndexingJob)
/// và các truy vấn public đọc trực tiếp từ SQL (PublicPrintBookRepository).
/// </summary>
public static class MarcHelper
{
    private const int Lang008Offset = 35;

    /// <summary>Lấy mã ngôn ngữ 3 ký tự ở vị trí 35-37 của MARC 008.</summary>
    public static string? ExtractLanguage(string? field008)
    {
        if (string.IsNullOrEmpty(field008) || field008.Length < Lang008Offset + 3) return null;
        var code = field008.Substring(Lang008Offset, 3).Trim();
        return string.IsNullOrEmpty(code) ? null : code;
    }

    /// <summary>
    /// Tách năm từ chuỗi năm xuất bản tự do ("2015", "[2015]", "H. : NXB, 2015", "2015-2016").
    /// Lấy cụm 4 chữ số liên tiếp đầu tiên nằm trong 1000..2100.
    /// </summary>
    public static int? ExtractYear(string? publishDate)
    {
        if (string.IsNullOrEmpty(publishDate)) return null;
        for (var i = 0; i + 4 <= publishDate.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < 4; j++)
                if (!char.IsAsciiDigit(publishDate[i + j])) { ok = false; break; }
            if (!ok) continue;
            var year = int.Parse(publishDate.AsSpan(i, 4));
            if (year is >= 1000 and <= 2100) return year;
        }
        return null;
    }
}
