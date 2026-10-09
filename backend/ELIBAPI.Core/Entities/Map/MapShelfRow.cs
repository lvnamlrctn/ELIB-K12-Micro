using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

[Table("MapShelfRow", Schema = "map")]
public class MapShelfRow
{
    [Key] public long Id { get; set; }
    public long    ShelfDetailId { get; set; }
    public int?    RowIndex      { get; set; }
    public string? DdcStart      { get; set; }
    public string? DdcEnd        { get; set; }
    public string? Description   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }

    // Chỉ dùng cho API công khai tra cứu theo DDC (PublicMapShelfRowRepository gán thủ công sau khi join) — không map cột DB.
    [NotMapped] public long?   ShelfObjectId { get; set; }
    [NotMapped] public string? ShelfCode     { get; set; }
    [NotMapped] public string? ShelfName     { get; set; }
    [NotMapped] public int?    FloorNumber   { get; set; }
    [NotMapped] public string? FloorName     { get; set; }
    [NotMapped] public double? PositionX     { get; set; }
    [NotMapped] public double? PositionY     { get; set; }
    [NotMapped] public string? CategoryRange { get; set; }
    [NotMapped] public string? SubjectName   { get; set; }
}
