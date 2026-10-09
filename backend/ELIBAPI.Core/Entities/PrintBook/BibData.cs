using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("BibData", Schema = "PrintBook")]
public class BibData
{
    [Key] public long   BibDataId    { get; set; }
    public long?   BibId        { get; set; }
    public string? Field        { get; set; }
    public string? SubField     { get; set; }
    public string? Data         { get; set; }
    public string? Fk           { get; set; }
    public string? L1           { get; set; }
    public string? L2           { get; set; }
    public string? Title        { get; set; }
    public string? DataUnsign   { get; set; }
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
}

