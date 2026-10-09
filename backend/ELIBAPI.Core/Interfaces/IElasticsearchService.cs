using ELIBAPI.Core.DTOs.Elasticsearch;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;

namespace ELIBAPI.Core.Interfaces;

public interface IElasticsearchService
{
    // --- Legacy document-level (ebook_items index) ---
    Task UpsertEbookItemAsync(EbookItemDocument document);
    Task BulkUpsertEbookItemsAsync(IEnumerable<EbookItemDocument> documents);

    // --- Chunk-based (ebook_chunks index) ---
    Task UpsertChunksAsync(IEnumerable<EbookChunkDocument> chunks);
    Task DeleteChunksByEbookIdAsync(long ebookId);
    Task UpdateChunkMetadataAsync(long ebookId, EbookChunkDocument metadata);
    Task<EbookSearchResponse> SearchChunksAsync(EbookChunkSearchRequest request);
    Task<List<string>> SuggestAsync(string query, long? tenantId = null);
    Task<EbookSearchResponse> SearchInBookAsync(string ebookId, string keyword);
    Task<PublicEbookElasticResponse> SearchEbooksAsync(PublicEbookElasticSearchRequest request);

    // --- Index GỘP (library_docs): tài liệu in + tài liệu số ---
    Task EnsureUnifiedIndexAsync();
    /// <summary>Đợt 26 — xóa toàn bộ index gộp rồi tạo lại mapping rỗng. Chỉ dùng khi cần build lại từ
    /// đầu (index hỏng/không đồng bộ) — sau khi gọi, phải enqueue lại job reindex in + số.</summary>
    Task DeleteUnifiedIndexAsync();
    Task UpsertLibraryDocumentsAsync(IEnumerable<LibraryDocument> documents);
    Task DeleteLibraryDocsByGroupAsync(string groupId);
    /// <summary>Chuyển tài liệu số từ index chunk cũ sang index gộp (giữ nguyên embedding). Trả về task id.</summary>
    Task<string> MigrateDigitalToUnifiedAsync();
    Task<object> GetTaskStatusAsync(string taskId);
    Task EnrichDigitalCommonFieldsAsync(long ebookId, LibraryDocument meta);
    /// <summary>Tìm kiếm GỘP tài liệu in + tài liệu số trên một index duy nhất.</summary>
    Task<UnifiedSearchResponse> SearchUnifiedAsync(UnifiedSearchRequest request);
    /// <summary>Toàn bộ bibId tài liệu in khớp bộ lọc (thay các truy vấn SQL LIKE).</summary>
    Task<HashSet<long>> SearchPrintBibIdsAsync(PrintBibFilter filter);
    /// <summary>Đợt 22.2 — "Có thể bạn quan tâm": more_like_this theo title/author/keyword của chính tài
    /// liệu, lọc tenant, loại chính tài liệu đó ra khỏi kết quả.</summary>
    Task<List<UnifiedSearchItem>> MoreLikeThisAsync(string groupId, long? tenantId, int size = 8);
    /// <summary>Gợi ý giá trị phân biệt theo tiền tố (không phân biệt dấu) cho 1 trường của index gộp —
    /// dùng cho 4 ô lọc nâng cao (Nhan đề/Tác giả/Nhà xuất bản/Từ khoá) ở trang Tìm kiếm OPAC.</summary>
    Task<List<string>> SuggestFieldAsync(string field, string query, long? tenantId = null);

    // --- RAG: kNN vector search ---
    Task<List<RagChunk>> RetrieveRagChunksAsync(float[] queryVector, long? tenantId = null,
        int topK = 10, string? collectionId = null, string? topicId = null,
        string? subjectId = null, string? language = null, bool? free = null,
        double minScore = 0, int numCandidatesMultiplier = 10, Guid? ebookPublicId = null);

    // --- RAG: tìm theo từ khóa (BM25) trong nội dung 1 tài liệu (hoặc toàn kho khi ebookPublicId null) —
    // ghép với kNN thành tìm kiếm lai, và là đường dự phòng khi máy chủ embedding không phản hồi ---
    Task<List<RagChunk>> RetrieveKeywordChunksAsync(Guid? ebookPublicId, string query, long? tenantId = null, int size = 10);

    // --- RAG: truy xuất tuần tự theo trang (tóm tắt/tổng quan toàn tài liệu) ---
    Task<List<RagChunk>> RetrieveDocumentOverviewChunksAsync(Guid ebookPublicId, long? tenantId = null, int maxPages = 300);

    // --- Embedding re-index support ---
    Task<(List<(string ChunkId, string Content)> Items, string? ScrollId)> ScrollChunksWithoutEmbeddingAsync(string? scrollId, int batchSize = 50);
    Task BulkUpdateEmbeddingsAsync(IEnumerable<(string ChunkId, float[] Embedding)> updates);
    Task<(long Total, long Embedded, long Pending)> GetEmbeddingStatusAsync();

    /// <summary>Đợt 22 — kiểm tra cụm Elasticsearch còn phản hồi (dùng cho /ready).</summary>
    Task<bool> PingAsync();
}
