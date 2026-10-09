using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("MetaDataValue", Schema = "Ebook")]
public class MetaDataValue
{
    [Key] public long    Id              { get; set; }
    public int?    MetaDataFieldId { get; set; }
    public string? Value           { get; set; }
    public string? Language        { get; set; }
    public long?   ItemId          { get; set; }
    public string? Value_UnSign    { get; set; }
    public int?    SortOrder       { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

