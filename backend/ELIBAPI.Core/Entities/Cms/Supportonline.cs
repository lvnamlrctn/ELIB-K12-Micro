using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("Supportonline", Schema = "cms")]
public class Supportonline
{
    [Key] public long    Id       { get; set; }
    public string? Name     { get; set; }
    public string? Yahoo    { get; set; }
    public string? Skype    { get; set; }
    public string? Facebook { get; set; }
    public string? Email    { get; set; }
    public string? Phone    { get; set; }
    public string? Mobile   { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? Wellcome { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

