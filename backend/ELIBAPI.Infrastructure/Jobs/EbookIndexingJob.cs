using ELIBAPI.Core.DTOs.Elasticsearch;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

public class EbookIndexingJob(
    ELIBAPIDbContext          db,
    IMinioService             minio,
    IPdfExtractorService      extractor,
    IOcrService               ocr,
    IElasticsearchService     elastic,
    IEmbeddingService         embedding,
    IConfiguration            config,
    ILogger<EbookIndexingJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [300, 300, 300])]
    public async Task RunAsync(Guid publicId)
    {
        // 1. Load EbookItem + EbookItemXml + MetaDataValues + file PDF đầu tiên
        var item = await db.EbookItems
            .Include(x => x.ItemXml)
            .Include(x => x.Collection)
            .Include(x => x.Subject)
            .Include(x => x.Topic)
            .FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2)
            ?? throw new InvalidOperationException($"EbookItem {publicId} not found");

        var pdfFile = await db.EbookFiles
            .Where(f => f.EbookId == item.Id && f.IsDelete != 2
                     && (f.FileExt == "pdf" || f.FileExt == ".pdf" || f.FileType == "application/pdf"))
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.Id)
            .FirstOrDefaultAsync();

        var metas = await db.MetaDataValues
            .Where(m => m.ItemId == item.Id && m.IsDelete != 2)
            .AsNoTracking()
            .ToListAsync();

        string? First(int fid)  => metas.FirstOrDefault(m => m.MetaDataFieldId == fid)?.Value;
        string? Joined(int fid) => string.Join("; ", metas
            .Where(m => m.MetaDataFieldId == fid && !string.IsNullOrEmpty(m.Value))
            .OrderBy(m => m.SortOrder)
            .Select(m => m.Value!));

        var ebookId    = item.Id;
        var fileVersion = item.FileVersion ?? 1;

        try
        {
            // 2. Xóa chunks cũ nếu đây không phải lần index đầu tiên
            if (fileVersion > 1)
                await elastic.DeleteChunksByEbookIdAsync(ebookId);

            // 3. Metadata chung cho mọi chunk
            var dcSubject     = Joined(57);
            var dcDescription = First(27);
            var dcLanguage    = First(38);
            var dcIdentifier  = First(20) ?? First(23);
            var dcType        = First(66);
            var dcContributor = Joined(3);
            if (string.IsNullOrEmpty(dcContributor))
                dcContributor = item.ItemXml?.Author;
            var now           = DateTime.UtcNow;

            List<EbookChunkDocument> docChunks;

            if (pdfFile != null)
            {
                // 4a. Có PDF → extract chunks từ file
                var (pdfStream, _) = await minio.GetObjectStreamAsync(pdfFile.Url ?? string.Empty);

                byte[] pdfBuffer;
                await using (pdfStream)
                {
                    using var ms = new MemoryStream();
                    await pdfStream.CopyToAsync(ms);
                    pdfBuffer = ms.ToArray();
                }

                var chunkSize = int.TryParse(config["Chunking:ChunkSize"], out var cs) ? cs : 500;
                var overlap   = int.TryParse(config["Chunking:Overlap"],    out var ov) ? ov : 50;

                var chunks = await extractor.ExtractChunksAsync(new MemoryStream(pdfBuffer), chunkSize, overlap);

                // PDF scan ảnh (không có text layer) → thử OCR qua Gemini Vision
                if (chunks.Count == 0)
                    chunks = await ocr.OcrPdfAsync(pdfBuffer, chunkSize, overlap);

                var fileId = pdfFile.Id;
                docChunks = chunks.Count > 0
                    ? chunks.Select(c => BuildChunkDoc(ebookId, c.PageNumber, c.ChunkIndex, c.Text,
                          fileVersion, now, item, fileId, dcSubject, dcDescription, dcLanguage, dcIdentifier, dcType, dcContributor)).ToList()
                    : [BuildChunkDoc(ebookId, 0, 0, "",
                          fileVersion, now, item, fileId, dcSubject, dcDescription, dcLanguage, dcIdentifier, dcType, dcContributor)];
            }
            else
            {
                // 4b. Không có PDF → tạo 1 chunk metadata-only
                docChunks = [BuildChunkDoc(ebookId, 0, 0, "",
                    fileVersion, now, item, null, dcSubject, dcDescription, dcLanguage, dcIdentifier, dcType, dcContributor)];
            }

            // 5. Gen embedding cho từng chunk (bỏ qua nếu content rỗng). Máy chủ embedding lỗi thì dừng ở chunk
            // đầu tiên thay vì chờ timeout cho từng chunk (hàng trăm trang × 60s) — chunk vẫn được index không kèm
            // vector, chat hỏi đáp vẫn tìm được bằng từ khóa (BM25); chạy EmbeddingReindexJob sau để bổ sung vector
            // (port ELIB-LRC 09-29).
            foreach (var doc in docChunks.Where(d => !string.IsNullOrWhiteSpace(d.Content)))
            {
                try { doc.Embedding = await embedding.EmbedAsync(doc.Content); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Embedding thất bại ở chunk {Id} — index các chunk còn lại không kèm vector", doc.ChunkId);
                    break;
                }
            }

            // 6. Bulk index vào ES
            await elastic.UpsertChunksAsync(docChunks);

            // 7. Cập nhật DB: IndexedAt, FileVersion++
            item.IndexedAt    = DateTime.Now;
            item.FileVersion  = fileVersion + 1;
            item.ErrorMessage = null;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            item.ErrorMessage = ex.Message;
            try { await db.SaveChangesAsync(); } catch { }
            throw; // Hangfire retry
        }
    }

    private static EbookChunkDocument BuildChunkDoc(
        long ebookId, int pageNumber, int chunkIndex, string content,
        int fileVersion, DateTime now, EbookItem item, long? ebookFileId,
        string? dcSubject, string? dcDescription, string? dcLanguage,
        string? dcIdentifier, string? dcType, string? dcContributor)
    {
        return new EbookChunkDocument
        {
            ChunkId      = $"{ebookId}_p{pageNumber:D4}_c{chunkIndex:D2}",
            EbookId      = ebookId,
            PublicId     = item.PublicId,
            PageNumber   = pageNumber,
            ChunkIndex   = chunkIndex,
            Content      = content,
            FileVersion  = fileVersion,
            IndexedAt    = now,
            EbookFileId  = ebookFileId,

            Title        = item.ItemXml?.Title,
            Author       = item.ItemXml?.Author,
            Publisher    = item.ItemXml?.Publisher,
            PublishDate  = item.ItemXml?.PublishDate,
            Keyword      = item.ItemXml?.Keyword,
            Images       = item.Images,

            CollectionId   = item.CollectionId?.ToString(),
            CollectionName = item.Collection?.Name,
            TopicId        = item.TopicId?.ToString(),
            TopicName      = item.Topic?.Name,
            SubjectId      = item.SubjectId?.ToString(),
            SubjectName    = item.Subject?.Name,

            Free      = item.Free  == 1,
            Share     = item.Share == 1,
            TenantId  = item.TenantId,

            DcSubject     = dcSubject,
            DcDescription = dcDescription,
            DcLanguage    = dcLanguage,
            DcIdentifier  = dcIdentifier,
            DcType        = dcType,
            DcContributor = dcContributor,
        };
    }
}
