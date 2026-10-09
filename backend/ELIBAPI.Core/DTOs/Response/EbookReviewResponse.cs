namespace ELIBAPI.Core.DTOs.Response;

public class EbookReviewResponse
{
    public long      Id             { get; set; }
    public Guid?     ItemId         { get; set; }
    public string?   ItemTitle      { get; set; }
    public string?   DisplayName    { get; set; }
    public int?      Rating         { get; set; }
    public string?   Content        { get; set; }
    public string?   Email          { get; set; }
    public int?      Status         { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public Guid      PublicId       { get; set; }
    public string?   TenantName     { get; set; }
}
