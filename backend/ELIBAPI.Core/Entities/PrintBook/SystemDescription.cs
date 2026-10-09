using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("System_description", Schema = "PrintBook")]
public class SystemDescription
{
    [Key] public int    ID              { get; set; }
    public string? Name           { get; set; }
    public string? Descvn         { get; set; }
    public string? Val            { get; set; }
    public string? Unit           { get; set; }
    public int?    IsDelete       { get; set; }
    public long?   CreatedRowBy   { get; set; }
    public long?   UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?   TenantId   { get; set; }
    public Guid    PublicId       { get; set; }
}

