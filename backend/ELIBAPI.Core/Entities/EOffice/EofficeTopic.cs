using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.EOffice;

[Table("Topic", Schema = "EOffice")]
public class EofficeTopic
{
    [Key] public long    Id       { get; set; }
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public long?   Level    { get; set; }
    public int?    Status   { get; set; }
    public int?    Order    { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? Link     { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

