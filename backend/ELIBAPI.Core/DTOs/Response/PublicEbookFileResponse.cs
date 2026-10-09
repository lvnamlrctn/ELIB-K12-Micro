namespace ELIBAPI.Core.DTOs.Response;

public class PublicEbookFileResponse
{
    public Guid PublicId { get; set; }
    public string? Url { get; set; }
    public string? Type { get; set; }
    public string? FileType { get; set; }
    public double? FileSize { get; set; }
    public string? FileExt { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
}
