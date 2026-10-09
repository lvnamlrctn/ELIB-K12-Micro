namespace ELIBAPI.Core.DTOs.Response;

public class MenuTreeResponse
{
    public long    Id          { get; set; }
    public string? Name        { get; set; }
    public long?   ParentId    { get; set; }
    public long?   MenuType    { get; set; }
    public string? Link        { get; set; }
    public string? FriendUrl   { get; set; }
    public int?    SortOrder   { get; set; }
    public int?    Status      { get; set; }
    public string? OpenType    { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
    public string? LinkType    { get; set; }
    public string? SubId       { get; set; }
    public int?    IsLogIn     { get; set; }
    public string? Icon        { get; set; }
    public Guid    PublicId    { get; set; }
    public long?   TenantId    { get; set; }
    public string? TenantName  { get; set; }
    public bool    HasChildren { get; set; }
    public int     ChildCount  { get; set; }
    public List<MenuTreeResponse> Children { get; set; } = new();
}
