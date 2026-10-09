using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

/// Đầu đọc / cửa của hệ thống kiểm soát truy cập. Code = địa chỉ cửa mà phần mềm kiểm soát gửi lên khi quẹt thẻ; MapObjectId = phòng
/// cửa này mở (null = cửa chung, chỉ thẻ quản trị mở được). Phần mềm kiểm soát xác thực bằng khoá API riêng của thiết bị
/// (chỉ lưu SHA-256, khoá gốc hiện 1 lần khi tạo/cấp lại). Tenant (K12): thiết bị thuộc 1 đơn vị — API thiết bị suy ra đơn vị từ
/// thiết bị đã xác thực, không nhận đơn vị từ request.
[Table("AccessDevice", Schema = "map")]
public class AccessDevice
{
    [Key] public long Id { get; set; }
    public string    Code           { get; set; } = "";
    public string?   Name           { get; set; }
    public long?     MapObjectId    { get; set; }
    /// Chỉ lưu hash; không bao giờ trả ra API.
    [System.Text.Json.Serialization.JsonIgnore] public string ApiKeyHash { get; set; } = "";
    public bool      IsActive       { get; set; } = true;
    public DateTime? LastSeenAt     { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    /// Đơn vị (K12) — suy ra quyền của thiết bị / thẻ: chỉ đối chiếu bạn đọc, lượt đặt cùng đơn vị.
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }

    [NotMapped] public string? RoomName { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

/// Thẻ quản trị (thẻ của thủ thư): quẹt ở bất kỳ cửa nào của đơn vị cũng mở, có ghi nhật ký. TenantId null = thẻ của tài khoản hệ
/// thống, mở mọi cửa.
[Table("AccessStaffCard", Schema = "map")]
public class AccessStaffCard
{
    [Key] public long Id { get; set; }
    public string    CardUid        { get; set; } = "";
    public long?     UserId         { get; set; }
    public string?   Label          { get; set; }
    public bool      IsActive       { get; set; } = true;
    public long?     CreatedRowBy   { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    /// Đơn vị (K12) — suy ra quyền của thiết bị / thẻ: chỉ đối chiếu bạn đọc, lượt đặt cùng đơn vị.
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}

/// Nhật ký mọi lần quẹt thẻ / mở cửa (kể cả bị từ chối). Source: 1 thẻ bạn đọc, 2 thẻ quản trị, 3 thủ thư mở từ web.
[Table("AccessScanLog", Schema = "map")]
public class AccessScanLog
{
    [Key] public long Id { get; set; }
    public long?     DeviceId       { get; set; }
    public string?   DeviceCode     { get; set; }
    public long?     MapObjectId    { get; set; }
    public string?   CardUid        { get; set; }
    public long?     ReaderId       { get; set; }
    public long?     StaffUserId    { get; set; }
    public long?     BookingId      { get; set; }
    public int       Source         { get; set; }
    public bool      Allowed        { get; set; }
    public string?   Reason         { get; set; }
    /// Giờ quét do thiết bị gửi (tham khảo); quyết định luôn theo giờ máy chủ CreatedAt.
    public DateTime? ScannedAt      { get; set; }
    /// Đơn vị (K12) — suy ra quyền của thiết bị / thẻ: chỉ đối chiếu bạn đọc, lượt đặt cùng đơn vị.
    public long?     TenantId       { get; set; }
    public DateTime  CreatedAt      { get; set; }

    [NotMapped] public string? RoomName     { get; set; }
    [NotMapped] public string? ReaderName   { get; set; }
    [NotMapped] public string? ReaderCardNo { get; set; }
}

/// Lệnh gửi xuống thiết bị (hiện chỉ "OPEN" — thủ thư mở cửa từ web). Phần mềm kiểm soát lấy lệnh chờ định kỳ và xác nhận;
/// lệnh quá ExpiresAt không giao nữa.
[Table("AccessCommand", Schema = "map")]
public class AccessCommand
{
    [Key] public long Id { get; set; }
    public long      DeviceId       { get; set; }
    public string    Command        { get; set; } = "OPEN";
    public long?     RequestedBy    { get; set; }
    public long?     BookingId      { get; set; }
    public string?   Note           { get; set; }
    public DateTime  RequestedAt    { get; set; }
    public DateTime  ExpiresAt      { get; set; }
    public DateTime? DeliveredAt    { get; set; }
    public DateTime? AckAt          { get; set; }
    /// Đơn vị (K12) — suy ra quyền của thiết bị / thẻ: chỉ đối chiếu bạn đọc, lượt đặt cùng đơn vị.
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}
