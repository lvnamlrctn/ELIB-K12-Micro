using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("d_bib_status", Schema = "PrintBook")]
public class DBibStatus
{
    [Key] public string?  Id              { get; set; }
    public string?  Name            { get; set; }
    public string?  Opac_Name       { get; set; }
    public string?  English         { get; set; }
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

