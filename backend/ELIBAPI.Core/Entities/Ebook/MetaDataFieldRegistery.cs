using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("MetaDataFieldRegistery", Schema = "Ebook")]
public class MetaDataFieldRegistery
{
    [Key] public long    MetaDataFieldId  { get; set; }
    public long?   MetaDataSchemaId { get; set; }
    public string? Field            { get; set; }
    public string? Subfield         { get; set; }
    public string? DescriptionVn    { get; set; }
    public string? DescriptionEn    { get; set; }
    public int?    SortOrder        { get; set; }
    public int?    Status           { get; set; }
    public string? Input            { get; set; }
    public int?    ExportField      { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

