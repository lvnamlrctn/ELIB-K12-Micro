using System.Text;
using System.Text.Json;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _timeoutSeconds;

    public OllamaEmbeddingService(IConfiguration config)
    {
        _baseUrl        = (config["OllamaSettings:BaseUrl"] ?? "http://localhost:11434").TrimEnd('/');
        _model          = config["OllamaSettings:EmbeddingModel"] ?? "nomic-embed-text";
        _timeoutSeconds = int.TryParse(config["OllamaSettings:TimeoutSeconds"], out var t) ? t : 60;
    }

    // Ollama mặc định num_ctx=2048 cho mọi model dù nomic-embed-text hỗ trợ tới 8192 —
    // phải khai báo rõ num_ctx mới dùng được context dài hơn.
    private const int NumCtx = 8192;
    private const int MaxShrinkAttempts = 6;

    // nomic-embed-text được huấn luyện với prefix theo tác vụ — thiếu prefix làm giảm chất lượng retrieval.
    public Task<float[]> EmbedAsync(string text)      => EmbedInternalAsync("search_document: ", text);
    public Task<float[]> EmbedQueryAsync(string text) => EmbedInternalAsync("search_query: ", text);

    private async Task<float[]> EmbedInternalAsync(string prefix, string text)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout     = TimeSpan.FromSeconds(_timeoutSeconds)
        };

        for (var attempt = 0; attempt < MaxShrinkAttempts; attempt++)
        {
            var payload = new
            {
                model   = _model,
                prompt  = prefix + text,
                options = new { num_ctx = NumCtx }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp    = await client.PostAsync("/api/embeddings", content);
            var body    = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                // Tỷ lệ ký tự/token phụ thuộc nội dung thực tế (văn bản đa dạng tokenize
                // kém hiệu quả hơn text lặp lại) — không đoán ngưỡng ký tự cố định được,
                // nên khi đúng lỗi vượt context thì co ngắn dần và thử lại thay vì bỏ chunk.
                if (body.Contains("exceeds the context length") && attempt < MaxShrinkAttempts - 1)
                {
                    text = text[..(text.Length / 2)];
                    continue;
                }
                throw new HttpRequestException($"Ollama embeddings API {(int)resp.StatusCode} {resp.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var values = doc.RootElement.GetProperty("embedding");

            var result = new float[values.GetArrayLength()];
            int i = 0;
            foreach (var v in values.EnumerateArray())
                result[i++] = v.GetSingle();

            // ES dùng similarity "dot_product" — bắt buộc vector đơn vị; không giả định Ollama đã chuẩn hoá.
            Normalize(result);
            return result;
        }

        throw new HttpRequestException("Vượt quá số lần thử co ngắn prompt do context length");
    }

    private static void Normalize(float[] vector)
    {
        double sumSq = 0;
        foreach (var v in vector) sumSq += (double)v * v;
        if (sumSq <= 0) return;
        var norm = (float)Math.Sqrt(sumSq);
        for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
    }
}
