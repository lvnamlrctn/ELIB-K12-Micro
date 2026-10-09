namespace ELIBAPI.Core.DTOs.Response;

public class PublicHyperLinkResponse
{
    public Guid PublicId { get; set; }
    public string? Name { get; set; }
    public string? LinkUrl { get; set; }
    public string? Description { get; set; }
    public string? Images { get; set; }
    public int? Status { get; set; }
    public Guid? LinkGroupPublicId { get; set; }
}
