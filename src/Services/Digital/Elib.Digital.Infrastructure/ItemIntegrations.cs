using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Elib.Digital.Infrastructure;

/// <summary>
/// Tra ISBN (LRC: IsbnMetadataLookupService): Google Books trước, Open Library bổ sung trường còn thiếu. Chỉ gọi hai máy chủ cố định
/// với ISBN đã kiểm định dạng (không có URL do người dùng đưa vào). Có nhớ tạm 24 giờ (không tìm thấy: 10 phút) để không dồn vào nguồn ngoài.
/// </summary>
public sealed partial class IsbnLookup(IHttpClientFactory clients, IMemoryCache cache, ILogger<IsbnLookup> logger) : IIsbnLookup
{
    public const string HttpClientName = "isbn-lookup";

    public async Task<IsbnMetadata?> FindAsync(string isbn, CancellationToken ct)
    {
        if (!IsbnFormat.TryNormalize(isbn, out var normalized))
            throw new BusinessRuleException("ISBN_INVALID", "ISBN không hợp lệ (10 hoặc 13 chữ số, đúng chữ số kiểm).");
        var key = "digital-isbn:" + normalized;
        if (cache.TryGetValue(key, out IsbnMetadata? cached)) return cached;

        var found = await LookupAsync(normalized, ct);
        if (found is null && IsbnFormat.ToIsbn13(normalized) is { } isbn13) found = await LookupAsync(isbn13, ct);
        cache.Set(key, found, found is null ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(24));
        return found;
    }

    private async Task<IsbnMetadata?> LookupAsync(string isbn, CancellationToken ct)
    {
        var google = await GoogleAsync(isbn, ct);
        var openLibrary = await OpenLibraryAsync(isbn, ct);
        if (google is null && openLibrary is null) return null;
        string? Pick(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a : !string.IsNullOrWhiteSpace(b) ? b : null;
        IReadOnlyList<string> PickList(IReadOnlyList<string>? a, IReadOnlyList<string>? b) => a is { Count: > 0 } ? a : b ?? [];
        var source = (google, openLibrary) switch
        {
            ({ }, { }) => "Google Books, Open Library",
            ({ }, null) => "Google Books",
            _ => "Open Library",
        };
        return new IsbnMetadata(
            isbn, Pick(google?.Title, openLibrary?.Title), Pick(google?.Subtitle, openLibrary?.Subtitle), PickList(google?.Authors, openLibrary?.Authors),
            Pick(google?.Publisher, openLibrary?.Publisher), Pick(google?.PublishDate, openLibrary?.PublishDate), google?.Pages ?? openLibrary?.Pages,
            Pick(google?.Language, openLibrary?.Language), Pick(google?.Description, openLibrary?.Description), PickList(google?.Categories, openLibrary?.Categories), source);
    }

    private async Task<IsbnMetadata?> GoogleAsync(string isbn, CancellationToken ct)
    {
        try
        {
            using var response = await clients.CreateClient(HttpClientName)
                .GetAsync(new Uri($"https://www.googleapis.com/books/v1/volumes?q=isbn:{Uri.EscapeDataString(isbn)}&maxResults=1"), ct);
            if (!response.IsSuccessStatusCode) return null;
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0) return null;
            if (!items[0].TryGetProperty("volumeInfo", out var info)) return null;
            return new IsbnMetadata(
                isbn, Text(info, "title"), Text(info, "subtitle"), Strings(info, "authors"), Text(info, "publisher"), Text(info, "publishedDate"),
                info.TryGetProperty("pageCount", out var pages) && pages.ValueKind == JsonValueKind.Number && pages.TryGetInt32(out var n) && n > 0 ? n : null,
                Text(info, "language"), Text(info, "description"), Strings(info, "categories"), "Google Books");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            LogFailed(logger, "Google Books", isbn, ex);
            return null;
        }
    }

