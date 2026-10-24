using System.Diagnostics;
using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Search.Application;

/// <summary>
/// Tìm gộp tài liệu in và tài liệu số trên OPAC (một danh sách). <see cref="Kind"/>: all | print | digital. <see cref="Q"/> và các ô theo trường dùng cùng
/// quy tắc với tìm sách in (không dấu, mọi từ phải có, "..." là cụm). Đặt <see cref="Collection"/> thì chỉ còn tài liệu số của bộ sưu tập đó;
/// <see cref="AvailableOnly"/> chỉ lọc sách in (tài liệu số luôn truy cập được). Sort: relevance (mặc định) | newest | oldest | title.
/// </summary>
public sealed record OpacAllRequest
{
    public string? Q { get; init; }
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Publisher { get; init; }
    public string? Keyword { get; init; }
    public int? YearFrom { get; init; }
    public int? YearTo { get; init; }
    public string? Kind { get; init; }
    public string? Collection { get; init; }
    public bool AvailableOnly { get; init; }
    public string? Sort { get; init; }

    /// <summary>Chỉ đổi bộ lọc/sắp xếp/loại của câu tìm đang xem — không tính là lượt tìm mới trong thống kê.</summary>
    public bool Refine { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}

/// <summary>Một kết quả: <see cref="Kind"/> = print | digital. Sách in có Copies/Available/MaterialType; tài liệu số có Free/HasFile/CollectionName.</summary>
public sealed record OpacAllHit(
    string Kind, Guid PublicId, string Title, string? Author, string? Publisher, string? PublishYear, string? CoverUrl, string? Language,
    string? MaterialType, int Copies, int Available, string? CollectionName, bool Free, bool HasFile, Guid? LinkedBibPublicId);

/// <summary>
/// <see cref="PrintTotal"/>/<see cref="DigitalTotal"/>: số kết quả của từng loại theo bộ lọc hiện tại (hiện trên chip); <see cref="Total"/> theo <c>Kind</c> đang chọn.
/// <see cref="Collections"/>: bộ sưu tập tài liệu số có trong kết quả (không tính điều kiện Collection).
/// </summary>
public sealed record OpacAllResult(
    int Total, int PrintTotal, int DigitalTotal, int Page, int PageSize, IReadOnlyList<OpacAllHit> Items, IReadOnlyList<FacetCount> Collections,
    Guid? QueryId = null, long ElapsedMs = 0);

/// <summary>
/// Trộn hai nguồn (sách in, tài liệu số) mà không dùng UNION: mỗi nguồn lấy khoá sắp xếp của "page × size" bản ghi đầu theo cùng thứ tự,
/// gộp trong bộ nhớ, cắt đúng cửa sổ trang rồi nạp chi tiết theo id — chạy giống nhau trên PostgreSQL và SQLite; độ sâu trang bị chặn.
/// </summary>
public sealed class OpacUnifiedSearch(ISearchDb db, OpacSearch print)
{
    public const int MaxPageSize = 50;
    public const int MaxDepth = 500;
    private const int FacetSize = 15;

    private readonly record struct Key(bool Print, long Id, Guid PublicId, int Rank, int Year, string TitleFold);

    private IQueryable<SearchEbook> Ebooks => db.Set<SearchEbook>().AsNoTracking().Where(e => !e.Deleted && e.Status == SearchEbook.OpacVisible);

