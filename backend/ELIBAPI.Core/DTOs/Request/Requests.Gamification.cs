namespace ELIBAPI.Core.DTOs.Request;

// ==================== BADGE ====================
public class BadgeRequest
{
    public string? Code         { get; set; }
    public string? Name         { get; set; }
    public string? Description  { get; set; }
    public string? IconName     { get; set; }
    public string? CriteriaType { get; set; }
    public long?   Threshold    { get; set; }
    public int?    SortOrder    { get; set; }
}
public class BadgeSearchRequest : SearchRequest { }
