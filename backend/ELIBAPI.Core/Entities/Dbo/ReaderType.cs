using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

[Table("ReaderType", Schema = "dbo")]
public class ReaderType
{
    [Key] public long   Id       { get; set; }
    public string  Name     { get; set; } = string.Empty;
    public string  PortalId { get; set; } = string.Empty;
    public string  Language { get; set; } = string.Empty;
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

