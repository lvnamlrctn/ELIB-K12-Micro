using System.Text.Json;
using ELIBAPI.Core.DTOs.Chat;

namespace ELIBAPI.API.Infrastructure;

/// <summary>
/// Ghi chuỗi <see cref="ChatStreamEvent"/> ra response dạng Server-Sent Events cho các khung chat trả dần.
/// Mỗi sự kiện: <c>event: {type}</c> + <c>data: {json}</c>. <c>X-Accel-Buffering: no</c> để nginx (elib-demo) không
/// gom buffer làm mất hiệu ứng chữ hiện dần.
/// </summary>
public static class ServerSentEvents
{
    // Không escape tiếng Việt thành \uXXXX — gói nhỏ hơn; JSON vẫn hợp lệ (dấu nháy, xuống dòng vẫn được escape).
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task WriteAsync(HttpResponse response, IAsyncEnumerable<ChatStreamEvent> events, CancellationToken ct)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        await response.Body.FlushAsync(ct);

        try
        {
            await foreach (var e in events.WithCancellation(ct))
                await WriteEventAsync(response, e, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Người dùng bấm "Dừng"/đóng trang — không còn ai nhận, bỏ qua.
        }
    }

    public static async Task WriteEventAsync(HttpResponse response, ChatStreamEvent e, CancellationToken ct)
    {
        await response.WriteAsync(Format(e), ct);
        await response.Body.FlushAsync(ct);
    }

    /// <summary>Định dạng 1 sự kiện SSE (tách riêng để test).</summary>
    public static string Format(ChatStreamEvent e)
    {
        object data = e.Type == "sources" ? new { sources = e.Data } : new { text = e.Text };
        return $"event: {e.Type}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n";
    }
}
