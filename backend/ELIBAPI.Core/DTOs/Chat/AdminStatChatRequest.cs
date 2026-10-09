namespace ELIBAPI.Core.DTOs.Chat;

public class AdminStatChatRequest
{
    public string Question { get; set; } = "";
    /// <summary>Các lượt trước trong hội thoại — để hiểu câu nối tiếp ("còn tháng trước?").</summary>
    public List<ChatTurn>? History { get; set; }
}

public class AdminStatChatResponse
{
    public string Answer { get; set; } = "";
    public List<string> PermittedModules { get; set; } = [];
}
