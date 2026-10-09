using ELIBAPI.Core.DTOs.Chat;

namespace ELIBAPI.Core.Interfaces;

public interface IChatService
{
    /// <summary><paramref name="tenantId"/> = đơn vị của OPAC đang hỏi (đã phân giải); chỉ truy xuất nội dung của đơn vị đó và
    /// tài liệu dùng chung.</summary>
    Task<ChatResponse> AskAsync(ChatRequest request, long? tenantId, CancellationToken ct = default);

    /// <summary>Như <see cref="AskAsync"/> nhưng trả dần: "sources" (nếu có) → nhiều "delta" → "done" (hoặc "error").</summary>
    IAsyncEnumerable<ChatStreamEvent> AskStreamAsync(ChatRequest request, long? tenantId, CancellationToken ct = default);
}
