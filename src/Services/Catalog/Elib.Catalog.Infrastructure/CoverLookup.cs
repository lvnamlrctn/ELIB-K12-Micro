using System.Globalization;
using System.Text.Json;
using Elib.Catalog.Application;
using Microsoft.Extensions.Logging;

namespace Elib.Catalog.Infrastructure;

/// <summary>
/// Ảnh bìa theo ISBN (monolith: BookCoverLookupService): Google Books trước, không có thì Open Library. Chỉ trả URL https — OPAC
/// chạy https nên ảnh http bị trình duyệt chặn (Google Books trả "http://books.google.com/…", đổi sang https).
/// </summary>
public sealed partial class CoverLookup(HttpClient http, ILogger<CoverLookup> logger) : ICoverLookup
{
    public const string HttpClientName = "cover-lookup";

    public async Task<string?> FindByIsbnAsync(string isbn, CancellationToken ct) =>
        await GoogleBooksAsync(isbn, ct) ?? await OpenLibraryAsync(isbn, ct);

    private async Task<string?> GoogleBooksAsync(string isbn, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(new Uri($"https://www.googleapis.com/books/v1/volumes?q=isbn:{Uri.EscapeDataString(isbn)}"), ct);
            if (!response.IsSuccessStatusCode) return null;
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0) return null;
            if (!items[0].TryGetProperty("volumeInfo", out var info) || !info.TryGetProperty("imageLinks", out var links)) return null;
            foreach (var name in new[] { "thumbnail", "smallThumbnail" })
                if (links.TryGetProperty(name, out var link) && link.ValueKind == JsonValueKind.String && Https(link.GetString()) is { } url)
                    return url;
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            LogLookupFailed(logger, "Google Books", isbn, ex);
            return null;
        }
    }

    private async Task<string?> OpenLibraryAsync(string isbn, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"https://covers.openlibrary.org/b/isbn/{Uri.EscapeDataString(isbn)}-L.jpg");
        try
        {
            // default=false: không có ảnh → 404 thay vì ảnh trống 1×1.
            using var response = await http.GetAsync(new Uri(url + "?default=false"), HttpCompletionOption.ResponseHeadersRead, ct);
            return response.IsSuccessStatusCode ? url : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogLookupFailed(logger, "Open Library", isbn, ex);
            return null;
        }
    }

    private static string? Https(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme == Uri.UriSchemeHttps) return uri.AbsoluteUri;
        return uri.Scheme == Uri.UriSchemeHttp ? new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tra ảnh bìa ở {Source} lỗi (ISBN {Isbn})")]
    private static partial void LogLookupFailed(ILogger logger, string source, string isbn, Exception exception);
}
