using System.Text;
using System.Text.Json;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

public class EmbeddingService : IEmbeddingService
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com";

    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _dimensions;
    private readonly int _timeoutSeconds;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _logger = logger;
        var key = config["LLMSettings:ApiKey"] ?? "";
        _apiKey = key.StartsWith("ENC:") ? AesEncryptionHelper.Decrypt(key[4..]) : key;
        // text-embedding-004 đã bị Google gỡ trên các API key thế hệ mới (404).
        // gemini-embedding-001 là model GA thay thế; mặc định 3072 chiều nên phải
        // ép outputDimensionality khớp mapping dense_vector của Elasticsearch (768).
        _model = config["LLMSettings:EmbeddingModel"] ?? "gemini-embedding-001";
        _dimensions = int.TryParse(config["LLMSettings:EmbeddingDimensions"], out var d) ? d : 768;
        _timeoutSeconds = int.TryParse(config["LLMSettings:TimeoutSeconds"], out var t) ? t : 60;
    }

    public Task<float[]> EmbedAsync(string text)      => EmbedInternalAsync(text, "RETRIEVAL_DOCUMENT");
    public Task<float[]> EmbedQueryAsync(string text) => EmbedInternalAsync(text, "RETRIEVAL_QUERY");

    private async Task<float[]> EmbedInternalAsync(string text, string taskType)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("LLMSettings:ApiKey chưa cấu hình, bỏ qua embedding");
            return [];
        }

        var payload = new
        {
            model                = $"models/{_model}",
            content              = new { parts = new[] { new { text } } },
            taskType,
            outputDimensionality = _dimensions
        };

        using var client = CreateClient();
        var body = await PostJsonAsync(client,
            $"/v1beta/models/{_model}:embedContent?key={_apiKey}", payload);

        using var doc = JsonDocument.Parse(body);
        var values = doc.RootElement
            .GetProperty("embedding")
            .GetProperty("values");

        var result = new float[values.GetArrayLength()];
        int i = 0;
        foreach (var v in values.EnumerateArray())
            result[i++] = v.GetSingle();

        // gemini-embedding-001 chỉ chuẩn hóa sẵn khi trả đủ 3072 chiều; ở chiều rút gọn
        // (768) vector chưa chuẩn hóa. Elasticsearch dùng similarity "dot_product" nên
        // BẮT BUỘC vector đơn vị — chuẩn hóa L2 tại đây cho cả document lẫn query.
        Normalize(result);
        return result;
    }

    private static void Normalize(float[] vector)
    {
        double sumSq = 0;
        foreach (var v in vector) sumSq += (double)v * v;
        if (sumSq <= 0) return;
        var norm = (float)Math.Sqrt(sumSq);
        for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
    }

    private HttpClient CreateClient() => new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout     = TimeSpan.FromSeconds(_timeoutSeconds)
    };

    private static async Task<string> PostJsonAsync(HttpClient client, string path, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        const int maxRetries = 3;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp    = await client.PostAsync(path, content);
            var respBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == (System.Net.HttpStatusCode)429 && attempt < maxRetries)
            {
                var delay = resp.Headers.RetryAfter?.Delta
                            ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                await Task.Delay(delay);
                continue;
            }

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Embedding API {(int)resp.StatusCode} {resp.StatusCode}: {respBody}");

            return respBody;
        }

        throw new HttpRequestException("Max retries exceeded for 429 Too Many Requests");
    }
}
