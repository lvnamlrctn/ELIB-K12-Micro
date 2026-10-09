using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("LinhVucNghienCuu", Schema = "PrintBook")]
public class LinhVucNghienCuu
{
    [Key] public long Id { get; set; }
    public string? Name     { get; set; }
    public string? Ma       { get; set; }
    public int?    ParentId { get; set; }
    public string? GhiChu  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

