using System.Text;

namespace Elib.Catalog.Domain;

/// <summary>
/// Trình bày biểu ghi theo ISBD (mô tả thư mục tiêu chuẩn quốc tế) cho trang chi tiết OPAC — dạng phiếu mục lục. Monolith sinh ISBD
/// theo bảng cấu hình dấu câu từng loại tài liệu (Config_Isbd, chưa ai cấu hình thì để trống); ở đây dùng quy tắc ISBD mặc định cho
/// MARC21, luôn có kết quả:
/// <list type="number">
/// <item>Vùng nhan đề và trách nhiệm (245): a. n, p : b / c</item>
/// <item>Vùng lần xuất bản (250): a / b</item>
/// <item>Vùng xuất bản (264 ind2 = 1, không có thì 260): a ; a : b, c</item>
/// <item>Vùng mô tả vật lý (300): a : b ; c + e</item>
/// <item>Vùng tùng thư (490/440): (a ; v)</item>
/// </list>
/// Các vùng nối bằng ". — "; phụ chú (500/504) và ISBN (020) mỗi thứ một đoạn riêng. Dấu câu cán bộ đã gõ cuối giá trị (" /", " :"…)
/// được bỏ trước khi đặt dấu chuẩn, để không bị lặp.
/// </summary>
public static class Isbd
{
    private const string AreaSeparator = ". — ";

    /// <summary>Các đoạn ISBD: đoạn mô tả chính, rồi từng phụ chú, cuối cùng ISBN. Không có nhan đề → rỗng.</summary>
    public static IReadOnlyList<string> Build(IReadOnlyList<MarcField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var title = TitleArea(fields.FirstOrDefault(f => f.Tag == "245"));
        if (title is null) return [];

        var areas = new List<string> { title };
        AddIfAny(areas, EditionArea(fields.FirstOrDefault(f => f.Tag == "250")));
        AddIfAny(areas, PublicationArea(fields.FirstOrDefault(f => f.Tag == "264" && f.Ind2 == "1")
            ?? fields.FirstOrDefault(f => f.Tag == "264") ?? fields.FirstOrDefault(f => f.Tag == "260")));
        AddIfAny(areas, PhysicalArea(fields.FirstOrDefault(f => f.Tag == "300")));
        var series = string.Join(" ", fields.Where(f => f.Tag is "490" or "440").Select(SeriesArea).Where(s => s is not null));
        AddIfAny(areas, series.Length > 0 ? series : null);

        var paragraphs = new List<string> { Finish(JoinAreas(areas)) };
        paragraphs.AddRange(fields.Where(f => f.Tag is "500" or "504").SelectMany(f => f.Values('a')).Select(Clean)
            .Where(v => v.Length > 0).Select(Finish));
        var isbns = fields.Where(f => f.Tag == "020").Select(IsbnText).Where(s => s is not null).ToList();
        if (isbns.Count > 0) paragraphs.Add(Finish(JoinAreas(isbns!)));
        return paragraphs;
    }

    private static string? TitleArea(MarcField? f) => Compose(f, code => code switch
    {
        'a' => "",
        'n' or 'p' => ". ",
        'b' => " : ",
        'c' => " / ",
        _ => null,
    });

    private static string? EditionArea(MarcField? f) => Compose(f, code => code switch { 'a' => "", 'b' => " / ", _ => null });

    private static string? PublicationArea(MarcField? f) => Compose(f, code => code switch { 'a' => " ; ", 'b' => " : ", 'c' => ", ", _ => null });

    private static string? PhysicalArea(MarcField? f) => Compose(f, code => code switch { 'a' => "", 'b' => " : ", 'c' => " ; ", 'e' => " + ", _ => null });

    private static string? SeriesArea(MarcField f) =>
        Compose(f, code => code switch { 'a' => " = ", 'v' => " ; ", _ => null }) is { } text ? $"({text})" : null;

    private static string? IsbnText(MarcField f) =>
        Compose(f, code => code switch { 'a' => " ; ", 'c' => " : ", _ => null }) is { } text ? "ISBN " + text : null;

    /// <summary>
    /// Nối các trường con theo thứ tự nhập, mỗi trường con có dấu đứng trước do <paramref name="prefix"/> quyết định (null = bỏ trường
    /// con). Dấu đứng trước của trường con đầu tiên bị bỏ.
    /// </summary>
    private static string? Compose(MarcField? field, Func<char, string?> prefix)
    {
        if (field?.Subfields is not { Count: > 0 } subfields) return null;
        var text = new StringBuilder();
        foreach (var sub in subfields)
        {
            if (sub.Code.Length != 1 || prefix(sub.Code[0]) is not { } before) continue;
            var value = Clean(sub.Value);
            if (value.Length == 0) continue;
            if (text.Length > 0) AppendSeparator(text, before);
            text.Append(value);
        }
        return text.Length > 0 ? text.ToString() : null;
    }

    private static string JoinAreas(IEnumerable<string> areas)
    {
        var text = new StringBuilder();
        foreach (var area in areas)
        {
            if (text.Length > 0) AppendSeparator(text, AreaSeparator);
            text.Append(area);
        }
        return text.ToString();
    }

    /// <summary>Dấu bắt đầu bằng "." mà phần trước đã kết thúc bằng "." (viết tắt "H.", "tr.") → không thêm dấu chấm thứ hai.</summary>
    private static void AppendSeparator(StringBuilder text, string separator) =>
        text.Append(separator[0] == '.' && text[^1] == '.' ? separator[1..] : separator);

    private static string Finish(string text) => text.EndsWith('.') || text.EndsWith(')') || text.EndsWith('?') || text.EndsWith('!') ? text : text + ".";

    private static void AddIfAny(List<string> areas, string? area)
    {
        if (!string.IsNullOrWhiteSpace(area)) areas.Add(area);
    }

    /// <summary>Bỏ dấu câu ISBD cán bộ gõ cuối giá trị (" /", " :", " ;", " =", ","), giữ dấu chấm của chữ viết tắt.</summary>
    private static string Clean(string? value)
    {
        var v = (value ?? "").Trim();
        while (v.Length > 0 && v[^1] is '/' or ':' or ';' or '=' or ',') v = v[..^1].TrimEnd();
        return v;
    }
}
