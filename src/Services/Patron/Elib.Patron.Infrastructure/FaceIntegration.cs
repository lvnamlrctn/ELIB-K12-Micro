using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elib.BuildingBlocks.Tenancy;
using Elib.Patron.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Patron.Infrastructure;

/// <summary>GET /internal/tenants/{tenantId}/files/{id}/content của service media (service token) — chỉ ảnh bạn đọc của đơn vị hiện tại.</summary>
public sealed class HttpReaderPhotoSource(IHttpClientFactory clients, ITenantContext tenant) : IReaderPhotoSource
{
    public const string HttpClientName = "elib-media";

    public async Task<byte[]?> ReadAsync(Guid fileId, CancellationToken ct)
    {
        var path = $"internal/tenants/{tenant.RequireTenantId()}/files/{fileId}/content";
        try
        {
            using var response = await clients.CreateClient(HttpClientName).GetAsync(new Uri(path, UriKind.Relative), ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null; // ảnh đã bị xoá khỏi media — bỏ qua
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new FaceServiceException("Không đọc được ảnh bạn đọc từ media.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new FaceServiceException("Media phản hồi quá lâu.", ex);
        }
    }
}

/// <summary>
/// So khớp khuôn mặt bằng Gemini (generateContent, khoá gửi qua header x-goog-api-key — không nằm trên URL/log). Ảnh #0 là ảnh camera,
/// #1..#n là ảnh bạn đọc theo đúng thứ tự; Gemini trả JSON {matchIndex, confidence}. Thử lại một lần khi quá tải (429/503). Không ghi log
/// nội dung ảnh hay câu trả lời.
/// </summary>
public sealed partial class GeminiFaceMatcher(IHttpClientFactory clients, IOptions<FaceRecognitionOptions> options, ILogger<GeminiFaceMatcher> logger) : IFaceMatcher
{
    public const string HttpClientName = "gemini";

    private static string Prompt(int n) =>
        FormattableString.Invariant($"Bạn là hệ thống nhận diện khuôn mặt. Ảnh #0 là ẢNH CAMERA cần nhận diện. Các ảnh #1 đến #{n} lần lượt là ảnh đại diện của {n} bạn đọc đã đăng ký, theo đúng thứ tự. ") +
        FormattableString.Invariant($"Xác định ảnh camera có khớp với đúng 1 trong số các ảnh #1..#{n} không (cùng một người). Chỉ trả lời khớp khi bạn thực sự chắc chắn. ") +
        "Trả lời DUY NHẤT bằng JSON thuần, không markdown, không giải thích, đúng định dạng: " +
        FormattableString.Invariant($$"""{"matchIndex": <số thứ tự ảnh khớp nhất trong khoảng 1..{{n}}, hoặc null nếu không khớp ai>, "confidence": <số thực 0.0 đến 1.0>}""");

    private readonly FaceRecognitionOptions _options = options.Value;

    public async Task<FaceBatchMatch> MatchAsync(byte[] camera, IReadOnlyList<byte[]> candidates, CancellationToken ct)
    {
        var parts = new JsonArray { new JsonObject { ["text"] = Prompt(candidates.Count) } };
        parts.Add(Image(camera));
        foreach (var c in candidates) parts.Add(Image(c));
        var payload = new JsonObject { ["contents"] = new JsonArray { new JsonObject { ["parts"] = parts } } }.ToJsonString();

        var http = clients.CreateClient(HttpClientName);
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1beta/models/{_options.Model}:generateContent")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
            try
            {
                using var response = await http.SendAsync(request, ct);
                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable && attempt == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    LogStatus((int)response.StatusCode);
                    throw new FaceServiceException($"Dịch vụ nhận diện trả lỗi {(int)response.StatusCode}.");
                }
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                return Parse(ExtractText(body));
            }
            catch (HttpRequestException ex)
            {
                throw new FaceServiceException("Không kết nối được dịch vụ nhận diện.", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new FaceServiceException("Dịch vụ nhận diện phản hồi quá lâu.", ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gemini nhận diện khuôn mặt trả {Status}")]
    private partial void LogStatus(int status);

    private static JsonObject Image(byte[] bytes) => new()
    {
        ["inline_data"] = new JsonObject { ["mime_type"] = MimeOf(bytes), ["data"] = Convert.ToBase64String(bytes) },
    };

    private static string MimeOf(byte[] b) =>
        b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 ? "image/png"
        : b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' ? "image/webp"
        : "image/jpeg";

    private static string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var cands) || cands.ValueKind != JsonValueKind.Array || cands.GetArrayLength() == 0
            || !cands[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
            return "";
        var text = new StringBuilder();
        foreach (var p in parts.EnumerateArray())
        {
            if (p.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True) continue;
            if (p.TryGetProperty("text", out var t)) text.Append(t.GetString());
        }
        return text.ToString();
    }

    /// <summary>JSON thuần hoặc bọc trong khối ``` — không đọc được thì coi như không khớp.</summary>
    public static FaceBatchMatch Parse(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var newline = trimmed.IndexOf('\n');
            trimmed = newline >= 0 ? trimmed[(newline + 1)..] : trimmed;
            var fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) trimmed = trimmed[..fence];
            trimmed = trimmed.Trim();
        }
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            int? index = root.TryGetProperty("matchIndex", out var mi) && mi.ValueKind == JsonValueKind.Number && mi.TryGetInt32(out var i) ? i : null;
            var confidence = root.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number ? cf.GetDouble() : 0;
            return new FaceBatchMatch(index, confidence);
        }
        catch (JsonException)
        {
            return new FaceBatchMatch(null, 0);
        }
    }
}
