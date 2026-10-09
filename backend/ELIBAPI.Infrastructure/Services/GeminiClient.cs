using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

/// <inheritdoc cref="IGeminiClient"/>
/// <remarks>
/// Dùng HttpClient từ <see cref="IHttpClientFactory"/> (named client <see cref="HttpClientName"/>) để tái sử dụng
/// kết nối TLS giữa các lượt gọi; key gửi qua header <c>x-goog-api-key</c> (không lộ trên URL/log). Thử lại 1 lần
/// khi Gemini quá tải (429/503). Mỗi lượt gọi ghi log độ trễ + số token để đo chi phí.
/// </remarks>
public class GeminiClient(IConfiguration config, IHttpClientFactory httpFactory, ILogger<GeminiClient> logger) : IGeminiClient
{
    public const string HttpClientName = "gemini";

    private string? _apiKey;
    // Model không nhận thinkingConfig → nhớ lại để các lượt sau khỏi gửi (tránh 1 request 400 mỗi lần).
    private volatile bool _thinkingUnsupported;

    private string Model => config["LLMSettings:Model"] ?? "gemini-2.0-flash-lite";

    private string ApiKey => _apiKey ??= DecryptKey(config["LLMSettings:ApiKey"] ?? "");

    private static string DecryptKey(string raw) => raw.StartsWith("ENC:") ? AesEncryptionHelper.Decrypt(raw[4..]) : raw;

    public async Task<JsonElement> GenerateAsync(object payload, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        using var resp = await SendAsync("generateContent", payload, HttpCompletionOption.ResponseContentRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        LogUsage(doc.RootElement, "generate", sw.ElapsedMilliseconds);
        return PickPart(doc.RootElement);
    }

    public async IAsyncEnumerable<string> StreamTextAsync(object payload, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        using var resp = await SendAsync("streamGenerateContent?alt=sse", payload, HttpCompletionOption.ResponseHeadersRead, ct);
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        long? firstTokenMs = null;
        string? lastChunk = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var json = line[5..].Trim();
            if (json.Length == 0) continue;
            lastChunk = json;
            foreach (var text in TextsFromChunk(json))
            {
                firstTokenMs ??= sw.ElapsedMilliseconds;
                yield return text;
            }
        }
        if (lastChunk != null)
        {
            try { using var doc = JsonDocument.Parse(lastChunk); LogUsage(doc.RootElement, $"stream (first text {firstTokenMs} ms)", sw.ElapsedMilliseconds); }
            catch (JsonException) { /* chỉ để log */ }
        }
    }

    /// <summary>Đoạn text (bỏ phần "thought") trong 1 sự kiện SSE của streamGenerateContent.</summary>
    public static IEnumerable<string> TextsFromChunk(string json)
    {
        List<string> texts = [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("candidates", out var cands) || cands.ValueKind != JsonValueKind.Array || cands.GetArrayLength() == 0)
                return texts;
            if (!cands[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)) return texts;
            foreach (var p in parts.EnumerateArray())
            {
                if (p.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True) continue;
                if (p.TryGetProperty("text", out var t) && t.GetString() is { Length: > 0 } s) texts.Add(s);
            }
        }
        catch (JsonException) { }
        return texts;
    }

    /// <summary>Part <c>functionCall</c> nếu có; ngược lại gộp mọi part text (bỏ "thought") thành <c>{text}</c>.</summary>
    public static JsonElement PickPart(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var cands) || cands.ValueKind != JsonValueKind.Array || cands.GetArrayLength() == 0
            || !cands[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
        {
            var reason = root.TryGetProperty("promptFeedback", out var fb) ? fb.ToString() : "không có candidates";
            throw new HttpRequestException($"Gemini không trả nội dung: {reason}");
        }

        var text = new StringBuilder();
        foreach (var p in parts.EnumerateArray())
        {
            if (p.TryGetProperty("functionCall", out _)) return p.Clone();
            if (p.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True) continue;
            if (p.TryGetProperty("text", out var t)) text.Append(t.GetString());
        }
        return JsonSerializer.SerializeToElement(new { text = text.ToString() });
    }

    private async Task<HttpResponseMessage> SendAsync(string action, object payload, HttpCompletionOption completion, CancellationToken ct)
    {
        var node = JsonSerializer.SerializeToNode(payload) as JsonObject ?? throw new ArgumentException("Payload Gemini phải là object", nameof(payload));
        var model = Model;
        // thinkingLevel chỉ có ở Gemini 3.x — model khác bỏ luôn để khỏi bị 400.
        if (_thinkingUnsupported || !model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase)) StripThinking(node);

        var http = httpFactory.CreateClient(HttpClientName);
        for (var attempt = 0; ; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/v1beta/models/{model}:{action}")
            {
                Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("x-goog-api-key", ApiKey);
            var resp = await http.SendAsync(req, completion, ct);
            if (resp.IsSuccessStatusCode) return resp;

            var body = await resp.Content.ReadAsStringAsync(ct);
            var status = resp.StatusCode;
            resp.Dispose();

            if (status == HttpStatusCode.BadRequest && body.Contains("thinking", StringComparison.OrdinalIgnoreCase) && StripThinking(node))
            {
                _thinkingUnsupported = true;
                logger.LogInformation("Gemini {Model} không nhận thinkingConfig — gửi lại không kèm", model);
                continue;
            }
            if (attempt == 0 && status is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                continue;
            }
            throw new HttpRequestException($"Gemini API {(int)status} {status}: {Truncate(body, 500)}", null, status);
        }
    }

    private static bool StripThinking(JsonObject node) =>
        (node["generationConfig"] as JsonObject)?.Remove("thinkingConfig") == true
        | (node["generation_config"] as JsonObject)?.Remove("thinkingConfig") == true;

    private void LogUsage(JsonElement root, string kind, long ms)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        int Get(JsonElement u, string name) => u.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
        if (root.TryGetProperty("usageMetadata", out var u))
            logger.LogInformation("Gemini {Kind} {Model}: {Ms} ms, prompt {Prompt} tok, output {Output} tok, thinking {Thoughts} tok",
                kind, Model, ms, Get(u, "promptTokenCount"), Get(u, "candidatesTokenCount"), Get(u, "thoughtsTokenCount"));
        else
            logger.LogInformation("Gemini {Kind} {Model}: {Ms} ms", kind, Model, ms);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
