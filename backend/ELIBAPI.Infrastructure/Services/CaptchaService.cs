using System.Text;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Services;

// Đăng ký Singleton trong DependencyInjection.cs: không giữ state riêng ngoài IMemoryCache (vốn đã
// thread-safe), nên không cần Scoped theo từng request.
public class CaptchaService(IMemoryCache cache) : ICaptchaService
{
    // Bỏ 0/O, 1/I/L khỏi bảng ký tự để tránh người dùng nhầm lẫn khi gõ lại từ ảnh SVG.
    private const string Charset = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 5;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private static readonly string[] Colors = ["#1e3a8a", "#7c2d12", "#14532d", "#581c87", "#7f1d1d", "#164e63"];

    public (string CaptchaId, string Svg) Generate()
    {
        var rnd = Random.Shared;
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = Charset[rnd.Next(Charset.Length)];
        var code = new string(chars);

        var captchaId = Guid.NewGuid().ToString("N");
        cache.Set(CacheKey(captchaId), code, Ttl);

        return (captchaId, BuildSvg(code, rnd));
    }

    public bool Validate(string captchaId, string? answer)
    {
        var key = CacheKey(captchaId);
        var ok = cache.TryGetValue<string>(key, out var expected)
                 && !string.IsNullOrEmpty(answer)
                 && string.Equals(expected, answer.Trim(), StringComparison.OrdinalIgnoreCase);
        cache.Remove(key); // dùng 1 lần — luôn xoá dù đúng hay sai, chống thử lại nhiều lần trên cùng 1 mã
        return ok;
    }

    private static string CacheKey(string captchaId) => $"Captcha:{captchaId}";

    // SVG vẽ tay bằng StringBuilder — không phụ thuộc thư viện ảnh ngoài (System.Drawing/SkiaSharp...).
    // Mỗi ký tự được jitter vị trí/góc xoay/màu/cỡ chữ ngẫu nhiên, cộng thêm vài đường nhiễu, để gây khó
    // cho OCR tự động trong khi vẫn đọc được bằng mắt người.
    private static string BuildSvg(string code, Random rnd)
    {
        const int width = 150, height = 50;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        sb.Append($"<rect width=\"{width}\" height=\"{height}\" fill=\"#f8fafc\"/>");

        for (var i = 0; i < 4; i++)
        {
            var x1 = rnd.Next(width);
            var y1 = rnd.Next(height);
            var x2 = rnd.Next(width);
            var y2 = rnd.Next(height);
            sb.Append($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"#cbd5e1\" stroke-width=\"1\"/>");
        }

        var slot = width / code.Length;
        for (var i = 0; i < code.Length; i++)
        {
            var cx = slot * i + slot / 2 + rnd.Next(-4, 5);
            var cy = height / 2 + rnd.Next(-6, 7);
            var angle = rnd.Next(-25, 26);
            var color = Colors[rnd.Next(Colors.Length)];
            var fontSize = 22 + rnd.Next(-2, 4);
            sb.Append($"<text x=\"{cx}\" y=\"{cy}\" font-size=\"{fontSize}\" font-weight=\"700\" font-family=\"monospace\" fill=\"{color}\" text-anchor=\"middle\" dominant-baseline=\"middle\" transform=\"rotate({angle} {cx} {cy})\">{code[i]}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
