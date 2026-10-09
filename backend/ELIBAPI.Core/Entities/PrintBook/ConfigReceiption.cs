using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("ConfigReceiption", Schema = "PrintBook")]
public class ConfigReceiption
{
    [Key] public long Id { get; set; }
    public long? UserId          { get; set; }
    public int?  CheckType       { get; set; }
    public int?  AutoCheck       { get; set; }
    public int?  CircPlaceId     { get; set; }
    public int?  RequirePassword { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }

    [NotMapped] public string? TenantName { get; set; }
}