    private async Task<IsbnMetadata?> OpenLibraryAsync(string isbn, CancellationToken ct)
    {
        try
        {
            using var response = await clients.CreateClient(HttpClientName)
                .GetAsync(new Uri($"https://openlibrary.org/api/books?bibkeys=ISBN:{Uri.EscapeDataString(isbn)}&format=json&jscmd=data"), ct);
            if (!response.IsSuccessStatusCode) return null;
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("ISBN:" + isbn, out var book)) return null;
            return new IsbnMetadata(
                isbn, Text(book, "title"), Text(book, "subtitle"), Names(book, "authors"), Names(book, "publishers") is { Count: > 0 } p ? p[0] : null, Text(book, "publish_date"),
                book.TryGetProperty("number_of_pages", out var pages) && pages.ValueKind == JsonValueKind.Number && pages.TryGetInt32(out var n) && n > 0 ? n : null,
                null, null, Names(book, "subjects"), "Open Library");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            LogFailed(logger, "Open Library", isbn, ex);
            return null;
        }
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { } s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    private static List<string> Strings(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? [.. v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!.Trim()).Where(s => s.Length > 0)]
            : [];

    private static List<string> Names(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? [.. v.EnumerateArray().Select(x => Text(x, "name")).OfType<string>()]
            : [];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tra ISBN ở {Source} lỗi (ISBN {Isbn})")]
    private static partial void LogFailed(ILogger logger, string source, string isbn, Exception exception);
}

/// <summary>
/// Gợi ý phân loại bằng Gemini (generateContent; khóa gửi qua header x-goog-api-key, không nằm trên URL/log). AI chỉ được chọn mã chủ đề / nhóm ngành
/// trong danh mục của đơn vị — mã ngoài danh mục bị bỏ — và đưa 3–6 từ khóa. Chỉ gửi nhan đề + mô tả, không gửi dữ liệu bạn đọc.
/// </summary>
public sealed partial class GeminiClassificationAi(IHttpClientFactory clients, IOptions<DigitalAiOptions> options, ILogger<GeminiClassificationAi> logger) : IClassificationAi
{
    public const string HttpClientName = "digital-gemini";

    private readonly DigitalAiOptions _options = options.Value;

    public bool Enabled => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<ClassificationSuggestion?> SuggestAsync(
        string title, string? description, IReadOnlyList<ClassificationOption> subjects, IReadOnlyList<ClassificationOption> topics, CancellationToken ct)
    {
        if (!Enabled) return null;
        var prompt = new StringBuilder()
            .AppendLine("Bạn là cán bộ biên mục thư viện số Việt Nam. Dựa vào nhan đề và mô tả tài liệu, hãy chọn:")
            .AppendLine("(1) \"subjectId\": mã chủ đề phù hợp nhất trong danh sách CHỦ ĐỀ, hoặc null nếu không chắc;")
            .AppendLine("(2) \"topicId\": mã nhóm ngành phù hợp nhất trong danh sách NHÓM NGÀNH, hoặc null nếu không chắc;")
            .AppendLine("(3) \"keywords\": 3 đến 6 từ khóa tiếng Việt mô tả chủ đề tài liệu.")
            .AppendLine("Chỉ dùng đúng các mã trong danh sách. Trả về DUY NHẤT một JSON object, không giải thích, không markdown.")
            .AppendLine("CHỦ ĐỀ:");
        foreach (var s in subjects.Take(300)) prompt.Append(CultureInfo.InvariantCulture, $"{s.Id}: {s.Name}\n");
        prompt.AppendLine("NHÓM NGÀNH:");
        foreach (var t in topics.Take(300)) prompt.Append(CultureInfo.InvariantCulture, $"{t.Id}: {t.Name}\n");
        prompt.Append("Nhan đề: ").AppendLine(title.Trim()).Append("Mô tả: ").Append(string.IsNullOrWhiteSpace(description) ? "(không có)" : Truncate(description.Trim(), 2000));

        var payload = new JsonObject
        {
            ["contents"] = new JsonArray { new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt.ToString() } } } },
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0,
                ["maxOutputTokens"] = 400,
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = new JsonObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = new JsonObject
                    {
                        ["subjectId"] = new JsonObject { ["type"] = "INTEGER", ["nullable"] = true },
                        ["topicId"] = new JsonObject { ["type"] = "INTEGER", ["nullable"] = true },
                        ["keywords"] = new JsonObject { ["type"] = "ARRAY", ["items"] = new JsonObject { ["type"] = "STRING" } },
                    },
                    ["required"] = new JsonArray("keywords"),
                },
            },
        };
        var text = "";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent")
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                LogFailed(logger, (int)response.StatusCode, null);
                return null;
            }
            var body = await response.Content.ReadFromJsonAsync<JsonNode>(ct);
            text = body?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
            var json = JsonNode.Parse(StripFence(text));
            var subjectId = Id(json?["subjectId"]);
            var topicId = Id(json?["topicId"]);
            var subject = subjectId is { } sid ? subjects.FirstOrDefault(x => x.Id == sid) : null;
            var topic = topicId is { } tid ? topics.FirstOrDefault(x => x.Id == tid) : null;
            var keywords = json?["keywords"] is JsonArray array
                ? array.Select(k => k?.GetValue<string>()?.Trim()).Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList()
                : [];
            return new ClassificationSuggestion(subject?.Id, subject?.Name, topic?.Id, topic?.Name, keywords);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or FormatException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            LogFailed(logger, 0, ex);
            return null;
        }
    }

    private static long? Id(JsonNode? node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<long>(out var l)) return l;
        return v.TryGetValue<double>(out var d) && d == Math.Floor(d) ? (long)d : null;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static string StripFence(string text)
    {
        var t = text.Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return t;
        var first = t.IndexOf('\n', StringComparison.Ordinal);
        var last = t.LastIndexOf("```", StringComparison.Ordinal);
        return first >= 0 && last > first ? t[(first + 1)..last].Trim() : t;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gợi ý phân loại bằng AI không dùng được (HTTP {Status}) — người dùng nhập tay")]
    private static partial void LogFailed(ILogger logger, int status, Exception? exception);
}

