using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("ModuleRoles", Schema = "cms")]
public class ModuleRoles
{
    [Key] public long   Id         { get; set; }
    public long?  RolesId    { get; set; }
    public long?  ModuleId   { get; set; }
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
    public byte?  Can_access { get; set; }
    public byte?  Can_add    { get; set; }
    public byte?  Can_edit   { get; set; }
    public byte?  Can_delete { get; set; }
    public byte?  Can_view   { get; set; }
    // Audit Trail
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
}

