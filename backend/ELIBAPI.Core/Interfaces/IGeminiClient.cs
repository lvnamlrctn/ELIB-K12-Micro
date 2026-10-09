using System.Text.Json;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Lớp gọi Gemini <c>generateContent</c> dùng chung cho mọi tính năng LLM.
///
/// Tách ra vì cả trợ lý nội dung (<c>IChatService</c>) lẫn trợ lý tìm tài liệu
/// (<c>IDocumentFinderService</c>) đều cần đúng một thứ: đọc khoá <c>LLMSettings</c>, giải mã khoá
/// dạng <c>ENC:</c>, gọi endpoint và bóc phần trả lời. Nhân đôi đoạn đó đồng nghĩa với việc đổi model
/// hay đổi cách lưu khoá phải sửa hai nơi.
/// </summary>
public interface IGeminiClient
{
    /// <summary>
    /// Gửi payload thô của Gemini <c>generateContent</c>, trả về 1 part của <c>candidates[0].content.parts</c>:
    /// part <c>{functionCall}</c> nếu có, ngược lại <c>{text}</c> ghép từ mọi part text.
    /// Ném <see cref="HttpRequestException"/> khi Gemini trả mã lỗi; bên gọi tự quyết định
    /// cách suy giảm dịch vụ.
    /// </summary>
    Task<JsonElement> GenerateAsync(object payload, CancellationToken ct = default);

    /// <summary>
    /// Như <see cref="GenerateAsync"/> nhưng qua <c>streamGenerateContent</c> (SSE): trả dần từng đoạn text
    /// ngay khi Gemini sinh ra — dùng cho chat hiển thị chữ dần. Payload không nên kèm tools.
    /// </summary>
    IAsyncEnumerable<string> StreamTextAsync(object payload, CancellationToken ct = default);
}

/// <summary>
/// <c>generationConfig</c> dùng chung cho các tính năng chat. <c>thinkingConfig</c> luôn được gửi ở mức
/// "minimal" — trợ lý thư viện không cần model "suy nghĩ" lâu (tốn token + chậm); GeminiClient tự bỏ
/// trường này nếu model không hỗ trợ.
/// </summary>
public static class GeminiGeneration
{
    public static object Config(double temperature, int maxOutputTokens) => new
    {
        temperature,
        maxOutputTokens,
        thinkingConfig = new { thinkingLevel = "minimal" }
    };
}
