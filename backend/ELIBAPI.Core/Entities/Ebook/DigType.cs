using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("DigType", Schema = "Ebook")]
public class DigType
{
    [Key] public long    Id            { get; set; }
    public string? Code          { get; set; }
    public string? DescriptionVn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? PortalId      { get; set; }
    public int?    SortOrder     { get; set; }
    public string? Language      { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

