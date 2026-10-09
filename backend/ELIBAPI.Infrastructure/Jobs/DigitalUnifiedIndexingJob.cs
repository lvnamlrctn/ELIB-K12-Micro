using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Elasticsearch;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>
/// Index metadata-only của tài liệu số (Ebook.Item) vào index gộp <c>library_docs</c> — bổ sung cho
/// <see cref="EbookIndexingJob"/> (chỉ ghi chunk nội dung/embedding): job này không đọc file PDF, không
/// OCR, không sinh embedding, chỉ gom đúng phần thư mục để tài liệu số xuất hiện được trong tìm kiếm gộp
/// ngay cả khi chưa index nội dung. Cùng quy ước DocId/GroupId với chunk trang 0 của
/// <see cref="EbookIndexingJob"/> ("digital_{id}_p0000_c00" / "digital_{id}") nên khi
/// <see cref="EbookIndexingJob"/> chạy sau, bản ghi nội dung sẽ ghi đè đúng chỗ, không tạo bản trùng.
/// Chạy on-demand (enqueue khi upload file / trigger tay từ AdminJobController), KHÔNG đăng ký Hangfire
/// định kỳ — đúng khuôn <see cref="PrintBookIndexingJob"/>.
/// </summary>
public class DigitalUnifiedIndexingJob(
    ELIBAPIDbContext db,
    IElasticsearchService elastic,
    ILogger<DigitalUnifiedIndexingJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
    public async Task RunAsync(long ebookId)
    {
        var docs = await BuildDocumentsAsync([ebookId]);
        if (docs.Count == 0)
        {
            await elastic.DeleteLibraryDocsByGroupAsync($"digital_{ebookId}");
            return;
        }
        await elastic.UpsertLibraryDocumentsAsync(docs);
    }

    /// <summary>Gỡ 1 tài liệu số khỏi index — gọi khi xóa (mềm hoặc cứng).</summary>
    public async Task RemoveAsync(long ebookId)
        => await elastic.DeleteLibraryDocsByGroupAsync($"digital_{ebookId}");

    /// <summary>Backfill toàn bộ (hoặc 1 khoảng Id) theo lô.</summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task RunBulkAsync(long? fromId = null, long? toId = null, int batchSize = 500)
    {
        var idQuery = db.EbookItems.AsNoTracking().Where(x => x.IsDelete != 2);
        if (fromId.HasValue) idQuery = idQuery.Where(x => x.Id >= fromId.Value);
        if (toId.HasValue)   idQuery = idQuery.Where(x => x.Id <= toId.Value);

        var ids = await idQuery.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        logger.LogInformation("DigitalUnifiedIndexingJob: bắt đầu index {Count} tài liệu số", ids.Count);

        var done = 0;
        foreach (var chunk in ids.Chunk(batchSize))
        {
            var docs = await BuildDocumentsAsync(chunk);
            if (docs.Count > 0) await elastic.UpsertLibraryDocumentsAsync(docs);
            done += chunk.Length;
            logger.LogInformation("DigitalUnifiedIndexingJob: {Done}/{Total}", done, ids.Count);
        }
        logger.LogInformation("DigitalUnifiedIndexingJob: xong {Count} tài liệu số", done);
    }

    // ── Dựng document ────────────────────────────────────────────────────────

    private async Task<List<LibraryDocument>> BuildDocumentsAsync(IReadOnlyCollection<long> ebookIds)
    {
        var items = await db.EbookItems.AsNoTracking()
            .Include(x => x.ItemXml)
            .Include(x => x.Collection)
            .Include(x => x.Subject)
            .Include(x => x.Topic)
            .Where(x => ebookIds.Contains(x.Id) && x.IsDelete != 2 && x.Status == 2)
            .ToListAsync();
        if (items.Count == 0) return [];

        var ids = items.Select(x => x.Id).ToList();

        var metaRows = await db.MetaDataValues.AsNoTracking()
            .Where(m => m.ItemId != null && ids.Contains(m.ItemId.Value) && m.IsDelete != 2)
            .ToListAsync();
        var metaByItem = metaRows.GroupBy(m => m.ItemId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var pdfFiles = await db.EbookFiles.AsNoTracking()
            .Where(f => f.EbookId != null && ids.Contains(f.EbookId.Value) && f.IsDelete != 2
                     && (f.FileExt == "pdf" || f.FileExt == ".pdf" || f.FileType == "application/pdf"))
            .OrderBy(f => f.SortOrder).ThenByDescending(f => f.Id)
            .ToListAsync();
        var firstPdfByItem = pdfFiles.GroupBy(f => f.EbookId!.Value).ToDictionary(g => g.Key, g => g.First().Id);

        var now = DateTime.UtcNow;
        var result = new List<LibraryDocument>(items.Count);

        foreach (var item in items)
        {
            metaByItem.TryGetValue(item.Id, out var metas);
            string? First(int fid) => metas?.FirstOrDefault(m => m.MetaDataFieldId == fid)?.Value;
            string? Joined(int fid) => metas is { Count: > 0 }
                ? string.Join("; ", metas.Where(m => m.MetaDataFieldId == fid && !string.IsNullOrEmpty(m.Value))
                    .OrderBy(m => m.SortOrder).Select(m => m.Value!))
                : null;

            var dcSubject = Joined(57);
            var keyword = string.Join("; ", new[] { item.ItemXml?.Keyword, dcSubject }.Where(s => !string.IsNullOrWhiteSpace(s)));
            firstPdfByItem.TryGetValue(item.Id, out var pdfFileId);

            result.Add(new LibraryDocument
            {
                DocId    = $"digital_{item.Id}_p0000_c00",
                GroupId  = $"digital_{item.Id}",
                DocType  = "digital",
                PublicId = item.PublicId,
                TenantId = item.TenantId,

                Title       = item.ItemXml?.Title,
                Author      = item.ItemXml?.Author,
                Publisher   = item.ItemXml?.Publisher,
                PublishDate = item.ItemXml?.PublishDate,
                PublishYear = MarcHelper.ExtractYear(item.ItemXml?.PublishDate),
                Ddc         = item.Subject?.DDC ?? item.Topic?.DDC,
                Isbn        = First(20) ?? First(23),
                Summary     = First(27),
                Keyword     = string.IsNullOrWhiteSpace(keyword) ? null : keyword,
                Language    = First(38),
                CollectionId   = item.CollectionId?.ToString(),
                CollectionName = item.Collection?.Name,
                Contributor = Joined(3),
                Images      = item.Images,
                IndexedAt   = now,

                EbookId     = item.Id,
                EbookFileId = pdfFileId,
                PageNumber  = 0,
                ChunkIndex  = 0,
                TopicId     = item.TopicId?.ToString(),
                TopicName   = item.Topic?.Name,
                SubjectId   = item.SubjectId?.ToString(),
                SubjectName = item.Subject?.Name,
                Free        = item.Free  == 1,
                Share       = item.Share == 1,
            });
        }

        return result;
    }
}
