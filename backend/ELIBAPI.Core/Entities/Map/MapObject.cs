using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

[Table("MapObject", Schema = "map")]
public class MapObject
{
    [Key] public long Id { get; set; }
    public long    FloorId    { get; set; }
    public string? Name       { get; set; }
    public string? Code       { get; set; }
    public string? ObjectType { get; set; } // ROOM, SHELF, PC, STUDY_SPACE, COUNTER, SERVER_ROOM, OFFICE, OTHER
    // 1=Không gian học tập, 2=Phòng học nhóm thảo luận, 3=Công nghệ và Stem, 4=Khác.
    public int?    Category   { get; set; }
    public double? PositionX  { get; set; } // % 0-100 theo chiều rộng canvas tầng
    public double? PositionY  { get; set; } // % 0-100 theo chiều cao canvas tầng
    public double? Width      { get; set; } // % 0-100
    public double? Height     { get; set; } // % 0-100
    public string? ColorHex   { get; set; }
    public string? IconName   { get; set; }
    public long?   StoreId    { get; set; } // FK-by-convention -> PrintBook.Store.Id (kho vật lý gắn với giá này)
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
