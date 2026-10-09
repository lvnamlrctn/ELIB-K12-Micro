using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Đóng dấu tên người xem + thời điểm lên mọi trang PDF (đọc tài liệu số qua EbookFile/View).
/// PDFsharp 6 không tự tìm font hệ thống: trước đây không cấu hình font nên vẽ chữ luôn lỗi, lỗi bị nuốt và người xem
/// nhận nguyên file gốc (không có dấu). Nay dùng <see cref="WatermarkFontResolver"/>; không có font thì
/// <see cref="Watermark"/> trả file gốc kèm <c>Applied = false</c> để bên gọi ghi log.
/// </summary>
public static class PdfWatermarkHelper
{
    private const string Family = "Watermark";
    private static readonly object InstallLock = new();
    private static bool _installed;

    public static byte[] Watermark(Stream pdfStream, string watermarkText) => TryWatermark(pdfStream, watermarkText).Pdf;

    /// <summary>File đã đóng dấu; nếu không đóng dấu được (PDF hỏng/mã hoá, không có font) thì file gốc và lý do.</summary>
    public static (byte[] Pdf, bool Applied, string? Reason) TryWatermark(Stream pdfStream, string watermarkText)
    {
        if (!EnsureFont()) return (Original(pdfStream), false, "Không tìm thấy font để đóng dấu (cài fonts-dejavu-core hoặc đặt PDF_WATERMARK_FONT)");

        PdfDocument doc;
        try { doc = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Modify); }
        catch (Exception ex) { return (Original(pdfStream), false, "Không mở được PDF để đóng dấu: " + ex.Message); }

        try
        {
            using (doc)
            {
                var font  = new XFont(Family, 26, XFontStyleEx.Bold);
                var brush = new XSolidBrush(XColor.FromArgb(50, 100, 100, 100));
                foreach (var page in doc.Pages)
                {
                    using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    double w = page.Width.Point, h = page.Height.Point;
                    const double stepX = 260, stepY = 160;
                    for (double y = -stepY; y < h + stepY * 2; y += stepY)
                    for (double x = -stepX; x < w + stepX * 2; x += stepX)
                        DrawTile(gfx, font, brush, watermarkText, x, y);
                }
                using var ms = new MemoryStream();
                doc.Save(ms);
                return (ms.ToArray(), true, null);
            }
        }
        catch (Exception ex) { return (Original(pdfStream), false, "Lỗi khi đóng dấu PDF: " + ex.Message); }
    }

    private static bool EnsureFont()
    {
        if (_installed) return true;
        lock (InstallLock)
        {
            if (_installed) return true;
            if (WatermarkFontResolver.FindFontFile() == null) return false;
            // FontResolver của PDFsharp là cấu hình toàn cục, chỉ gán được 1 lần — dấu PDF là nơi duy nhất vẽ chữ.
            GlobalFontSettings.FontResolver ??= new WatermarkFontResolver();
            _installed = true;
            return true;
        }
    }

    private static byte[] Original(Stream pdfStream)
    {
        pdfStream.Position = 0;
        using var raw = new MemoryStream();
        pdfStream.CopyTo(raw);
        return raw.ToArray();
    }

    private static void DrawTile(XGraphics gfx, XFont font, XBrush brush, string text, double cx, double cy)
    {
        var state = gfx.Save();
        gfx.TranslateTransform(cx, cy);
        gfx.RotateTransform(-35);
        gfx.DrawString(text, font, brush, new XPoint(0, 0), XStringFormats.Center);
        gfx.Restore(state);
    }
}

/// <summary>
/// Font cho dấu PDF, lấy từ file TTF có sẵn trên máy (không đóng gói font vào phần mềm): biến môi trường
/// <c>PDF_WATERMARK_FONT</c>, DejaVu Sans (gói <c>fonts-dejavu-core</c> trong Docker image — có đủ dấu tiếng Việt),
/// rồi Arial của Windows (máy dev). Mọi tên/kiểu chữ đều dùng chung file này.
/// </summary>
public sealed class WatermarkFontResolver : IFontResolver
{
    private const string FaceName = "watermark-face";
    private static readonly Lazy<byte[]?> Bytes = new(() => FindFontFile() is { } path ? File.ReadAllBytes(path) : null);

    public static string? FindFontFile()
    {
        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("PDF_WATERMARK_FONT"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
        ];
        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) => new(FaceName);

    public byte[]? GetFont(string faceName) => Bytes.Value;
}
