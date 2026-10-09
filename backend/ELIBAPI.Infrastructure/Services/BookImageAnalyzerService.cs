using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Services;

public class BookImageAnalyzerService : IBookImageAnalyzerService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _provider;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly int    _timeoutSeconds;

    private const string Prompt =
        "Analyze the book image(s) and extract bibliographic metadata. " +
        "Return ONLY a JSON object with these fields: " +
        "title, author (comma-separated if multiple), publisher, publishYear (4-digit), " +
        "isbn (if visible), language (ISO 639-1 code), description (brief if visible). " +
        "If a field is not visible, set it to null. No explanation, no markdown, just the JSON object.";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public BookImageAnalyzerService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory    = httpFactory;
        _provider       = config["LLMSettings:Provider"]    ?? "ollama";
        _apiKey         = config["LLMSettings:ApiKey"]      ?? "";
        _model          = config["LLMSettings:Model"]       ?? "llava";
        _baseUrl        = config["LLMSettings:BaseUrl"]     ?? "http://localhost:11434";
        _timeoutSeconds = int.TryParse(config["LLMSettings:TimeoutSeconds"], out var t) ? t : 60;
    }

    public async Task<BookMetadataResult> AnalyzeAsync(IEnumerable<Stream> images)
    {
        var base64List = new List<(string Data, string MimeType)>();
        foreach (var stream in images)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            base64List.Add((Convert.ToBase64String(ms.ToArray()), "image/jpeg"));
        }

        var text = _provider.ToLowerInvariant() switch
        {
            "claude" => await CallClaudeAsync(base64List),
            "openai" => await CallOpenAiAsync(base64List),
            "gemini" => await CallGeminiAsync(base64List),
            _        => await CallOllamaAsync(base64List),
        };

        return ParseResult(text);
    }

    private async Task<string> CallOllamaAsync(List<(string Data, string MimeType)> images)
    {
        var payload = new
        {
            model    = _model,
            stream   = false,
            messages = new[]
            {
                new
                {
                    role    = "user",
                    content = Prompt,
                    images  = images.Select(i => i.Data).ToArray()
                }
            }
        };

        using var client = CreateClient(_baseUrl);
        var resp = await PostJsonAsync(client, "/api/chat", payload);
        var doc  = JsonDocument.Parse(resp);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task<string> CallClaudeAsync(List<(string Data, string MimeType)> images)
    {
        var contentParts = new List<object>();
        foreach (var (data, mimeType) in images)
            contentParts.Add(new { type = "image", source = new { type = "base64", media_type = mimeType, data } });
        contentParts.Add(new { type = "text", text = Prompt });

        var payload = new
        {
            model      = _model,
            max_tokens = 1024,
            messages   = new[] { new { role = "user", content = contentParts } }
        };

        using var client = CreateClient("https://api.anthropic.com");
        client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var resp = await PostJsonAsync(client, "/v1/messages", payload);
        var doc  = JsonDocument.Parse(resp);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    private async Task<string> CallOpenAiAsync(List<(string Data, string MimeType)> images)
    {
        var contentParts = new List<object>();
        foreach (var (data, mimeType) in images)
            contentParts.Add(new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{data}" } });
        contentParts.Add(new { type = "text", text = Prompt });

        var payload = new
        {
            model      = _model,
            max_tokens = 1024,
            messages   = new[] { new { role = "user", content = contentParts } }
        };

        using var client = CreateClient("https://api.openai.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var resp = await PostJsonAsync(client, "/v1/chat/completions", payload);
        var doc  = JsonDocument.Parse(resp);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task<string> CallGeminiAsync(List<(string Data, string MimeType)> images)
    {
        var parts = new List<object>();
        foreach (var (data, mimeType) in images)
            parts.Add(new { inline_data = new { mime_type = mimeType, data } });
        parts.Add(new { text = Prompt });

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
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout     = TimeSpan.FromSeconds(_timeoutSeconds)
        };
        return client;
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

    private static BookMetadataResult ParseResult(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence    = trimmed.LastIndexOf("```");
            if (firstNewline >= 0 && lastFence > firstNewline)
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            return JsonSerializer.Deserialize<BookMetadataResult>(trimmed, JsonOpts) ?? new BookMetadataResult();
        }
        catch
        {
            return new BookMetadataResult { Description = text };
        }
    }
}
