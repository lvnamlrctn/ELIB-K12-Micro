using System.Linq.Expressions;
using Elib.BuildingBlocks.Domain;
using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Search.Application;

/// <summary>
/// Tra cứu OPAC (monolith: PublicUnifiedSearch — phần sách in). <see cref="Q"/>: tìm nhanh mọi trường; các ô nâng cao tìm theo đúng
/// trường. Không phân biệt dấu/hoa thường, mọi từ phải có mặt. Facet chọn nhiều giá trị (Authors/Years/MaterialTypes/Languages/Stores).
/// Sort: relevance (mặc định) | newest | oldest | title.
/// </summary>
public sealed record OpacSearchRequest
{
    public string? Q { get; init; }
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Publisher { get; init; }
    public string? Keyword { get; init; }
    public string? Isbn { get; init; }
    public string? Ddc { get; init; }

    /// <summary>Số ĐKCB (đầu mã).</summary>
    public string? Barcode { get; init; }

    public int? YearFrom { get; init; }
    public int? YearTo { get; init; }
    public IReadOnlyList<int>? Years { get; init; }
    public IReadOnlyList<string>? Authors { get; init; }
    public IReadOnlyList<string>? MaterialTypes { get; init; }
    public IReadOnlyList<string>? Languages { get; init; }
    public IReadOnlyList<string>? Stores { get; init; }
    public bool AvailableOnly { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed record OpacBib(
    Guid PublicId, long Mfn, string Title, string? Author, string? Publisher, string? PublishYear, string? MaterialType, string? Language,
    string? Ddc, string? Isbns, string? Summary, int Copies, int Available);

public sealed record FacetCount(string Key, int Count);

public sealed record OpacFacets(
    IReadOnlyList<FacetCount> Years, IReadOnlyList<FacetCount> Authors, IReadOnlyList<FacetCount> MaterialTypes, IReadOnlyList<FacetCount> Languages,
    IReadOnlyList<FacetCount> Stores, int AvailableCount);

public sealed record OpacSearchResult(int Total, int Page, int PageSize, IReadOnlyList<OpacBib> Items, OpacFacets Facets);

/// <summary>Bản sách trên trang chi tiết: trạng thái hiển thị "Sẵn sàng" / "Đang mượn" (kèm hạn trả) / "Đang xử lý".</summary>
public sealed record OpacCopy(string Barcode, string? StoreName, string Status, string StatusName, DateTimeOffset? DueAt);

public sealed record OpacBibDetail(
    Guid PublicId, long Mfn, string Title, string? Author, string? OtherAuthors, string? Publisher, string? PublishPlace, string? PublishYear,
    string? Edition, string? PhysicalDescription, string? Series, string? MaterialType, string? Language, string? Ddc, string? Cutter,
    string? Isbns, string? Keywords, string? Summary, int Copies, int Available, IReadOnlyList<OpacCopy> Holdings);

/// <summary>Tra cứu công khai trên chỉ mục của search (chỉ biểu ghi hiện trên OPAC — Status 2, chưa xoá) của đơn vị theo host.</summary>
public sealed class OpacSearch(ISearchDb db)
{
    public const int MaxPageSize = 50;
    private const int FacetSize = 15;

    private IQueryable<SearchBib> Visible => db.Set<SearchBib>().AsNoTracking().Where(b => !b.Deleted && b.Status == SearchBib.OpacVisible);

    private IQueryable<SearchItem> Copies => db.Set<SearchItem>().AsNoTracking().Where(i => !i.Deleted && SearchItem.OpacStatuses.Contains(i.Status));

    public async Task<OpacSearchResult> SearchAsync(OpacSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var query = Filter(request);

        var total = await query.CountAsync(ct);
        var phrase = TextFold.Fold(request.Q ?? request.Title);
        var items = await Order(query, request.Sort, phrase).Skip((page - 1) * size).Take(size).Select(Projection).ToListAsync(ct);
        return new OpacSearchResult(total, page, size, items, await FacetsAsync(query, ct));
    }

    public async Task<OpacBibDetail> DetailAsync(Guid publicId, CancellationToken ct)
    {
        var b = await Visible.FirstOrDefaultAsync(x => x.BibPublicId == publicId, ct) ?? throw new NotFoundException("Tài liệu", publicId);
        var copies = await Copies.Where(i => i.Mfn == b.Mfn).OrderBy(i => i.BarcodeKey).Take(500).ToListAsync(ct);
        var holdings = copies.Select(i => i.Status != "R"
                ? new OpacCopy(i.Barcode, i.StoreName, "processing", "Đang xử lý", null)
                : i.OnLoan ? new OpacCopy(i.Barcode, i.StoreName, "on-loan", "Đang mượn", i.LoanDueAt)
                : new OpacCopy(i.Barcode, i.StoreName, "available", "Sẵn sàng", null))
            .ToList();
        return new OpacBibDetail(b.BibPublicId, b.Mfn, b.Title, b.Author, b.OtherAuthors, b.Publisher, b.PublishPlace, b.PublishYear, b.Edition,
            b.PhysicalDescription, b.Series, b.MaterialType, b.Language, b.Ddc, b.Cutter, b.Isbns, b.Keywords, b.Summary,
            holdings.Count, holdings.Count(h => h.Status == "available"), holdings);
    }

    /// <summary>Gợi ý nhan đề khi gõ: nhan đề bắt đầu bằng cụm đã gõ đứng trước.</summary>
    public async Task<IReadOnlyList<string>> SuggestAsync(string? q, CancellationToken ct)
    {
        var phrase = TextFold.Fold(q);
        if (phrase.Length < 2) return [];
        var titles = await Visible.Where(b => b.TitleFold.Contains(phrase))
            .OrderBy(b => b.TitleFold.StartsWith(phrase) ? 0 : 1).ThenBy(b => b.TitleFold).Select(b => b.Title).Take(20).ToListAsync(ct);
        return [.. titles.Distinct().Take(8)];
    }

    /// <summary>"Có thể bạn quan tâm": cùng tác giả hoặc cùng lớp phân loại DDC (3 số đầu).</summary>
    public async Task<IReadOnlyList<OpacBib>> SimilarAsync(Guid publicId, int size, CancellationToken ct)
    {
        var b = await Visible.FirstOrDefaultAsync(x => x.BibPublicId == publicId, ct) ?? throw new NotFoundException("Tài liệu", publicId);
        var ddc = b.Ddc is { Length: >= 3 } d ? d[..3] : null;
        var author = b.Author;
        if (author is null && ddc is null) return [];
        return await Visible.Where(x => x.Mfn != b.Mfn && ((author != null && x.Author == author) || (ddc != null && x.Ddc != null && x.Ddc.StartsWith(ddc))))
            .OrderBy(x => x.Author == author ? 0 : 1).ThenByDescending(x => x.Year).ThenByDescending(x => x.Mfn)
            .Take(Math.Clamp(size, 1, 20)).Select(Projection).ToListAsync(ct);
    }

    private IQueryable<SearchBib> Filter(OpacSearchRequest r)
    {
        var query = Visible;
        foreach (var token in TextFold.Tokens(r.Q)) query = query.Where(b => b.SearchText.Contains(token));
        foreach (var token in TextFold.Tokens(r.Title)) query = query.Where(b => b.TitleFold.Contains(token));
        foreach (var token in TextFold.Tokens(r.Author)) query = query.Where(b => b.AuthorFold.Contains(token));
        foreach (var token in TextFold.Tokens(r.Publisher)) query = query.Where(b => b.PublisherFold.Contains(token));
        foreach (var token in TextFold.Tokens(r.Keyword)) query = query.Where(b => b.KeywordFold.Contains(token));
        if (Digits(r.Isbn) is { Length: > 0 } isbn) query = query.Where(b => b.IsbnKey.Contains(isbn));
        if (!string.IsNullOrWhiteSpace(r.Ddc))
        {
            var ddc = r.Ddc.Trim();
            query = query.Where(b => b.Ddc != null && b.Ddc.StartsWith(ddc));
        }
        if (!string.IsNullOrWhiteSpace(r.Barcode))
        {
            var key = SearchItem.Key(r.Barcode);
            query = query.Where(b => Copies.Any(i => i.Mfn == b.Mfn && i.BarcodeKey.StartsWith(key)));
        }
        if (r.Years is { Count: > 0 } years) query = query.Where(b => b.Year != null && years.Contains(b.Year.Value));
        else
        {
            if (r.YearFrom is { } from) query = query.Where(b => b.Year >= from);
            if (r.YearTo is { } to) query = query.Where(b => b.Year <= to);
        }
        if (r.Authors is { Count: > 0 } authors) query = query.Where(b => b.Author != null && authors.Contains(b.Author));
        if (r.MaterialTypes is { Count: > 0 } types) query = query.Where(b => b.MaterialType != null && types.Contains(b.MaterialType));
        if (r.Languages is { Count: > 0 } languages) query = query.Where(b => b.Language != null && languages.Contains(b.Language));
        if (r.Stores is { Count: > 0 } stores) query = query.Where(b => Copies.Any(i => i.Mfn == b.Mfn && i.StoreName != null && stores.Contains(i.StoreName)));
        if (r.AvailableOnly) query = query.Where(b => Copies.Any(i => i.Mfn == b.Mfn && i.Status == "R" && !i.OnLoan));
        return query;
    }

    private static IQueryable<SearchBib> Order(IQueryable<SearchBib> query, string? sort, string phrase) => (sort ?? "").ToLowerInvariant() switch
    {
        "newest" => query.OrderByDescending(b => b.Year ?? 0).ThenByDescending(b => b.Mfn),
        "oldest" => query.OrderBy(b => b.Year ?? 9999).ThenBy(b => b.Mfn),
        "title" => query.OrderBy(b => b.TitleFold).ThenBy(b => b.Mfn),
        // Liên quan: cụm từ ở đầu nhan đề > trong nhan đề > trong tên tác giả > còn lại; cùng hạng thì mới hơn trước.
        _ when phrase.Length > 0 => query
            .OrderByDescending(b => b.TitleFold.StartsWith(phrase) ? 3 : b.TitleFold.Contains(phrase) ? 2 : b.AuthorFold.Contains(phrase) ? 1 : 0)
            .ThenByDescending(b => b.Year ?? 0).ThenByDescending(b => b.Mfn),
        _ => query.OrderByDescending(b => b.Mfn),
    };

#pragma warning disable CA1845 // biểu thức EF dịch sang SQL — không dùng span
    private Expression<Func<SearchBib, OpacBib>> Projection => b => new OpacBib(
        b.BibPublicId, b.Mfn, b.Title, b.Author, b.Publisher, b.PublishYear, b.MaterialType, b.Language, b.Ddc, b.Isbns,
        b.Summary != null && b.Summary.Length > 300 ? b.Summary.Substring(0, 300) + "…" : b.Summary,
        Copies.Count(i => i.Mfn == b.Mfn),
        Copies.Count(i => i.Mfn == b.Mfn && i.Status == "R" && !i.OnLoan));
#pragma warning restore CA1845

    private async Task<OpacFacets> FacetsAsync(IQueryable<SearchBib> query, CancellationToken ct)
    {
        var years = await query.Where(b => b.Year != null).GroupBy(b => b.Year!.Value).Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Key).Take(30).ToListAsync(ct);
        var authors = await query.Where(b => b.Author != null).GroupBy(b => b.Author!).Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Key).Take(FacetSize).ToListAsync(ct);
        var types = await query.Where(b => b.MaterialType != null).GroupBy(b => b.MaterialType!).Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(FacetSize).ToListAsync(ct);
        var languages = await query.Where(b => b.Language != null).GroupBy(b => b.Language!).Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(FacetSize).ToListAsync(ct);
        var mfns = query.Select(b => b.Mfn);
        var stores = await Copies.Where(i => i.StoreName != null && mfns.Contains(i.Mfn)).GroupBy(i => i.StoreName!)
            .Select(g => new { g.Key, Count = g.Select(i => i.Mfn).Distinct().Count() })
            .OrderByDescending(x => x.Count).Take(FacetSize).ToListAsync(ct);
        var available = await query.CountAsync(b => Copies.Any(i => i.Mfn == b.Mfn && i.Status == "R" && !i.OnLoan), ct);
        return new OpacFacets(
            [.. years.Select(x => new FacetCount(x.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), x.Count))],
            [.. authors.Select(x => new FacetCount(x.Key, x.Count))],
            [.. types.Select(x => new FacetCount(x.Key, x.Count))],
            [.. languages.Select(x => new FacetCount(x.Key, x.Count))],
            [.. stores.Select(x => new FacetCount(x.Key, x.Count))],
            available);
    }

    private static string Digits(string? isbn) => new([.. (isbn ?? "").Where(c => char.IsAsciiDigit(c) || c is 'x' or 'X').Select(char.ToUpperInvariant)]);
}