/// <summary>Vector hóa qua Ollama <c>/api/embeddings</c> (LRC: OllamaEmbeddingService, mặc định nomic-embed-text, num_ctx 8192, tiền tố tác vụ). Vector được chuẩn hóa về độ dài 1.</summary>
public sealed class OllamaEmbedder(IHttpClientFactory clients, IOptions<DigitalOllamaOptions> options) : ITextEmbedder
{
    public const string HttpClientName = "digital-ollama";
    private const int MaxShrinkAttempts = 6;

    private readonly DigitalOllamaOptions _options = options.Value;

    public bool Enabled => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<float[]> EmbedAsync(string text, bool query, CancellationToken ct)
    {
        if (!Enabled) throw new BusinessRuleException("EMBEDDING_DISABLED", "Chưa cấu hình dịch vụ vector hóa (Ollama).", 503);
        var prefix = query ? "search_query: " : "search_document: ";
        var http = clients.CreateClient(HttpClientName);
        for (var attempt = 0; attempt < MaxShrinkAttempts; attempt++)
        {
            var payload = new { model = _options.EmbeddingModel, prompt = prefix + text, options = new { num_ctx = 8192 } };
            using var response = await http.PostAsJsonAsync(new Uri("/api/embeddings", UriKind.Relative), payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                // Vượt context: co ngắn dần rồi thử lại thay vì bỏ đoạn (tỉ lệ ký tự/token tùy nội dung).
                if (body.Contains("exceeds the context length", StringComparison.Ordinal) && attempt < MaxShrinkAttempts - 1)
                {
                    text = text[..(text.Length / 2)];
                    continue;
                }
                throw new HttpRequestException($"Ollama embeddings trả {(int)response.StatusCode}.");
            }
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var values = doc.RootElement.GetProperty("embedding");
            var vector = new float[values.GetArrayLength()];
            var i = 0;
            foreach (var v in values.EnumerateArray()) vector[i++] = v.GetSingle();
            return Normalize(vector);
        }
        throw new HttpRequestException("Ollama: vượt giới hạn context sau nhiều lần co ngắn.");
    }

    private static float[] Normalize(float[] vector)
    {
        double sum = 0;
        foreach (var v in vector) sum += (double)v * v;
        if (sum <= 0) return vector;
        var norm = (float)Math.Sqrt(sum);
        for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }
}

