using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("Page", Schema = "cms")]
public class Page
{
    [Key] public long    Id       { get; set; }
    public string? PageCode { get; set; }
    public string? PageName { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

