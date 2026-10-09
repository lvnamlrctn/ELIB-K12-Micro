using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

[Table("MapEquipment", Schema = "map")]
public class MapEquipment
{
    [Key] public long Id { get; set; }
    public long    ObjectId        { get; set; }
    public string? Name            { get; set; }
    public string? Description     { get; set; }
    public int?    Quantity        { get; set; }
    public string? ConditionStatus { get; set; } // Sẵn sàng, Hoạt động, Bảo trì...
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}
