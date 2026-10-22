using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Search.Application;

/// <summary>Tìm tài liệu số trên OPAC: <see cref="Q"/> tìm mọi trường; không dấu, mọi từ phải có, "..." là cụm đúng thứ tự (như tìm sách in).</summary>
public sealed record OpacEbookSearchRequest
{
    public string? Q { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 6;
}

public sealed record OpacEbook(
    Guid PublicId, string Title, string? Author, string? Publisher, string? PublishYear, string? Language, string? CollectionName, string? TypeName,
    string? CoverUrl, bool Free, bool HasFile, Guid? LinkedBibPublicId);

public sealed record OpacEbookResult(int Total, int Page, int PageSize, IReadOnlyList<OpacEbook> Items);

/// <summary>Tra cứu công khai tài liệu số trên chỉ mục của search (chỉ tài liệu xuất bản — Status 2, chưa xoá).</summary>
public sealed class OpacEbooks(ISearchDb db)
{
    public const int MaxPageSize = 30;

    public async Task<OpacEbookResult> SearchAsync(OpacEbookSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var query = db.Set<SearchEbook>().AsNoTracking().Where(e => !e.Deleted && e.Status == SearchEbook.OpacVisible);
        foreach (var term in TextFold.Terms(request.Q))
        {
            var text = term.Text;
            var padded = $" {text} ";
            query = term.Phrase ? query.Where(e => e.SearchText.Contains(text) && (" " + e.SearchText + " ").Contains(padded)) : query.Where(e => e.SearchText.Contains(text));
        }

        var phrase = TextFold.Fold(request.Q);
        var total = await query.CountAsync(ct);
        var items = await (phrase.Length > 0
                ? query.OrderByDescending(e => e.TitleFold.StartsWith(phrase) ? 3 : e.TitleFold.Contains(phrase) ? 2 : e.AuthorFold.Contains(phrase) ? 1 : 0).ThenByDescending(e => e.Year ?? 0)
                : query.OrderByDescending(e => e.Year ?? 0))
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(e => new OpacEbook(e.EbookPublicId, e.Title, e.Author, e.Publisher, e.PublishYear, e.Language, e.CollectionName, e.TypeName, e.CoverUrl, e.Free, e.HasFile,
                e.LinkedBibPublicId))
            .ToListAsync(ct);
        return new OpacEbookResult(total, page, size, items);
    }
}
