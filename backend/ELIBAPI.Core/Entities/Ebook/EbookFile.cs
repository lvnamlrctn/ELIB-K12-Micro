using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("EbookFile", Schema = "Ebook")]
public class EbookFile
{
    [Key] public long      Id                 { get; set; }
    public string?   Url                { get; set; }
    public string?   Type               { get; set; }
    public int?      IsConvert          { get; set; }
    public long?     EbookId            { get; set; }
    public DateTime? CreatedDate        { get; set; }
    public string?   FileType           { get; set; }
    public double?   FileSize           { get; set; }
    public string?   FileExt            { get; set; }
    public string?   Description        { get; set; }
    public string?   Source             { get; set; }
    public int?      FormatId           { get; set; }
    public string?   CheckSumAlgorithm  { get; set; }
    public int?      SortOrder          { get; set; }
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

