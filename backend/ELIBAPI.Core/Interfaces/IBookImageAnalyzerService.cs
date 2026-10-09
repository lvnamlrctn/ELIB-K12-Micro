namespace ELIBAPI.Core.Interfaces;

public class BookMetadataResult
{
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishYear { get; set; }
    public string? Isbn        { get; set; }
    public string? Language    { get; set; }
    public string? Description { get; set; }
}

public interface IBookImageAnalyzerService
{
    Task<BookMetadataResult> AnalyzeAsync(IEnumerable<Stream> images);
}
