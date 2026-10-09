namespace ELIBAPI.Core.DTOs.Response;

public class EbookTopicTreeResponse
{
    public long    Id              { get; set; }
    public string? Name            { get; set; }
    public long?   ParentId        { get; set; }
    public string? ParentName      { get; set; }
    public long?   Level           { get; set; }
    public int?    Status          { get; set; }
    public int?    Order           { get; set; }
    public string? PortalId        { get; set; }
    public string? Language        { get; set; }
    public int?    IsLogin         { get; set; }
    public string? Description     { get; set; }
    public string? Keyword         { get; set; }
    public string? PageTitle       { get; set; }
    public string? MetaDescription { get; set; }
    public string? DDC             { get; set; }
    public Guid    PublicId        { get; set; }
    public long?   TenantId        { get; set; }
    public string? TenantName      { get; set; }
    public bool    HasChildren     { get; set; }
    public int     ChildCount      { get; set; }
    public List<EbookTopicTreeResponse> Children { get; set; } = new();
}