    public async Task<OpacAllResult> SearchAsync(OpacAllRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = Stopwatch.GetTimestamp();
        var size = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var page = Math.Clamp(request.Page, 1, Math.Max(1, MaxDepth / size));
        var kind = (request.Kind ?? "all").ToLowerInvariant();
        var sort = (request.Sort ?? "").ToLowerInvariant();
        var phrase = TextFold.Fold(request.Q ?? request.Title);
        var hasCollection = !string.IsNullOrWhiteSpace(request.Collection);

        var bibs = print.Filter(new OpacSearchRequest
        {
            Q = request.Q, Title = request.Title, Author = request.Author, Publisher = request.Publisher, Keyword = request.Keyword,
            YearFrom = request.YearFrom, YearTo = request.YearTo, AvailableOnly = request.AvailableOnly,
        });
        if (hasCollection) bibs = bibs.Where(_ => false); // sách in không thuộc bộ sưu tập tài liệu số
        var ebooksBase = FilterEbooks(request);
        var ebooks = hasCollection ? ebooksBase.Where(e => e.CollectionName == request.Collection!.Trim()) : ebooksBase;

        var printTotal = await bibs.CountAsync(ct);
        var digitalTotal = await ebooks.CountAsync(ct);
        var total = kind switch { "print" => printTotal, "digital" => digitalTotal, _ => printTotal + digitalTotal };

        var take = page * size;
        var keys = new List<Key>();
        if (kind != "digital" && printTotal > 0) keys.AddRange(await PrintKeysAsync(bibs, sort, phrase, take, ct));
        if (kind != "print" && digitalTotal > 0) keys.AddRange(await DigitalKeysAsync(ebooks, sort, phrase, take, ct));
        var window = keys.Order(Comparer(sort)).Skip((page - 1) * size).Take(size).ToList();

        var mfns = window.Where(k => k.Print).Select(k => k.Id).ToList();
        var ebookIds = window.Where(k => !k.Print).Select(k => k.Id).ToList();
        var bibRows = mfns.Count == 0 ? [] : await print.Visible.Where(b => mfns.Contains(b.Mfn)).Select(print.Projection).ToListAsync(ct);
        var bibByMfn = bibRows.ToDictionary(b => b.Mfn);
        var ebookRows = ebookIds.Count == 0 ? [] : await Ebooks.Where(e => ebookIds.Contains(e.Id)).ToListAsync(ct);
        var ebookById = ebookRows.ToDictionary(e => e.Id);

        var items = new List<OpacAllHit>(window.Count);
        foreach (var k in window)
        {
            if (k.Print && bibByMfn.TryGetValue(k.Id, out var b))
                items.Add(new OpacAllHit("print", b.PublicId, b.Title, b.Author, b.Publisher, b.PublishYear, b.CoverUrl, b.Language, b.MaterialType, b.Copies, b.Available,
                    null, false, false, null));
            else if (!k.Print && ebookById.TryGetValue(k.Id, out var e))
                items.Add(new OpacAllHit("digital", e.EbookPublicId, e.Title, e.Author, e.Publisher, e.PublishYear, e.CoverUrl, e.Language, e.TypeName, 0, 0,
                    e.CollectionName, e.Free, e.HasFile, e.LinkedBibPublicId));
        }

        var collections = await ebooksBase.Where(e => e.CollectionName != null).GroupBy(e => e.CollectionName!).Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Key).Take(FacetSize).ToListAsync(ct);
        return new OpacAllResult(total, printTotal, digitalTotal, page, size, items, [.. collections.Select(c => new FacetCount(c.Key, c.Count))],
            ElapsedMs: (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    /// <summary>Điều kiện tìm của tài liệu số: Q/Nhan đề/Tác giả theo trường; NXB và Từ khoá tìm trong toàn bộ nội dung chỉ mục (SearchText).</summary>
    private IQueryable<SearchEbook> FilterEbooks(OpacAllRequest r)
    {
        var query = Ebooks;
        query = OpacSearch.Match(query, r.Q, e => e.SearchText);
        query = OpacSearch.Match(query, r.Title, e => e.TitleFold);
        query = OpacSearch.Match(query, r.Author, e => e.AuthorFold);
        query = OpacSearch.Match(query, r.Publisher, e => e.SearchText);
        query = OpacSearch.Match(query, r.Keyword, e => e.SearchText);
        if (r.YearFrom is { } from) query = query.Where(e => e.Year >= from);
        if (r.YearTo is { } to) query = query.Where(e => e.Year <= to);
        return query;
    }

    private static async Task<List<Key>> PrintKeysAsync(IQueryable<SearchBib> query, string sort, string phrase, int take, CancellationToken ct)
    {
        var ordered = sort switch
        {
            "newest" => query.OrderByDescending(b => b.Year ?? 0).ThenByDescending(b => b.Mfn),
            "oldest" => query.OrderBy(b => b.Year ?? 9999).ThenByDescending(b => b.Mfn),
            "title" => query.OrderBy(b => b.TitleFold).ThenByDescending(b => b.Mfn),
            _ => query.OrderByDescending(b => phrase.Length == 0 ? 0 : b.TitleFold.StartsWith(phrase) ? 3 : b.TitleFold.Contains(phrase) ? 2 : b.AuthorFold.Contains(phrase) ? 1 : 0)
                .ThenByDescending(b => b.Year ?? 0).ThenByDescending(b => b.Mfn),
        };
        var rows = await ordered.Take(take).Select(b => new
        {
            b.Mfn, b.BibPublicId, b.Year, b.TitleFold,
            Rank = phrase.Length == 0 ? 0 : b.TitleFold.StartsWith(phrase) ? 3 : b.TitleFold.Contains(phrase) ? 2 : b.AuthorFold.Contains(phrase) ? 1 : 0,
        }).ToListAsync(ct);
        return [.. rows.Select(r => new Key(true, r.Mfn, r.BibPublicId, r.Rank, r.Year ?? 0, r.TitleFold))];
    }

    private static async Task<List<Key>> DigitalKeysAsync(IQueryable<SearchEbook> query, string sort, string phrase, int take, CancellationToken ct)
    {
        var ordered = sort switch
        {
            "newest" => query.OrderByDescending(e => e.Year ?? 0).ThenByDescending(e => e.Id),
            "oldest" => query.OrderBy(e => e.Year ?? 9999).ThenByDescending(e => e.Id),
            "title" => query.OrderBy(e => e.TitleFold).ThenByDescending(e => e.Id),
            _ => query.OrderByDescending(e => phrase.Length == 0 ? 0 : e.TitleFold.StartsWith(phrase) ? 3 : e.TitleFold.Contains(phrase) ? 2 : e.AuthorFold.Contains(phrase) ? 1 : 0)
                .ThenByDescending(e => e.Year ?? 0).ThenByDescending(e => e.Id),
        };
        var rows = await ordered.Take(take).Select(e => new
        {
            e.Id, e.EbookPublicId, e.Year, e.TitleFold,
            Rank = phrase.Length == 0 ? 0 : e.TitleFold.StartsWith(phrase) ? 3 : e.TitleFold.Contains(phrase) ? 2 : e.AuthorFold.Contains(phrase) ? 1 : 0,
        }).ToListAsync(ct);
        return [.. rows.Select(r => new Key(false, r.Id, r.EbookPublicId, r.Rank, r.Year ?? 0, r.TitleFold))];
    }

    /// <summary>Thứ tự trộn = thứ tự từng nguồn, hoà thì sách in trước rồi tới id lớn trước (mới hơn).</summary>
    private static Comparer<Key> Comparer(string sort) => Comparer<Key>.Create((a, b) =>
    {
        var c = sort switch
        {
            "newest" => b.Year.CompareTo(a.Year),
            "oldest" => YearAsc(a).CompareTo(YearAsc(b)),
            "title" => string.CompareOrdinal(a.TitleFold, b.TitleFold),
            _ => b.Rank != a.Rank ? b.Rank.CompareTo(a.Rank) : b.Year.CompareTo(a.Year),
        };
        if (c != 0) return c;
        if (a.Print != b.Print) return a.Print ? -1 : 1;
        return b.Id.CompareTo(a.Id);
    });

    private static int YearAsc(Key k) => k.Year == 0 ? 9999 : k.Year;
}
