using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

[Table("MapFloorUtility", Schema = "map")]
public class MapFloorUtility
{
    [Key] public long Id { get; set; }
    public long    FloorId         { get; set; }
    public string? Name            { get; set; }
    public string? Description     { get; set; }
    public int?    Quantity        { get; set; }
    public string? ConditionStatus { get; set; } // HOAT_DONG, BAO_TRI, HU_HONG
    public string? IconName        { get; set; }
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}
