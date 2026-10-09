using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>Metadata phân công cho Trung tâm công việc (Đợt 15 — port từ ELIB-LRC
/// <c>AdminWorkAssignment</c>). Không đụng cột nào ở 3 bảng nguồn (RoomBooking/DocumentSubmission/Review)
/// — hồ sơ chưa từng có dòng ở đây vẫn hiện "Chưa phân công" nhờ LEFT JOIN phía đọc, không cần backfill.
/// Chống ghi đè bằng compare-and-swap trên <see cref="Version"/> qua <c>ExecuteUpdateAsync</c> (đúng idiom
/// đã dùng ở <see cref="AdminTask.Version"/>/<c>AdminTaskChunks.cs</c>), không phải rowversion EF chuẩn.
/// <see cref="TenantId"/> — khác ELIB-LRC (đơn tenant, không có cột này): null = tài khoản đặc quyền xem
/// hết, đúng quy ước <see cref="AdminTask.TenantId"/>/<c>BaseApiController.GetTenantId()</c>.</summary>
[Table("AdminWorkAssignment", Schema = "dbo")]
public class AdminWorkAssignment
{
    /// <summary>"room-booking" | "submission" | "review".</summary>
    public string SourceType     { get; set; } = "";
    public Guid   SourcePublicId { get; set; }

    public long?     AssigneeId   { get; set; }
    public DateTime? DueAtUtc     { get; set; }
    public int        Version      { get; set; }
    public long        UpdatedBy    { get; set; }
    public DateTime    UpdatedAtUtc { get; set; }
    public long?        TenantId    { get; set; }
}