/// <summary>Trích lớp văn bản PDF bằng PdfPig (thuần .NET, không cần thư viện hệ thống). PDF quét không có chữ → không có trang nào có văn bản.</summary>
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public IEnumerable<PdfPageText> Extract(Stream pdf, int maxPages)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        PdfDocument document;
        try
        {
            document = PdfDocument.Open(pdf);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidDataException("Không đọc được file PDF.", ex);
        }
        using (document)
        {
            var count = Math.Min(document.NumberOfPages, maxPages);
            for (var number = 1; number <= count; number++)
            {
                string text;
                try
                {
                    text = ContentOrderTextExtractor.GetText(document.GetPage(number)).Trim();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    continue; // một trang hỏng không làm mất cả tài liệu
                }
                if (text.Length > 0) yield return new PdfPageText(number, text);
            }
        }
    }
}

/// <summary>
/// Đọc/ghi file tài liệu số qua /internal của media (service token): luồng đầy đủ, không qua URL ký công khai (/s3 chỉ có ý nghĩa với trình duyệt).
/// Client này KHÔNG đi qua ServiceTokenHandler vì handler đó đệm toàn bộ nội dung vào bộ nhớ để gửi lại khi gặp 401 — với file tới 200 MB là không chấp nhận được.
/// Token được gắn tại đây; đọc (GET) thử lại một lần khi 401, tải lên (luồng chỉ đọc được một lần) thì báo thử lại sau và lần sau lấy token mới.
/// </summary>
public sealed class HttpMediaFileStore(IHttpClientFactory clients, ITenantContext tenant, ServiceTokenProvider tokens) : IMediaFileStore
{
    public const string HttpClientName = "elib-media-stream";

    private sealed record UploadedFile(Guid FileId);

    public async Task<Stream> OpenReadAsync(Guid mediaFileId, CancellationToken ct)
    {
        var url = $"internal/tenants/{tenant.RequireTenantId()}/ebook-files/{mediaFileId}/content";
        HttpResponseMessage response;
        try
        {
            response = await SendReadAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                response = await SendReadAsync(url, ct, refreshToken: true);
            }
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("MEDIA_UNAVAILABLE", "Không kết nối được kho file để đọc nội dung — thử lại sau.", 503);
        }
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            throw new NotFoundException("File", mediaFileId);
        }
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new BusinessRuleException("MEDIA_UNAVAILABLE", $"Kho file báo lỗi {status} khi đọc nội dung — thử lại sau.", 503);
        }
        return new ResponseStream(await response.Content.ReadAsStreamAsync(ct), response);
    }

    public async Task<Guid> UploadAsync(string fileName, long size, Stream content, CancellationToken ct)
    {
        var url = $"internal/tenants/{tenant.RequireTenantId()}/ebook-files?fileName={Uri.EscapeDataString(fileName)}";
        using var body = new StreamContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        body.Headers.ContentLength = size;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Relative)) { Content = body };
        var token = await tokens.GetTokenAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient(HttpClientName).SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("MEDIA_UNAVAILABLE", "Không kết nối được kho file để tải lên — thử lại sau.", 503);
        }
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                tokens.Invalidate(token); // luồng đã dùng, không gửi lại được — lần tải sau dùng token mới
                throw new BusinessRuleException("MEDIA_UNAVAILABLE", "Kho file từ chối phiên làm việc của dịch vụ — thử lại sau.", 503);
            }
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadFromJsonAsync<ErrorBody>(ct);
                throw new BusinessRuleException(detail?.Code ?? "MEDIA_UNAVAILABLE", detail?.Detail ?? "Kho file từ chối file này.", response.StatusCode == HttpStatusCode.ServiceUnavailable ? 503 : 400);
            }
            return (await response.Content.ReadFromJsonAsync<UploadedFile>(ct))?.FileId ?? throw new BusinessRuleException("MEDIA_UNAVAILABLE", "Kho file trả về dữ liệu không hợp lệ.", 503);
        }
    }

    private async Task<HttpResponseMessage> SendReadAsync(string url, CancellationToken ct, bool refreshToken = false)
    {
        var token = await tokens.GetTokenAsync(ct);
        if (refreshToken)
        {
            tokens.Invalidate(token);
            token = await tokens.GetTokenAsync(ct);
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await clients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private sealed record ErrorBody(string? Code, string? Detail);

    /// <summary>Luồng đọc giữ phản hồi HTTP đến khi đóng luồng.</summary>
    private sealed class ResponseStream(Stream inner, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
