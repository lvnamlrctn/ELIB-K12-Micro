using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Private", Schema = "PrintBook")]
public class PbPrivate
{
    [Key] public int Id { get; set; }
    public int? RoleId   { get; set; }
    public int? ModuleId { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

