using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

[Table("MapShelfDetail", Schema = "map")]
public class MapShelfDetail
{
    [Key] public long Id { get; set; }
    public long    ObjectId     { get; set; }
    public string? CategoryRange { get; set; }
    public string? SubjectName   { get; set; }
    public int?    Capacity      { get; set; }
    public string? Description   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}
