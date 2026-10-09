using System.Text.Json;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

public class BookCoverLookupService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<BookCoverLookupService> logger)
    : IBookCoverLookupService
{
    public async Task<string?> FindCoverUrlAsync(string isbn)
    {
        return await TryGoogleBooksAsync(isbn) ?? await TryOpenLibraryAsync(isbn);
    }

    private async Task<string?> TryGoogleBooksAsync(string isbn)
    {
        try
        {
            var apiKey = config["BookCoverSettings:GoogleBooksApiKey"];
            var url = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{Uri.EscapeDataString(isbn)}"
                + (string.IsNullOrWhiteSpace(apiKey) ? "" : $"&key={apiKey}");

            using var client = httpFactory.CreateClient();
            using var res = await client.GetAsync(url);
            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0) return null;

            var imageLinks = items[0].GetProperty("volumeInfo").TryGetProperty("imageLinks", out var il) ? il : default;
            if (imageLinks.ValueKind != JsonValueKind.Object) return null;

            if (imageLinks.TryGetProperty("thumbnail", out var thumb)) return thumb.GetString();
            if (imageLinks.TryGetProperty("smallThumbnail", out var small)) return small.GetString();
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BookCoverLookupService: Google Books lookup thất bại cho ISBN {Isbn}", isbn);
            return null;
        }
    }

    private async Task<string?> TryOpenLibraryAsync(string isbn)
    {
        try
        {
            var probeUrl = $"https://covers.openlibrary.org/b/isbn/{Uri.EscapeDataString(isbn)}-L.jpg?default=false";
            using var client = httpFactory.CreateClient();
            using var res = await client.GetAsync(probeUrl);
            if (!res.IsSuccessStatusCode) return null;

            return $"https://covers.openlibrary.org/b/isbn/{Uri.EscapeDataString(isbn)}-L.jpg";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BookCoverLookupService: OpenLibrary lookup thất bại cho ISBN {Isbn}", isbn);
            return null;
        }
    }
}
