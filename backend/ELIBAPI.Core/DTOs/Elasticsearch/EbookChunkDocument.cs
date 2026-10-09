namespace ELIBAPI.Core.DTOs.Elasticsearch;

public class EbookChunkDocument
{
    // Identity
    public string   ChunkId     { get; set; } = string.Empty;  // "{ebookId}_p{page:D4}_c{chunk:D2}"
    public long     EbookId     { get; set; }                  // EbookItem.Id
    public Guid     PublicId    { get; set; }                  // EbookItem.PublicId — dùng cho tra cứu/response Public
    public int      PageNumber  { get; set; }
    public int      ChunkIndex  { get; set; }
    public string   Content     { get; set; } = string.Empty;
    public int      FileVersion { get; set; }
    public DateTime IndexedAt   { get; set; }

    // Metadata cơ bản (từ EbookItemXml)
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishDate { get; set; }
    public string? Keyword     { get; set; }
    public string? Images      { get; set; }

    // Phân loại (denormalize)
    public string? CollectionId   { get; set; }
    public string? CollectionName { get; set; }
    public string? TopicId        { get; set; }
    public string? TopicName      { get; set; }
    public string? SubjectId      { get; set; }
    public string? SubjectName    { get; set; }

    // File nguồn
    public long? EbookFileId { get; set; }  // EbookFile.Id

    // Quyền truy cập
    public bool  Free     { get; set; }
    public bool  Share    { get; set; }
    public long? TenantId { get; set; }

    // Dublin Core từ MetaDataValue
    public string? DcSubject     { get; set; }  // FieldId 57
    public string? DcDescription { get; set; }  // FieldId 27
    public string? DcLanguage    { get; set; }  // FieldId 38
    public string? DcIdentifier  { get; set; }  // FieldId 20 (ISBN) / 23 (ISSN)
    public string? DcType        { get; set; }  // FieldId 66
    public string? DcContributor { get; set; }  // FieldId 2

    // Embedding (lưu sẵn, index=false — bật sau cho chatbot RAG)
    public float[]? Embedding { get; set; }
}
