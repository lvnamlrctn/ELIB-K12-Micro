namespace ELIBAPI.Core.DTOs.Elasticsearch;

public class RagChunk
{
    public long   EbookId  { get; set; }        // dùng để GroupBy dedup nội bộ
    public long?  EbookFileId { get; set; }     // internal EbookFile.Id — resolve sang PublicId trước khi trả ra ChatSource
    public string PublicId { get; set; } = "";  // dùng cho ChatSource.EbookId (giữ nguyên contract Guid)
    public string? Title { get; set; }
    public string? Author { get; set; }
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = "";
    public double Score { get; set; }
}
