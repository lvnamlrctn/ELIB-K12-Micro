using ELIBAPI.Core.DTOs.Chat;

namespace ELIBAPI.Core.Interfaces;

/// <summary>Trợ lý thống kê cho cán bộ quản trị. <c>tenantId</c> lấy từ JWT của người hỏi (null = tài khoản hệ thống, xem
/// toàn bộ) — mọi số liệu/danh sách chỉ trong phạm vi đơn vị đó; model không tự chọn đơn vị.</summary>
public interface IAdminStatChatService
{
    Task<AdminStatChatResponse> AskStatsAsync(long userId, long? tenantId, string question, List<ChatTurn>? history = null, CancellationToken ct = default);

    /// <summary>Như <see cref="AskStatsAsync"/> nhưng trả dần: nhiều "delta" → "done" (hoặc "error").</summary>
    IAsyncEnumerable<ChatStreamEvent> AskStatsStreamAsync(long userId, long? tenantId, string question, List<ChatTurn>? history = null, CancellationToken ct = default);
}
