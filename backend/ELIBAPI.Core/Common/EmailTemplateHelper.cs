namespace ELIBAPI.Core.Common;

/// Thay thế placeholder dạng {Token} trong nội dung mẫu email bằng giá trị thực — string-replace đơn
/// giản, không phải template engine.
public static class EmailTemplateHelper
{
    public static string Render(string template, Dictionary<string, string> tokens)
    {
        var result = template;
        foreach (var (key, value) in tokens)
            result = result.Replace("{" + key + "}", value ?? "");
        return result;
    }
}
