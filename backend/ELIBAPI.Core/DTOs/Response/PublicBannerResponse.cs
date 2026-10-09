namespace ELIBAPI.Core.DTOs.Response;

public class PublicBannerResponse
{
    public string? Url       { get; set; }
    public string? Link      { get; set; }
    public string? Name      { get; set; }
    public int?    SortOrder { get; set; }
    public int?    Status    { get; set; }
}
