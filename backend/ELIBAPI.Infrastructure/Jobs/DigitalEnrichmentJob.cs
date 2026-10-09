using ELIBAPI.Core.DTOs.Elasticsearch;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>
/// Bổ sung các trường CHUNG cho tài liệu số trong index gộp mà <c>_reindex</c> không lấy được,
/// vì index chunk cũ không hề chứa chúng (đã đo trên dữ liệu thật: dcDescription/dcLanguage/
/// dcIdentifier/tenantId rỗng 100%, dcType chỉ 11%). Nguồn duy nhất là DB.
///
/// Chỉ cập nhật tại chỗ theo ebookId — KHÔNG đụng tới content/embedding, nên chạy rất nhanh
/// và không tốn quota OCR/embedding.
/// </summary>
public class DigitalEnrichmentJob(
    ELIBAPIDbContext               db,
    IElasticsearchService          elastic,
    ILogger<DigitalEnrichmentJob>  logger)
{
    // FieldId trong Ebook.MetaDataFieldRegistery (hard-code sẵn ở EbookIndexingJob và ebook-indexer/db.py)
    private const int FieldIsbn     = 20;
    private const int FieldIssn     = 23;
    private const int FieldSummary  = 27;
    private const int FieldLanguage = 38;
    private const int FieldDocType  = 66;

    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(long? fromId = null, long? toId = null)
    {
        var itemQuery = db.EbookItems.AsNoTracking().Where(x => x.IsDelete != 2);
        if (fromId.HasValue) itemQuery = itemQuery.Where(x => x.Id >= fromId.Value);
        if (toId.HasValue)   itemQuery = itemQuery.Where(x => x.Id <= toId.Value);

        var items = await itemQuery
            .Select(x => new { x.Id, x.TenantId, x.SubjectId, x.TopicId })
            .ToListAsync();

        logger.LogInformation("DigitalEnrichmentJob: bắt đầu làm giàu {Count} tài liệu số", items.Count);

        var subjectDdc = await db.EbookSubjects.AsNoTracking()
            .Where(s => s.DDC != null && s.DDC != "")
            .ToDictionaryAsync(s => s.Id, s => s.DDC);
        var topicDdc = await db.EbookTopics.AsNoTracking()
            .Where(t => t.DDC != null && t.DDC != "")
            .ToDictionaryAsync(t => t.Id, t => t.DDC);

        var itemIds = items.Select(i => i.Id).ToList();
        var metas = await db.MetaDataValues.AsNoTracking()
            .Where(m => m.ItemId != null && itemIds.Contains(m.ItemId.Value) && m.IsDelete != 2
                     && m.Value != null && m.Value != ""
                     && new[] { FieldIsbn, FieldIssn, FieldSummary, FieldLanguage, FieldDocType }
                            .Contains(m.MetaDataFieldId!.Value))
            .Select(m => new { ItemId = m.ItemId!.Value, FieldId = m.MetaDataFieldId!.Value, m.Value })
            .ToListAsync();
        var metaByItem = metas.GroupBy(m => m.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Đếm độ phủ để biết các trường chung có thực sự có dữ liệu phía tài liệu số hay không
        // (chưa xác minh được trực tiếp trên DB lúc lập kế hoạch).
        int nDdc = 0, nSummary = 0, nLanguage = 0, nIsbn = 0, nMaterial = 0, nTenant = 0;

        foreach (var item in items)
        {
            metaByItem.TryGetValue(item.Id, out var m);
            string? Meta(int fid) => m?.FirstOrDefault(x => x.FieldId == fid)?.Value;

            var ddc = item.SubjectId.HasValue && subjectDdc.TryGetValue(item.SubjectId.Value, out var sd) ? sd
                    : item.TopicId.HasValue   && topicDdc.TryGetValue(item.TopicId.Value, out var td)   ? td
                    : null;

            var meta = new LibraryDocument
            {
                Ddc          = ddc,
                Summary      = Meta(FieldSummary),
                Language     = Meta(FieldLanguage),
                Isbn         = Meta(FieldIsbn) ?? Meta(FieldIssn),
                MaterialType = Meta(FieldDocType),
                TenantId     = item.TenantId,
            };

            if (meta.Ddc          != null) nDdc++;
            if (meta.Summary      != null) nSummary++;
            if (meta.Language     != null) nLanguage++;
            if (meta.Isbn         != null) nIsbn++;
            if (meta.MaterialType != null) nMaterial++;
            if (meta.TenantId     != null) nTenant++;

            try { await elastic.EnrichDigitalCommonFieldsAsync(item.Id, meta); }
            catch (Exception ex) { logger.LogWarning(ex, "Làm giàu thất bại cho ebookId {Id}", item.Id); }
        }

        logger.LogInformation(
            "DigitalEnrichmentJob: xong {Total} tài liệu. Độ phủ — ddc={Ddc}, summary={Summary}, " +
            "language={Lang}, isbn={Isbn}, materialType={Mat}, tenantId={Tenant}",
            items.Count, nDdc, nSummary, nLanguage, nIsbn, nMaterial, nTenant);
    }
}
