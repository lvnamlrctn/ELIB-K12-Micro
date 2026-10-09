namespace ELIBAPI.Core.DTOs.Chat;

public class ChatResponse
{
    public string Answer { get; set; } = "";
    public List<ChatSource> Sources { get; set; } = [];
}

public class ChatSource
{
    public string EbookId { get; set; } = "";
    public Guid? EbookFileId { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public int PageNumber { get; set; }
    public string? Excerpt { get; set; }
    public double Score { get; set; }
}
