using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Cms;

[Table("Module", Schema = "cms")]
public class Module
{
    [Key] public long    Id         { get; set; }
    public string? Name       { get; set; }
    public string? Link       { get; set; }
    public string? Icon       { get; set; }
    public long?   ParentId   { get; set; }
    public string? Language   { get; set; }
    public string? PortalId   { get; set; }
    public long?   Group      { get; set; }
    public int?    SortOrder  { get; set; }
    public string? ModuleCode { get; set; }
    public string? Type       { get; set; }
    public string? FolderPage { get; set; }
    public int?    Status     { get; set; }
    // Audit Trail
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
}

