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

    /// <summary>OLE2/CFBF (DOC cũ) — cùng magic với XLS/PPT cũ, không phân biệt được chỉ từ 16 byte đầu; chấp nhận vì mục đích
    /// document-submission chỉ cho phép DOC/DOCX, không cho XLS/PPT, nên việc gắn nhãn "application/msword" ở đây không mở rộng
    /// loại file được chấp nhận ra ngoài những gì MediaPurpose đã khai.</summary>
    private static readonly byte[] Ole2 = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>ZIP (DOCX/XLSX/PPTX/ODT đều dùng magic này) — cùng lý do như Ole2, chỉ gắn nhãn DOCX vì đó là loại duy nhất
    /// document-submission khai nhận trong nhóm ZIP-based.</summary>
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>MIME thật của nội dung, hoặc null nếu không thuộc loại được hỗ trợ.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(Png)) return "image/png";
        if (header.StartsWith(Jpeg)) return "image/jpeg";
        if (header.StartsWith(Gif87) || header.StartsWith(Gif89)) return "image/gif";
        if (header.Length >= 12 && header.StartsWith(Riff) && header[8..12].SequenceEqual(Webp)) return "image/webp";
        if (header.StartsWith(Pdf)) return "application/pdf";
        if (header.StartsWith(Ole2)) return "application/msword";
        if (header.StartsWith(Zip)) return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        return null;
    }
}
