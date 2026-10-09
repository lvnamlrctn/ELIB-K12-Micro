using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

/// Giờ mở cửa đặt phòng theo thứ trong tuần (Weekday 0 = Chủ nhật … 6 = Thứ bảy) cho 1 loại cơ sở (MapObject.Category,
/// null = mọi loại). Giờ "HH:mm" theo giờ thư viện. Không có dòng nào thì dùng ROOM_BOOKING_OPEN/CLOSE_TIME.
/// Tenant (K12): dòng của đơn vị được ưu tiên, không có thì dùng dòng dùng chung (TenantId null).
[Table("RoomOpeningHour", Schema = "map")]
public class RoomOpeningHour
{
    [Key] public long Id { get; set; }
    public int?      Category       { get; set; }
    public int       Weekday        { get; set; }
    public string?   OpenTime       { get; set; }
    public string?   CloseTime      { get; set; }
    public bool      IsClosed       { get; set; }
    /// Đơn vị (K12): null = cấu hình dùng chung mọi đơn vị.
    public long?     TenantId       { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
}
