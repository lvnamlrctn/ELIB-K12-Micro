using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("ADS", Schema = "cms")]
public class ADS
{
    [Key] public long     Id          { get; set; }
    public string?  Name        { get; set; }
    public string?  Image       { get; set; }
    public int?     Width       { get; set; }
    public int?     Height      { get; set; }
    public string?  Link        { get; set; }
    public int?     Status      { get; set; }
    public int?     SortOrder   { get; set; }
    public long?    AdsGroupId  { get; set; }
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

