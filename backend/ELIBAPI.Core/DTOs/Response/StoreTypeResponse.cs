namespace ELIBAPI.Core.DTOs.Response;

public class StoreTypeTreeResponse
{
    public long    Id          { get; set; }
    public string? Name        { get; set; }
    public long?   ParentId    { get; set; }
    public string? ParentName  { get; set; }
    public Guid    PublicId    { get; set; }
    public long?   TenantId    { get; set; }
    public string? TenantName  { get; set; }
    public bool    HasChildren { get; set; }
    public int     ChildCount  { get; set; }
    public List<StoreTypeTreeResponse> Children { get; set; } = new();
}
