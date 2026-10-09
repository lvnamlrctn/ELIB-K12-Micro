using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("IntroBooks", Schema = "Ebook")]
public class IntroBooks
{
    [Key] public long      Id                  { get; set; }
    public string    Title               { get; set; } = string.Empty;
    public string?   Brief               { get; set; }
    public string?   Noidung             { get; set; }
    public string?   Image               { get; set; }
    public DateTime? Submited            { get; set; }
    public string?   PortalId            { get; set; }
    public string?   Language            { get; set; }
    public int?      Order               { get; set; }
    public long?     IntroBookCategoryId { get; set; }
    public long?     BibId               { get; set; }
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

