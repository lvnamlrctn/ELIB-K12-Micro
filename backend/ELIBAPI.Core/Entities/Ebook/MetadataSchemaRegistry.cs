using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("MetadataSchemaRegistry", Schema = "Ebook")]
public class MetadataSchemaRegistry
{
    [Key] public long    MetadataSchemaId { get; set; }
    public string? NameSpace        { get; set; }
    public string? ShortId          { get; set; }
    public string? PortalId         { get; set; }
    public string? Language         { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

