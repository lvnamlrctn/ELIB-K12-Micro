using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Catalog.Domain;

/// <summary>Trường con MARC: mã một ký tự ($a, $b…) và giá trị.</summary>
public sealed record MarcSubfield(string Code, string Value);

/// <summary>
/// Một trường MARC21. Trường điều khiển (001–009) chỉ có <see cref="Value"/>; trường dữ liệu (010–999) có hai chỉ thị và
/// danh sách trường con theo đúng thứ tự nhập. Trường lặp là nhiều phần tử cùng <see cref="Tag"/> — giữ nguyên thứ tự
/// (monolith gộp các lần lặp cùng chỉ thị khi đọc lại — lỗi không mang sang).
/// </summary>
public sealed record MarcField(string Tag, string? Ind1 = null, string? Ind2 = null, string? Value = null, IReadOnlyList<MarcSubfield>? Subfields = null)
{
    public bool IsControl => MarcRecord.IsControlTag(Tag);

    /// <summary>Giá trị các trường con có mã <paramref name="code"/>, theo thứ tự.</summary>
    public IEnumerable<string> Values(char code) =>
        (Subfields ?? []).Where(s => s.Code.Length == 1 && s.Code[0] == code).Select(s => s.Value);
}

/// <summary>Thông tin tóm tắt rút từ MARC — lưu thành cột để tìm/lọc và gửi trong <c>BibChanged</c>.</summary>
public sealed record BibSummary(
    string Title, string? Author, string? Publisher, string? PublishYear, IReadOnlyList<string> Isbns, string? Ddc, string? Keywords, string? Language);

/// <summary>Quy tắc MARC21 dùng chung: chuẩn hoá trường nhập, sinh Leader/008, rút thông tin tóm tắt.</summary>
public static partial class MarcRecord
{
    public const int MaxFields = 500;
    public const int MaxValueLength = 9999;

    /// <summary>Trường điều khiển do hệ thống quản lý: 001 = MFN, 005 = thời điểm sửa gần nhất (không lưu, sinh khi đọc).</summary>
    public static readonly string[] SystemControlTags = ["001", "005"];

    public static bool IsControlTag(string tag) => tag.Length == 3 && string.CompareOrdinal(tag, "010") < 0;

    /// <summary>
    /// Chuẩn hoá danh sách trường người dùng gửi: tag 3 ký tự, chỉ thị một ký tự (trống/#/_ → khoảng trắng), bỏ trường con rỗng,
    /// bỏ trường dữ liệu không còn trường con nào, bỏ 000/001/005 (hệ thống tự quản). Lỗi cấu trúc → <see cref="BusinessRuleException"/>.
    /// <paramref name="template"/> (biểu mẫu biên mục): giữ trường/trường con chưa có giá trị, giữ thứ tự người dùng sắp.
    /// </summary>
    public static List<MarcField> Normalize(IEnumerable<MarcField>? fields, bool template = false)
    {
        var result = new List<MarcField>();
        foreach (var field in fields ?? [])
        {
            var tag = (field.Tag ?? "").Trim();
            if (!TagPattern().IsMatch(tag))
                throw new BusinessRuleException("MARC_TAG_INVALID", $"Nhãn trường '{field.Tag}' không hợp lệ (3 chữ số).");
            if (tag == "000" || SystemControlTags.Contains(tag)) continue;

            if (IsControlTag(tag))
            {
                var value = Clip(field.Value, tag);
                if (value.Length > 0 || template) result.Add(new MarcField(tag, Value: value));
                continue;
            }

            var subfields = new List<MarcSubfield>();
            foreach (var sub in field.Subfields ?? [])
            {
                var code = (sub.Code ?? "").Trim().ToLowerInvariant();
                if (!CodePattern().IsMatch(code))
                    throw new BusinessRuleException("MARC_SUBFIELD_INVALID", $"Trường {tag}: mã trường con '{sub.Code}' không hợp lệ (một chữ thường hoặc chữ số).");
                var value = Clip(sub.Value, $"{tag}${code}");
                if (value.Length > 0 || template) subfields.Add(new MarcSubfield(code, value));
            }
            if (subfields.Count > 0 || template) result.Add(new MarcField(tag, Indicator(field.Ind1, tag), Indicator(field.Ind2, tag), Subfields: subfields));
        }
        if (result.Count > MaxFields)
            throw new BusinessRuleException("MARC_TOO_MANY_FIELDS", $"Biểu ghi tối đa {MaxFields} trường.");
        // Sắp theo nhãn, giữ thứ tự nhập giữa các lần lặp cùng nhãn (OrderBy ổn định).
        return template ? result : [.. result.OrderBy(f => f.Tag, StringComparer.Ordinal)];
    }

