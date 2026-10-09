namespace ELIBAPI.Core.DTOs.Response;

public class PublicEbookResponse
{
    public int Index { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public string? PublishDate { get; set; }
    public string? Images { get; set; }


    public string? Url { get; set; }
    public string? Keyword { get; set; }
    public string? Xml { get; set; }
    public string? OtherTitle { get; set; }
    public string? CollectionName { get; set; }
    public string? SubjectName { get; set; }
    public string? ToPicName { get; set; }
    public int? TotalView { get; set; }
    public int? TotalDownload { get; set; }
    public int? Status { get; set; }


    public int? AllowDownload { get; set; }
    public int?  IsFree       { get; set; }

    public string? Page { get; set; }
    public DateTime? Submited { get; set; }
    public DateTime? CreatedDate { get; set; }
    public Guid PublicId { get; set; }

}
