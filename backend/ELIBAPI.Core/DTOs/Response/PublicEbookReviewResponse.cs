namespace ELIBAPI.Core.DTOs.Response;

public class PublicEbookReviewSearchRequest : ELIBAPI.Core.DTOs.Request.PublicSearchRequest
{
    public Guid? ItemId { get; set; }
    public int? Rating { get; set; }
}

public class PublicEbookReviewRequest
{
    public Guid? ItemId { get; set; }
    public int? Rating { get; set; }
    public string? DisplayName { get; set; }
    public string? Content { get; set; }
    public string? Email { get; set; }
}

public class PublicEbookReviewResponse
{
    public long Id { get; set; }
    public long? ItemId { get; set; }
    public int? Rating { get; set; }
    public string? DisplayName { get; set; }
    public string? Content { get; set; }
    public int? Status { get; set; }
    public DateTime? CreatedRowDate { get; set; }
}
