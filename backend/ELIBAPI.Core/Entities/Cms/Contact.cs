using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("Contact", Schema = "cms")]
public class Contact
{
    [Key] public long      Id             { get; set; }
    public string?   Name           { get; set; }
    public string?   Email          { get; set; }
    public string?   Content        { get; set; }
    public string?   Mobile         { get; set; }
    public string?   Address        { get; set; }
    public DateTime? CreatedDate    { get; set; }
    public int?      ContactGroupId { get; set; }
    public int?      Status         { get; set; }
    public string?   PortalId       { get; set; }
    public string?   Language       { get; set; }
    public string?   Title          { get; set; }
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

