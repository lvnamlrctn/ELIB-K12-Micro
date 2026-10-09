namespace ELIBAPI.Core.DTOs.Chat;

/// <summary>1 lượt trong lịch sử hội thoại client gửi kèm để model hiểu câu hỏi nối tiếp ("còn tháng trước?").</summary>
public class ChatTurn
{
    /// <summary>"user" hoặc "assistant".</summary>
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
}

/// <summary>Sự kiện của chat dạng luồng (SSE): "sources" (Data = danh sách nguồn), "delta" (Text = đoạn chữ mới),
/// "done", "error" (Text = thông báo).</summary>
public record ChatStreamEvent(string Type, string? Text = null, object? Data = null)
{
    public static ChatStreamEvent Delta(string text) => new("delta", text);
    public static ChatStreamEvent Done() => new("done");
    public static ChatStreamEvent Error(string message) => new("error", message);
    public static ChatStreamEvent Sources(object sources) => new("sources", Data: sources);
}

/// <summary>Chuẩn hoá lịch sử hội thoại từ client thành <c>contents</c> của Gemini: chỉ giữ vài lượt cuối, cắt
/// bớt lượt dài, ép vai trò về user/model và bảo đảm luân phiên — client không thể nhồi prompt quá dài.</summary>
public static class ChatHistory
{
    public const int MaxTurns = 6;
    public const int MaxCharsPerTurn = 1500;

    public static List<object> ToGeminiContents(IEnumerable<ChatTurn>? history, string question, int maxTurns = MaxTurns)
    {
        // Gemini yêu cầu luân phiên user/model và bắt đầu bằng user: bỏ lượt model đứng đầu, GỘP các lượt liền nhau
        // cùng vai trò (vd lượt trước lỗi nên chưa có câu trả lời) thay vì bỏ — không làm mất ngữ cảnh.
        var turns = new List<(string Role, string Text)>();
        foreach (var turn in Normalize(history, maxTurns).Append(new ChatTurn { Role = "user", Text = question }))
        {
            var role = turn.Role == "assistant" ? "model" : "user";
            if (turns.Count == 0 && role == "model") continue;
            if (turns.Count > 0 && turns[^1].Role == role) turns[^1] = (role, turns[^1].Text + "\n\n" + turn.Text);
            else turns.Add((role, turn.Text));
        }
        return turns.Select(t => (object)new { role = t.Role, parts = new object[] { new { text = t.Text } } }).ToList();
    }

    /// <summary>Các lượt hợp lệ cuối cùng (bỏ lượt rỗng, cắt còn <see cref="MaxCharsPerTurn"/> ký tự).</summary>
    public static List<ChatTurn> Normalize(IEnumerable<ChatTurn>? history, int maxTurns = MaxTurns) =>
        (history ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t?.Text))
            .Select(t => new ChatTurn
            {
                Role = t.Role == "assistant" ? "assistant" : "user",
                Text = t.Text.Length > MaxCharsPerTurn ? t.Text[..MaxCharsPerTurn] + "…" : t.Text.Trim()
            })
            .TakeLast(Math.Max(0, maxTurns))
            .ToList();

    /// <summary>Câu hỏi gần nhất của người dùng trong lịch sử (null nếu không có).</summary>
    public static string? LastUserQuestion(IEnumerable<ChatTurn>? history) =>
        Normalize(history).LastOrDefault(t => t.Role == "user")?.Text;

    /// <summary>Dấu vân tay ngắn của lịch sử — dùng làm 1 phần khoá cache.</summary>
    public static string Fingerprint(IEnumerable<ChatTurn>? history)
    {
        var turns = Normalize(history);
        if (turns.Count == 0) return "";
        var raw = string.Join("\u001f", turns.Select(t => t.Role + ":" + t.Text));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)))[..16];
    }

    /// <summary>Chuẩn hoá câu hỏi để làm khoá cache: thường hoá, gộp khoảng trắng, bỏ dấu câu cuối.</summary>
    public static string NormalizeQuestion(string? question) =>
        System.Text.RegularExpressions.Regex.Replace((question ?? "").Trim().ToLowerInvariant(), @"\s+", " ").TrimEnd('?', '.', '!', ' ');
}
