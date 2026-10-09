namespace ELIBAPI.Core.DTOs.Chat;

public class ChatRequest
{
    public string Question { get; set; } = "";
    public Guid? TenantId { get; set; }
    public Guid? EbookId { get; set; }
    public string? CollectionId { get; set; }
    public string? TopicId { get; set; }
    public string? SubjectId { get; set; }
    public string? Language { get; set; }
    public bool? Free { get; set; }
    /// <summary>Các lượt trước trong hội thoại (server chỉ giữ <see cref="ChatHistory.MaxTurns"/> lượt cuối).</summary>
    public List<ChatTurn>? History { get; set; }
}
