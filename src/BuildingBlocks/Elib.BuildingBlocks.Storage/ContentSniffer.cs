namespace Elib.BuildingBlocks.Storage;

/// <summary>
/// Nhận dạng loại file theo magic bytes (docs 05 §3) — không tin Content-Type/đuôi file trình duyệt gửi.
/// Chỉ nhận các loại an toàn khi phục vụ lại cho trình duyệt; SVG/HTML không bao giờ được nhận (chạy được script).
/// </summary>
public static class ContentSniffer
{
    /// <summary>Số byte đầu cần đọc để nhận dạng.</summary>
    public const int HeaderLength = 16;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89 = "GIF89a"u8.ToArray();
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();

    /// <summary>MIME thật của nội dung, hoặc null nếu không thuộc loại được hỗ trợ.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(Png)) return "image/png";
        if (header.StartsWith(Jpeg)) return "image/jpeg";
        if (header.StartsWith(Gif87) || header.StartsWith(Gif89)) return "image/gif";
        if (header.Length >= 12 && header.StartsWith(Riff) && header[8..12].SequenceEqual(Webp)) return "image/webp";
        if (header.StartsWith(Pdf)) return "application/pdf";
        return null;
    }
}
