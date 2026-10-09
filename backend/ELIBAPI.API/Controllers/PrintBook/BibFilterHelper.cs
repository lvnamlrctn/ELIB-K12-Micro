using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.API.Controllers.PrintBook;

public static class BibFilterHelper
{
    /// <summary>
    /// Bật tìm kiếm tài liệu in qua Elasticsearch. Tắt (false) để quay lại truy vấn SQL LIKE cũ —
    /// giữ đường lui vì tra cứu tài liệu in là nghiệp vụ lõi, không nên phụ thuộc cứng vào ES.
    /// </summary>
    public static bool UseElastic(IConfiguration config)
        => !bool.TryParse(config["PrintBookSearch:UseElastic"], out var v) || v;

    // Lọc theo Bib (Mfn/Title/Author/Publisher/PublishDate) — trả về null nếu không có bộ lọc nào
    public static async Task<HashSet<long>?> GetMatchingBibIdsAsync(
        ELIBAPIDbContext db, long? mfnFrom, long? mfnTo, string? title, string? author, string? publisher, string? publishDate,
        IElasticsearchService? elastic = null, IConfiguration? config = null)
    {
        bool hasMfn  = mfnFrom.HasValue || mfnTo.HasValue;
        bool hasText = !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(author)
                     || !string.IsNullOrEmpty(publisher) || !string.IsNullOrEmpty(publishDate);
        if (!hasMfn && !hasText) return null;

        // Đường ES: tách từ + bỏ dấu tiếng Việt, thay cho LIKE '%...%'.
        if (elastic != null && config != null && UseElastic(config))
        {
            return await elastic.SearchPrintBibIdsAsync(new PrintBibFilter
            {
                MfnFrom = mfnFrom, MfnTo = mfnTo,
                Title = title, Author = author, Publisher = publisher, PublishDate = publishDate,
            });
        }

        List<long>? mfnIds = null;
        if (hasMfn)
        {
            var bibQ = db.Bibs.Where(x => x.IsDelete != 2);
            if (mfnFrom.HasValue) bibQ = bibQ.Where(x => x.Mfn >= mfnFrom);
            if (mfnTo.HasValue)   bibQ = bibQ.Where(x => x.Mfn <= mfnTo);
            mfnIds = await bibQ.Select(x => x.Bibid).ToListAsync();
        }
        if (!hasText) return mfnIds!.ToHashSet();

        var xmlQ = db.BibXmls.AsQueryable();
        if (mfnIds != null) xmlQ = xmlQ.Where(x => mfnIds.Contains(x.BibId));
        if (!string.IsNullOrEmpty(title))       xmlQ = xmlQ.Where(x => x.Title!.Contains(title));
        if (!string.IsNullOrEmpty(author))      xmlQ = xmlQ.Where(x => x.Author!.Contains(author));
        if (!string.IsNullOrEmpty(publisher))   xmlQ = xmlQ.Where(x => x.Publisher!.Contains(publisher));
        if (!string.IsNullOrEmpty(publishDate)) xmlQ = xmlQ.Where(x => x.PublishDate!.Contains(publishDate));
        return (await xmlQ.Select(x => x.BibId).ToListAsync()).ToHashSet();
    }
}
