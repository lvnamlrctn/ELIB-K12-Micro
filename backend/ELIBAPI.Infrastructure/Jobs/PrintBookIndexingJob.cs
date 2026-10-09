using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Elasticsearch;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>
/// Index biểu ghi tài liệu in (PrintBook.Bib) vào index gộp <c>library_docs</c>.
/// Theo đúng khuôn <see cref="EbookIndexingJob"/> nhưng đơn giản hơn nhiều vì tài liệu in
/// không có file để trích xuất — chỉ gom metadata thư mục + bản sao vật lý.
/// </summary>
public class PrintBookIndexingJob(
    ELIBAPIDbContext              db,
    IElasticsearchService         elastic,
    ILogger<PrintBookIndexingJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
    public async Task RunAsync(long bibId)
    {
        var docs = await BuildDocumentsAsync([bibId]);
        if (docs.Count == 0)
        {
            // Biểu ghi đã bị xóa mềm (hoặc không tồn tại) → gỡ khỏi index.
            await elastic.DeleteLibraryDocsByGroupAsync($"print_{bibId}");
            return;
        }
        await elastic.UpsertLibraryDocumentsAsync(docs);
    }

    /// <summary>Gỡ 1 biểu ghi khỏi index — gọi khi xóa (mềm hoặc cứng).</summary>
    public async Task RemoveAsync(long bibId)
        => await elastic.DeleteLibraryDocsByGroupAsync($"print_{bibId}");

    /// <summary>Backfill toàn bộ (hoặc 1 khoảng Bibid) theo lô.</summary>
    [AutomaticRetry(Attempts = 0)]
    public Task RunBulkAsync(long? fromId = null, long? toId = null, int batchSize = 500)
        => RunBulkForTenantAsync(null, fromId, toId, batchSize);

    /// <summary>Đợt 18: như <see cref="RunBulkAsync"/> nhưng giới hạn 1 đơn vị (<paramref name="tenantId"/> null =
    /// toàn hệ thống). Tách method riêng thay vì thêm tham số để không làm hỏng job Hangfire đã xếp hàng theo
    /// chữ ký cũ.</summary>
    public async Task RunBulkForTenantAsync(long? tenantId, long? fromId = null, long? toId = null, int batchSize = 500)
    {
        var idQuery = db.Bibs.AsNoTracking().Where(b => b.IsDelete != 2);
        if (tenantId.HasValue) idQuery = idQuery.Where(b => b.TenantId == tenantId.Value);
        if (fromId.HasValue) idQuery = idQuery.Where(b => b.Bibid >= fromId.Value);
        if (toId.HasValue)   idQuery = idQuery.Where(b => b.Bibid <= toId.Value);

        var ids = await idQuery.OrderBy(b => b.Bibid).Select(b => b.Bibid).ToListAsync();
        logger.LogInformation("PrintBookIndexingJob: bắt đầu index {Count} biểu ghi (tenant {TenantId})", ids.Count, tenantId);

        var done = 0;
        foreach (var chunk in ids.Chunk(batchSize))
        {
            var docs = await BuildDocumentsAsync(chunk);
            if (docs.Count > 0) await elastic.UpsertLibraryDocumentsAsync(docs);
            done += chunk.Length;
            logger.LogInformation("PrintBookIndexingJob: {Done}/{Total}", done, ids.Count);
        }
        logger.LogInformation("PrintBookIndexingJob: xong {Count} biểu ghi", done);
    }

    // ── Dựng document ────────────────────────────────────────────────────────

    private async Task<List<LibraryDocument>> BuildDocumentsAsync(IReadOnlyCollection<long> bibIds)
    {
        var bibs = await db.Bibs.AsNoTracking()
            .Where(b => bibIds.Contains(b.Bibid) && b.IsDelete != 2)
            .ToListAsync();
        if (bibs.Count == 0) return [];

        var ids = bibs.Select(b => b.Bibid).ToList();

        var xmlMap = await db.BibXmls.AsNoTracking()
            .Where(x => ids.Contains(x.BibId))
            .ToDictionaryAsync(x => x.BibId);

        // Toàn bộ subfield MARC của các biểu ghi này — dùng cho ISBN(020$a), tóm tắt(520$a),
        // đồng tác giả(700$a) và trường bắt-tất allMarcText.
        var marcRows = await db.BibDatas.AsNoTracking()
            .Where(d => d.BibId != null && ids.Contains(d.BibId.Value) && d.IsDelete != 2
                     && d.Data != null && d.Data != "")
            .Select(d => new { BibId = d.BibId!.Value, d.Field, d.SubField, d.Data })
            .ToListAsync();
        var marcByBib = marcRows.GroupBy(d => d.BibId).ToDictionary(g => g.Key, g => g.ToList());

        // MARC 008 → mã ngôn ngữ
        var fixedRows = await db.FixedFieldValues.AsNoTracking()
            .Where(f => f.Bibid != null && ids.Contains(f.Bibid.Value) && f.Field == "008" && f.IsDelete != 2)
            .Select(f => new { BibId = f.Bibid!.Value, f.Value })
            .ToListAsync();
        var lang008 = fixedRows
            .GroupBy(f => f.BibId)
            .ToDictionary(g => g.Key, g => MarcHelper.ExtractLanguage(g.First().Value));

        var barcodes = await db.Barcodes.AsNoTracking()
            .Where(bc => bc.BibId != null && ids.Contains(bc.BibId.Value) && bc.IsDelete != 2)
            .Select(bc => new
            {
                bc.Id, bc.BarcodeValue, bc.Store, bc.Status, bc.Receipt_Id, BibId = bc.BibId!.Value
            })
            .ToListAsync();
        var barcodesByBib = barcodes.GroupBy(b => b.BibId).ToDictionary(g => g.Key, g => g.ToList());

        // Bảng tra cứu để denormalize
        var storeIds = barcodes.Where(b => b.Store.HasValue).Select(b => (long)b.Store!.Value).Distinct().ToList();
        var storeMap = await db.Stores.AsNoTracking()
            .Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);

        var statusIds = barcodes.Where(b => b.Status != null).Select(b => b.Status!).Distinct().ToList();
        var statusMap = await db.BarcodeStatuses.AsNoTracking()
            .Where(s => s.Id != null && statusIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id!, s => s.CommentStatus);

        var receiptIds = barcodes.Where(b => b.Receipt_Id.HasValue).Select(b => b.Receipt_Id!.Value).Distinct().ToList();
        var receiptMap = await db.AbReceipts.AsNoTracking()
            .Where(r => receiptIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Code);

        var bibTypeIds = bibs.Where(b => b.Bib_type_id.HasValue).Select(b => b.Bib_type_id!.Value).Distinct().ToList();
        var bibTypeMap = await db.BibTypes.AsNoTracking()
            .Where(t => bibTypeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);

        var collectionIds = bibs.Where(b => b.CollectionId.HasValue).Select(b => b.CollectionId!.Value).Distinct().ToList();
        var collectionMap = await db.EbookCollections.AsNoTracking()
            .Where(c => collectionIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

        var bibStatusCodes = bibs.Where(b => b.Status != null).Select(b => b.Status!).Distinct().ToList();
        var bibStatusMap = await db.DBibStatuses.AsNoTracking()
            .Where(s => s.Id != null && bibStatusCodes.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id!, s => s.Name);

        var now = DateTime.UtcNow;
        var result = new List<LibraryDocument>(bibs.Count);

        foreach (var bib in bibs)
        {
            xmlMap.TryGetValue(bib.Bibid, out var xml);
            marcByBib.TryGetValue(bib.Bibid, out var marc);
            barcodesByBib.TryGetValue(bib.Bibid, out var copies);

            string? Sub(string field, string sub) => marc?
                .FirstOrDefault(m => m.Field == field && m.SubField == sub)?.Data;
            string? SubJoined(string field, string sub)
            {
                var vals = marc?.Where(m => m.Field == field && m.SubField == sub)
                                .Select(m => m.Data!).ToList();
                return vals is { Count: > 0 } ? string.Join("; ", vals) : null;
            }

            var holdings = (copies ?? [])
                .Select(bc => new HoldingDocument
                {
                    BarcodeId   = bc.Id,
                    Barcode     = bc.BarcodeValue,
                    StoreId     = bc.Store,
                    StoreName   = bc.Store.HasValue && storeMap.TryGetValue(bc.Store.Value, out var sn) ? sn : null,
                    Status      = bc.Status,
                    StatusName  = bc.Status != null && statusMap.TryGetValue(bc.Status, out var stn) ? stn : null,
                    ReceiptId   = bc.Receipt_Id,
                    ReceiptCode = bc.Receipt_Id.HasValue && receiptMap.TryGetValue(bc.Receipt_Id.Value, out var rc) ? rc : null,
                })
                .ToList();

            result.Add(new LibraryDocument
            {
                DocId    = $"print_{bib.Bibid}",
                GroupId  = $"print_{bib.Bibid}",
                DocType  = "print",
                PublicId = bib.PublicId,
                TenantId = bib.TenantId,

                // Bộ trường chung
                Title        = xml?.Title,
                Author       = xml?.Author,
                Publisher    = xml?.Publisher,
                PublishDate  = xml?.PublishDate,
                PublishYear  = MarcHelper.ExtractYear(xml?.PublishDate),
                Ddc          = Blank(xml?.DDC),
                Isbn         = Blank(Sub("020", "a")),
                Summary      = Sub("520", "a"),
                Keyword      = xml?.Keyword,
                Language     = lang008.TryGetValue(bib.Bibid, out var lg) ? lg : null,
                MaterialType = bib.Bib_type_id.HasValue && bibTypeMap.TryGetValue(bib.Bib_type_id.Value, out var bt) ? bt : null,
                CollectionId = bib.CollectionId?.ToString(),
                CollectionName = bib.CollectionId.HasValue && collectionMap.TryGetValue(bib.CollectionId.Value, out var cn) ? cn : null,
                Contributor  = SubJoined("700", "a"),
                Images       = bib.Images,
                IndexedAt    = now,

                // Riêng tài liệu in
                BibId       = bib.Bibid,
                Mfn         = bib.Mfn,
                Isbd        = xml?.Isbd,
                AllMarcText = marc is { Count: > 0 } ? string.Join(" ", marc.Select(m => m.Data)) : null,
                BibTypeId   = bib.Bib_type_id,
                Status      = bib.Status,
                StatusName  = bib.Status != null && bibStatusMap.TryGetValue(bib.Status, out var bsn) ? bsn : null,
                Url         = bib.Url,
                LinkedEbookId = bib.EbookId,

                Holdings   = holdings,
                CopyCount  = holdings.Count,
                // "Sẵn có" = bản sao không ở trạng thái đang mượn/mất/thanh lý.
                // Status là mã tự do trong PrintBook.Barcode_Status nên chỉ đếm thô ở đây;
                // siết lại khi nghiệp vụ chốt danh sách mã "không sẵn sàng".
                AvailableCount = holdings.Count(h => string.IsNullOrEmpty(h.Status) || h.Status == "0"),
            });
        }

        return result;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
