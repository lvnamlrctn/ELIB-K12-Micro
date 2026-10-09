using System.Net;
using System.Text.RegularExpressions;

namespace Elib.Notification.Application;

/// <summary>
/// Thay biến <c>{{ ten_bien }}</c> trong mẫu. Nội dung (HTML): giá trị được mã hoá HTML — dữ liệu từ người dùng không chèn được thẻ.
/// Tiêu đề: văn bản thường, bỏ ký tự xuống dòng (chống chèn header). Biến không có giá trị thành chuỗi rỗng.
/// </summary>
public static partial class TemplateRenderer
{
    public static string RenderHtml(string template, IReadOnlyDictionary<string, string> data) =>
        Placeholder().Replace(template, m => WebUtility.HtmlEncode(Lookup(data, m.Groups[1].Value)));

    public static string RenderSubject(string template, IReadOnlyDictionary<string, string> data) =>
        Placeholder().Replace(template, m => Lookup(data, m.Groups[1].Value))
            .Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string Lookup(IReadOnlyDictionary<string, string> data, string key) =>
        data.TryGetValue(key, out var value) ? value : "";

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}")]
    private static partial Regex Placeholder();
}
