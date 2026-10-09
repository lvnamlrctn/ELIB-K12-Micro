using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("Banner", Schema = "cms")]
public class Banner
{
    [Key] public long     Id        { get; set; }
    public string?  Name      { get; set; }
    public string?  Url       { get; set; }
    public string?  Link      { get; set; }
    public int?     Width     { get; set; }
    public int?     Height    { get; set; }
    public int?     SortOrder { get; set; }
    public int?     Status    { get; set; }
    public string?  PortalId  { get; set; }
    public string?  Language  { get; set; }
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

