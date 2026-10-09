using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

/// Ngày đặc biệt (lễ, đóng cửa đột xuất, giờ rút ngắn) — ghi đè giờ theo thứ. Date là ngày theo giờ thư viện; Category null
/// = mọi loại cơ sở. IsClosed = đóng cả ngày, ngược lại dùng OpenTime/CloseTime ("HH:mm").
/// Tenant (K12): dòng của đơn vị được ưu tiên, không có thì dùng dòng dùng chung (TenantId null).
[Table("RoomSpecialDay", Schema = "map")]
public class RoomSpecialDay
{
    [Key] public long Id { get; set; }
    public DateTime  Date           { get; set; }
    public int?      Category       { get; set; }
    public bool      IsClosed       { get; set; }
    public string?   OpenTime       { get; set; }
    public string?   CloseTime      { get; set; }
    public string?   Reason         { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    /// Đơn vị (K12): null = áp cho mọi đơn vị.
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}
