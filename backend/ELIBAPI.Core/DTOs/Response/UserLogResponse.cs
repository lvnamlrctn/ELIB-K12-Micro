namespace ELIBAPI.Core.DTOs.Response;

public class UserLogResponse
{
    public long      Id           { get; set; }
    public long?     UserId       { get; set; }
    public string?   FullName     { get; set; }
    public string?   ActionType   { get; set; }
    public string?   Object       { get; set; }
    public string?   Action       { get; set; }
    public DateTime? Submited     { get; set; }
    public string?   Ip           { get; set; }
    public string?   Application  { get; set; }
    public string?   PortalId     { get; set; }
    public long?     TenantId { get; set; }
    public string?   TenantName { get; set; }
}

