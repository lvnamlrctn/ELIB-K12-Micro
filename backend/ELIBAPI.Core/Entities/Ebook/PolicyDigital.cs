using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("PolicyDigital", Schema = "Ebook")]
public class PolicyDigital
{
    [Key] public int    Id           { get; set; }
    public int?   ReaderTypeid { get; set; }
    public int?   Maxpage      { get; set; }
    public double? Maxsize      { get; set; }
    public int?   Maxdocument  { get; set; }
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

