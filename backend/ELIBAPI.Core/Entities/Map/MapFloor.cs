using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

[Table("MapFloor", Schema = "map")]
public class MapFloor
{
    [Key] public long Id { get; set; }
    public long    BuildingId     { get; set; }
    public int?    FloorNumber    { get; set; }
    public string? Name           { get; set; }
    public string? LayoutImageUrl { get; set; }
    public double? Width          { get; set; }
    public double? Height         { get; set; }
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}