    /// <summary>Rút thông tin tóm tắt như BibXml của monolith (245, 100/110/700, 260/264, 020, 082, 650/653, 041/008).</summary>
    public static BibSummary Summarize(IReadOnlyList<MarcField> fields)
    {
        var title = Join(First(fields, "245"), 'a', 'b', 'n', 'p')
            ?? throw new BusinessRuleException("MARC_TITLE_REQUIRED", "Biểu ghi phải có nhan đề (trường 245$a).");
        var author = FirstValue(fields, "100", 'a') ?? FirstValue(fields, "110", 'a') ?? FirstValue(fields, "700", 'a');
        var publisher = FirstValue(fields, "264", 'b') ?? FirstValue(fields, "260", 'b');
        var date = FirstValue(fields, "264", 'c') ?? FirstValue(fields, "260", 'c');
        var isbns = fields.Where(f => f.Tag == "020").SelectMany(f => f.Values('a')).Select(NormalizeIsbn)
            .Where(i => i is not null).Select(i => i!).Distinct().ToList();
        var keywords = string.Join("; ", fields.Where(f => f.Tag is "650" or "653").SelectMany(f => f.Values('a')).Select(v => Trim(v)).Where(v => v.Length > 0).Distinct());
        var language = FirstValue(fields, "041", 'a')
            ?? (fields.FirstOrDefault(f => f.Tag == "008")?.Value is { Length: >= 38 } f008 ? f008.Substring(35, 3).Trim() : null);
        return new BibSummary(Cut(title, 1000)!, Cut(author, 500), Cut(publisher, 500), Year(date), isbns,
            Cut(FirstValue(fields, "082", 'a'), 50), keywords.Length > 0 ? Cut(keywords, 2000) : null, string.IsNullOrWhiteSpace(language) ? null : language);
    }

    /// <summary>ISBN 10/13: bỏ gạch, khoảng trắng và phần chú thích "(bìa mềm)"; null nếu không giống ISBN.</summary>
    public static string? NormalizeIsbn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var head = text.Split('(', ':', ';')[0];
        var digits = new string(head.Where(c => char.IsAsciiDigit(c) || c is 'x' or 'X').ToArray()).ToUpperInvariant();
        return digits.Length is 10 or 13 ? digits : null;
    }

    /// <summary>Leader 24 ký tự (monolith: BuildLeader) — 06/07 theo loại biểu ghi; độ dài/địa chỉ tính lại khi xuất ISO2709.</summary>
    public static string BuildLeader(char recordType, char bibLevel)
    {
        var leader = "00000nam a2200000 a 4500".ToCharArray();
        leader[6] = recordType;
        leader[7] = bibLevel;
        return new string(leader);
    }

    /// <summary>Đặt lại Leader/06–07 theo loại biểu ghi, giữ phần còn lại người dùng đã nhập (nếu hợp lệ).</summary>
    public static string ApplyType(string? leader, char recordType, char bibLevel)
    {
        if (leader is not { Length: 24 }) return BuildLeader(recordType, bibLevel);
        var chars = leader.ToCharArray();
        chars[6] = recordType;
        chars[7] = bibLevel;
        return new string(chars);
    }

    /// <summary>008 cho sách (monolith: BuildField008): ngày nhập, năm xuất bản, nơi xuất bản "vm ", ngôn ngữ "vie".</summary>
    public static string BuildField008(DateOnly entered, string? publishYear)
    {
        var f = new string(' ', 40).ToCharArray();
        entered.ToString("yyMMdd", CultureInfo.InvariantCulture).CopyTo(0, f, 0, 6);
        f[6] = 's';
        (publishYear ?? "    ").PadRight(4)[..4].CopyTo(0, f, 7, 4);
        "vm ".CopyTo(0, f, 15, 3);
        "vie".CopyTo(0, f, 35, 3);
        return new string(f);
    }

    /// <summary>Chữ thường không dấu (đ → d) — cột tìm kiếm, để "nguyen du" khớp "Nguyễn Du".</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch));
        }
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static MarcField? First(IReadOnlyList<MarcField> fields, string tag) => fields.FirstOrDefault(f => f.Tag == tag);

    private static string? FirstValue(IReadOnlyList<MarcField> fields, string tag, char code) =>
        First(fields, tag)?.Values(code).Select(v => Trim(v)).FirstOrDefault(v => v.Length > 0);

    private static string? Join(MarcField? field, params char[] codes)
    {
        if (field is null) return null;
        var parts = (field.Subfields ?? []).Where(s => s.Code.Length == 1 && codes.Contains(s.Code[0])).Select(s => Trim(s.Value)).Where(v => v.Length > 0);
        var text = string.Join(" ", parts);
        return text.Length > 0 ? text : null;
    }

    /// <summary>Bỏ dấu câu ISBD cuối giá trị (" /", " :", " ;", ",", ".") như khi hiển thị.</summary>
    private static string Trim(string? value) => (value ?? "").Trim().TrimEnd('/', ':', ';', ',', '=', ' ').TrimEnd('.').Trim();

    private static string? Year(string? date) => date is null ? null : YearPattern().Match(date) is { Success: true } m ? m.Value : null;

    private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];

    private static string Clip(string? value, string where)
    {
        var v = (value ?? "").Trim();
        return v.Length <= MaxValueLength ? v : throw new BusinessRuleException("MARC_VALUE_TOO_LONG", $"{where}: tối đa {MaxValueLength} ký tự.");
    }

    private static string Indicator(string? value, string tag)
    {
        var v = string.IsNullOrEmpty(value) ? " " : value;
        if (v is "#" or "_") v = " ";
        return v.Length == 1 && (v == " " || char.IsAsciiLetterOrDigit(v[0]))
            ? v
            : throw new BusinessRuleException("MARC_INDICATOR_INVALID", $"Trường {tag}: chỉ thị '{value}' không hợp lệ (một ký tự).");
    }

    [GeneratedRegex("^[0-9]{3}$")]
    private static partial Regex TagPattern();

    [GeneratedRegex("^[a-z0-9]$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("[12][0-9]{3}")]
    private static partial Regex YearPattern();
}
