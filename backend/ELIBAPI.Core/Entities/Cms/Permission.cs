using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("Permission", Schema = "cms")]
public class Permission
{
    [Key] public long   Id         { get; set; }
    public long?  UserId     { get; set; }
    public long?  ModuleId   { get; set; }
    public byte?  Can_Access { get; set; }
    public byte?  Can_Add    { get; set; }
    public byte?  Can_Edit   { get; set; }
    public byte?  Can_Delete { get; set; }
    public byte?  Can_View   { get; set; }
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
    // Audit Trail
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
}

