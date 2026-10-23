using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>
/// Trạng thái đồng bộ nội dung (trích chữ + vector hóa) của một tài liệu số (LRC: "Đồng bộ nội dung"). Mỗi tài liệu một dòng; việc nền
/// nhận các dòng <see cref="Pending"/> theo thứ tự yêu cầu. Dòng kẹt ở <see cref="Running"/> quá <see cref="StaleMinutes"/> phút (tiến trình chết giữa chừng) được trả về Pending.
/// </summary>
public sealed class EbookContentState : TenantEntity
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Done = "done";

    /// <summary>File PDF không có lớp văn bản (PDF quét) — chưa có OCR nên không có gì để lập chỉ mục.</summary>
    public const string NoText = "no_text";

    public const string Failed = "failed";
    public const int StaleMinutes = 30;

    private EbookContentState() { }

    public long EbookItemId { get; private set; }
    public string Status { get; private set; } = Pending;
    public int PageCount { get; private set; }
    public int ChunkCount { get; private set; }

    /// <summary>Số đoạn đã có vector (có thể ít hơn <see cref="ChunkCount"/> nếu Ollama lỗi hoặc chưa bật khi đồng bộ).</summary>
    public int EmbeddedCount { get; private set; }

    public string? Error { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? SyncedAt { get; private set; }

    public static EbookContentState Request(long ebookItemId, DateTimeOffset now) => new() { EbookItemId = ebookItemId, RequestedAt = now };

    /// <summary>Yêu cầu đồng bộ lại. Đang chạy thì giữ nguyên (việc đang làm sẽ phản ánh nội dung mới nhất sau khi xong — nhận lại khi người dùng bấm tiếp).</summary>
    public bool Requeue(DateTimeOffset now)
    {
        if (Status == Running) return false;
        Status = Pending;
        Error = null;
        RequestedAt = now;
        return true;
    }

    public void Start(DateTimeOffset now)
    {
        Status = Running;
        StartedAt = now;
        Error = null;
    }

    public void Finish(string status, int pageCount, int chunkCount, int embeddedCount, string? error, DateTimeOffset now)
    {
        Status = status;
        PageCount = pageCount;
        ChunkCount = chunkCount;
        EmbeddedCount = embeddedCount;
        Error = error is { Length: > 500 } ? error[..500] : error;
        SyncedAt = now;
    }

    /// <summary>Trả về hàng đợi (bị ngắt giữa chừng khi dịch vụ tắt, hoặc kẹt quá lâu).</summary>
    public void Release(DateTimeOffset now)
    {
        Status = Pending;
        StartedAt = null;
        RequestedAt = now;
    }
}

/// <summary>
/// Một đoạn văn bản của tài liệu (trong phạm vi một trang) kèm vector. <see cref="SearchText"/> là bản chữ thường để tìm không phân biệt hoa/thường
/// kể cả chữ Việt có dấu (hàm lower() của CSDL phụ thuộc collation). Vector lưu <c>real[]</c> — chưa có pgvector nên so khớp cosine tính trong ứng dụng.
/// </summary>
public sealed class EbookChunk : TenantEntity
{
    public const int MaxTextLength = 4000;

    private EbookChunk() { }

    public long EbookItemId { get; private set; }
    public int Ordinal { get; private set; }
    public int Page { get; private set; }
    public string Text { get; private set; } = "";
    public string SearchText { get; private set; } = "";
    public float[]? Embedding { get; private set; }

    public static EbookChunk Create(long ebookItemId, int ordinal, int page, string text, float[]? embedding)
    {
        var clipped = text.Length > MaxTextLength ? text[..MaxTextLength] : text;
        return new EbookChunk { EbookItemId = ebookItemId, Ordinal = ordinal, Page = page, Text = clipped, SearchText = clipped.ToLowerInvariant(), Embedding = embedding };
    }
}
