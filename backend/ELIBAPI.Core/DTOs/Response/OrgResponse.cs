namespace ELIBAPI.Core.DTOs.Response;

public class OrgTreeResponse
{
    public long     Id          { get; set; }
    public string?  Name        { get; set; }
    public long?    ParentId    { get; set; }
    public string?  ParentName  { get; set; }
    public long?    Level       { get; set; }
    public int?     Status      { get; set; }
    public int?     Order       { get; set; }
    public string?  PortalId    { get; set; }
    public string?  Language    { get; set; }
    public string?  Link        { get; set; }
    public Guid     PublicId    { get; set; }
    public long?    TenantId    { get; set; }
    public string?  TenantName  { get; set; }
    public bool     HasChildren { get; set; }
    public int      ChildCount  { get; set; }
    public List<OrgTreeResponse> Children { get; set; } = new();
}
