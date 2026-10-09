using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Repositories;

public class BibRepository : BaseRepository<Bib, BibSearchRequest, BibRequest>
{
    private readonly IElasticsearchService _elastic;
    private readonly IConfiguration _config;

    // Tập bibId do Elasticsearch trả về cho lần tìm kiếm hiện tại.
    // BuildQuery là hàm ĐỒNG BỘ (không await được) nên phải phân giải trước ở SearchAsync/SearchAllAsync
    // rồi gửi xuống qua field này. Repository đăng ký Scoped (1 instance / 1 request) và 2 hàm đó
    // không lồng nhau, nên không có rủi ro tranh chấp.
    private HashSet<long>? _esBibIds;

    public BibRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http,
        IElasticsearchService elastic, IConfiguration config) : base(ctx, http)
    {
        _elastic = elastic;
        _config  = config;
    }

    private bool UseElastic
        => !bool.TryParse(_config["PrintBookSearch:UseElastic"], out var v) || v;

    private static bool HasTextFilter(BibSearchRequest r)
        => !string.IsNullOrEmpty(r.Title) || !string.IsNullOrEmpty(r.Author)
        || !string.IsNullOrEmpty(r.Publisher) || !string.IsNullOrEmpty(r.PublishYear)
        || !string.IsNullOrEmpty(r.Keyword);

    private async Task<HashSet<long>?> ResolveEsBibIdsAsync(BibSearchRequest r)
    {
        if (!UseElastic || !HasTextFilter(r)) return null;
        return await _elastic.SearchPrintBibIdsAsync(new PrintBibFilter
        {
            Title       = r.Title,
            Author      = r.Author,
            Publisher   = r.Publisher,
            PublishDate = r.PublishYear,
            // Keyword ở DTO này là ô "tìm chung" (trước đây quét Title/Author/Publisher bằng LIKE);
            // trên ES đưa thẳng vào truy vấn đa trường sẽ đúng ngữ nghĩa hơn.
            Keyword     = r.Keyword,
        });
    }

    public override async Task<PagedResult<Bib>> SearchAsync(BibSearchRequest request)
    {
        _esBibIds = await ResolveEsBibIdsAsync(request);
        try { return await base.SearchAsync(request); }
        finally { _esBibIds = null; }
    }

    public override async Task<List<Bib>> SearchAllAsync(BibSearchRequest request)
    {
        _esBibIds = await ResolveEsBibIdsAsync(request);
        try { return await base.SearchAllAsync(request); }
        finally { _esBibIds = null; }
    }

    protected override IQueryable<Bib> BuildQuery(BibSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);

        if (r.Bib_type_id.HasValue) q = q.Where(x => x.Bib_type_id == r.Bib_type_id);
        if (r.MfnFrom.HasValue)     q = q.Where(x => x.Mfn >= r.MfnFrom);
        if (r.MfnTo.HasValue)       q = q.Where(x => x.Mfn <= r.MfnTo);

        // Đường Elasticsearch: đã phân giải sẵn ở SearchAsync/SearchAllAsync.
        if (_esBibIds != null)
        {
            var ids = _esBibIds;
            return q.Where(x => ids.Contains(x.Bibid)).OrderByDescending(x => x.Bibid);
        }

        var hasTextFilter = HasTextFilter(r);
        if (hasTextFilter)
        {
            var xmlQuery = _context.BibXmls.AsQueryable();
            if (!string.IsNullOrEmpty(r.Title))       xmlQuery = xmlQuery.Where(x => x.Title       != null && x.Title.Contains(r.Title));
            if (!string.IsNullOrEmpty(r.Author))      xmlQuery = xmlQuery.Where(x => x.Author      != null && x.Author.Contains(r.Author));
            if (!string.IsNullOrEmpty(r.Publisher))    xmlQuery = xmlQuery.Where(x => x.Publisher   != null && x.Publisher.Contains(r.Publisher));
            if (!string.IsNullOrEmpty(r.PublishYear)) xmlQuery = xmlQuery.Where(x => x.PublishDate != null && x.PublishDate.Contains(r.PublishYear));
            if (!string.IsNullOrEmpty(r.Keyword))
                xmlQuery = xmlQuery.Where(x =>
                    (x.Title    != null && x.Title.Contains(r.Keyword))    ||
                    (x.Author   != null && x.Author.Contains(r.Keyword))  ||
                    (x.Publisher!= null && x.Publisher.Contains(r.Keyword)));

            var bibIds = xmlQuery.Select(x => x.BibId);
            q = q.Where(x => bibIds.Contains(x.Bibid));
        }

        return q.OrderByDescending(x => x.Bibid);
    }

    protected override void MapRequestToEntity(BibRequest r, Bib e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Bib e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Bib e, int status, long userId) { }
}
