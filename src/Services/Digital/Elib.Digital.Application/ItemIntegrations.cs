using System.Globalization;

namespace Elib.Digital.Application;

/// <summary>Kết quả tra ISBN (LRC: IsbnLookupResult) — điền sẵn vào form tài liệu số; ảnh bìa không tải về.</summary>
public sealed record IsbnMetadata(
    string Isbn, string? Title, string? Subtitle, IReadOnlyList<string> Authors, string? Publisher, string? PublishDate, int? Pages,
    string? Language, string? Description, IReadOnlyList<string> Categories, string Source);

/// <summary>Tra metadata theo ISBN ở nguồn ngoài (Google Books, rồi Open Library bổ sung trường còn thiếu). null = không nguồn nào có.</summary>
public interface IIsbnLookup
{
    Task<IsbnMetadata?> FindAsync(string isbn, CancellationToken ct);
}

/// <summary>Gợi ý phân loại của AI cho một tài liệu: chủ đề / nhóm ngành chọn trong danh mục của đơn vị, và từ khóa.</summary>
public sealed record ClassificationSuggestion(long? SubjectId, string? SubjectName, long? TopicId, string? TopicName, IReadOnlyList<string> Keywords);

/// <summary>Một mục danh mục (chủ đề / nhóm ngành) đưa cho AI chọn — AI chỉ được trả đúng các mã này.</summary>
public sealed record ClassificationOption(long Id, string Name);

/// <summary>Gợi ý phân loại bằng mô hình ngôn ngữ (LRC: CatalogingAiService → Gemini). <see cref="Enabled"/> = false khi chưa cấu hình khóa.</summary>
public interface IClassificationAi
{
    bool Enabled { get; }

    /// <summary>null khi mô hình không trả được kết quả dùng được (đã ghi log) — người dùng vẫn nhập tay.</summary>
    Task<ClassificationSuggestion?> SuggestAsync(
        string title, string? description, IReadOnlyList<ClassificationOption> subjects, IReadOnlyList<ClassificationOption> topics, CancellationToken ct);
}

/// <summary>Vector hóa văn bản (LRC: OllamaEmbeddingService — nomic-embed-text). <see cref="Enabled"/> = false khi chưa cấu hình địa chỉ Ollama.</summary>
public interface ITextEmbedder
{
    bool Enabled { get; }

    /// <summary>Vector đơn vị (đã chuẩn hóa) — <paramref name="query"/>: câu hỏi tìm kiếm thay vì đoạn tài liệu (khác tiền tố tác vụ).</summary>
    Task<float[]> EmbedAsync(string text, bool query, CancellationToken ct);
}

/// <summary>Văn bản của một trang PDF (số trang từ 1).</summary>
public sealed record PdfPageText(int Page, string Text);

/// <summary>Trích lớp văn bản của PDF. PDF quét (không có lớp văn bản) cho ra danh sách rỗng — chưa có OCR.</summary>
public interface IPdfTextExtractor
{
    /// <summary>Đọc lần lượt từng trang, dừng sau <paramref name="maxPages"/> trang. Ném <see cref="InvalidDataException"/> nếu không phải PDF đọc được.</summary>
    IEnumerable<PdfPageText> Extract(Stream pdf, int maxPages);
}

/// <summary>Đọc/ghi nội dung file tài liệu số ở media qua /internal (luồng, không nạp cả file vào bộ nhớ).</summary>
public interface IMediaFileStore
{
    /// <summary>Luồng đọc nội dung file <c>ebook-file</c> đã xong; ném NotFoundException nếu không có.</summary>
    Task<Stream> OpenReadAsync(Guid mediaFileId, CancellationToken ct);

    /// <summary>Tải file PDF lên kho (media kiểm magic bytes + dung lượng) và trả id file media.</summary>
    Task<Guid> UploadAsync(string fileName, long size, Stream content, CancellationToken ct);
}

/// <summary>Khả năng tích hợp đang bật ở đơn vị này — giao diện ẩn nút của dịch vụ chưa cấu hình.</summary>
public sealed record ItemCapabilities(bool Isbn, bool Ai, bool Content, bool Embedding);

/// <summary>Cấu hình AI gợi ý phân loại (Gemini). Khóa để trống = tắt; đặt qua biến môi trường <c>Digital__Ai__ApiKey</c>, không ghi vào file.</summary>
public sealed class DigitalAiOptions
{
    public const string SectionName = "Digital:Ai";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-3.1-flash-lite";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>Cấu hình vector hóa (Ollama). Địa chỉ để trống = tắt; đặt qua <c>Digital__Ollama__BaseUrl</c>.</summary>
public sealed class DigitalOllamaOptions
{
    public const string SectionName = "Digital:Ollama";
    public string? BaseUrl { get; set; }
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>ISBN-10/13: bỏ dấu gạch/khoảng trắng, kiểm độ dài và chữ số kiểm.</summary>
public static class IsbnFormat
{
    public static bool TryNormalize(string? raw, out string isbn)
    {
        isbn = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var s = new string([.. raw.Where(c => c is not ('-' or ' ' or ' '))]).ToUpperInvariant();
        if (s.Length == 13 && s.All(char.IsAsciiDigit) && Checksum13(s))
        {
            isbn = s;
            return true;
        }
        if (s.Length == 10 && s[..9].All(char.IsAsciiDigit) && (char.IsAsciiDigit(s[9]) || s[9] == 'X') && Checksum10(s))
        {
            isbn = s;
            return true;
        }
        return false;
    }

    /// <summary>ISBN-10 đã chuẩn hóa → ISBN-13 (tiền tố 978); null nếu không phải ISBN-10.</summary>
    public static string? ToIsbn13(string isbn10)
    {
        if (isbn10.Length != 10) return null;
        var body = "978" + isbn10[..9];
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (body[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return body + ((10 - sum % 10) % 10).ToString(CultureInfo.InvariantCulture);
    }

    private static bool Checksum13(string s)
    {
        var sum = 0;
        for (var i = 0; i < 13; i++) sum += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return sum % 10 == 0;
    }

    private static bool Checksum10(string s)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++) sum += (s[i] == 'X' ? 10 : s[i] - '0') * (10 - i);
        return sum % 11 == 0;
    }
}
