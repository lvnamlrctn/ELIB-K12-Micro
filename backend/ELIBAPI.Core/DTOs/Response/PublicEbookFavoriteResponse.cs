namespace ELIBAPI.Core.DTOs.Response;

public class PublicEbookFavoriteSearchRequest : ELIBAPI.Core.DTOs.Request.PublicSearchRequest
{
    public string? ReaderId { get; set; }
    public Guid? ItemId { get; set; }
}

public class PublicEbookFavoriteRequest
{
    public Guid? ItemId { get; set; }
    public string? ReaderId { get; set; }
    public string? Cardnumber { get; set; }
    public Guid? TenantId { get; set; }
}

public class PublicEbookFavoriteResponse
{
    public long Id { get; set; }
    public Guid? ItemId { get; set; }
    public long? ReaderId { get; set; }
    public string? ItemTitle { get; set; }
    public string? Author { get; set; }
    public string? PublishDate { get; set; }
    public string? Images { get; set; }
    public DateTime? CreatedRowDate { get; set; }
}
