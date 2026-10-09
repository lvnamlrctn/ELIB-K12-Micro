using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

public interface IOcrService
{
    Task<List<PageChunk>> OcrPdfAsync(byte[] pdfBuffer, int chunkSize = 500, int overlap = 50);
}

public class OcrService : IOcrService
{
    private readonly string _provider;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int    _timeoutSeconds;
    private readonly ILogger<OcrService> _logger;

    private const string OcrPrompt =
        "Extract all text content from this PDF document. " +
        "Return plain text only, preserving paragraph breaks with newlines. " +
        "No explanations, no markdown formatting, no page labels — just the text.";

    public OcrService(IConfiguration config, ILogger<OcrService> logger)
    {
        _logger         = logger;
        _provider       = config["LLMSettings:Provider"]       ?? "gemini";
        _apiKey         = config["LLMSettings:ApiKey"]         ?? "";
        _model          = config["LLMSettings:Model"]          ?? "gemini-2.0-flash-lite";
        _timeoutSeconds = int.TryParse(config["LLMSettings:TimeoutSeconds"], out var t) ? t : 120;
    }

    public async Task<List<PageChunk>> OcrPdfAsync(byte[] pdfBuffer, int chunkSize = 500, int overlap = 50)
    {
        // Gemini inline_data limit ≈ 20MB; base64 tăng 1.33× → an toàn với buffer ≤ 13MB
        if (pdfBuffer.Length > 13 * 1024 * 1024)
        {
            _logger.LogWarning("PDF quá lớn cho OCR ({Size}MB), bỏ qua", pdfBuffer.Length / 1024 / 1024);
            return [];
        }

        if (_provider.ToLowerInvariant() != "gemini")
        {
            _logger.LogWarning("OCR chỉ hỗ trợ provider=gemini, hiện tại: {Provider}", _provider);
            return [];
        }

        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("LLMSettings:ApiKey chưa cấu hình, bỏ qua OCR");
            return [];
        }

        string text;
        try
        {
            text = await CallGeminiOcrAsync(Convert.ToBase64String(pdfBuffer));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gọi Gemini OCR thất bại");
            return [];
        }

        if (string.IsNullOrWhiteSpace(text)) return [];

        var words  = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        var chunks = SplitIntoChunks(words, chunkSize, overlap);

        _logger.LogInformation("OCR trả về {Words} từ → {Chunks} chunks", words.Count, chunks.Count);

        return chunks.Select((c, i) => new PageChunk
        {
            PageNumber = 0,
            ChunkIndex = i,
            Text       = c
        }).ToList();
    }

    private async Task<string> CallGeminiOcrAsync(string base64Pdf)
    {
        var parts = new List<object>
        {
            new { inline_data = new { mime_type = "application/pdf", data = base64Pdf } },
            new { text = OcrPrompt }
        };

        var payload = new { contents = new[] { new { parts } } };

        using var client = CreateClient("https://generativelanguage.googleapis.com");
        var resp = await PostJsonAsync(client, $"/v1beta/models/{_model}:generateContent?key={_apiKey}", payload);
        var doc  = JsonDocument.Parse(resp);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text").GetString() ?? "";
    }

    private HttpClient CreateClient(string baseUrl)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout     = TimeSpan.FromSeconds(_timeoutSeconds)
        };
    }

    private static async Task<string> PostJsonAsync(HttpClient client, string path, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        const int maxRetries = 3;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp    = await client.PostAsync(path, content);

            if (resp.StatusCode == (System.Net.HttpStatusCode)429 && attempt < maxRetries)
            {
                var delay = resp.Headers.RetryAfter?.Delta
                            ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                await Task.Delay(delay);
                continue;
            }

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }

        throw new HttpRequestException("Max retries exceeded for 429 Too Many Requests");
    }

    private static List<string> SplitIntoChunks(List<string> words, int chunkSize, int overlap)
    {
        var chunks = new List<string>();

        if (words.Count <= chunkSize)
        {
            chunks.Add(string.Join(" ", words));
            return chunks;
        }

        int step = chunkSize - overlap;
        if (step <= 0) step = chunkSize;

        for (int start = 0; start < words.Count; start += step)
        {
            var slice = words.Skip(start).Take(chunkSize).ToList();
            if (slice.Count < 100 && chunks.Count > 0)
            {
                chunks[^1] = chunks[^1] + " " + string.Join(" ", slice);
                break;
            }
            chunks.Add(string.Join(" ", slice));
            if (start + chunkSize >= words.Count) break;
        }

        return chunks;
    }
}
