using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>Cấu hình bảng danh sách theo tài khoản (Đợt 21 — port từ ELIB-LRC): bộ lọc, số dòng, cột hiển thị
/// và thứ tự cột của 1 trang quản trị. Khoá (ActorId, PageKey). <see cref="Revision"/> chống ghi đè giữa 2 phiên.
/// Khác LRC (đơn tenant): thêm <see cref="TenantId"/> để ghi nhận đơn vị của tài khoản lúc lưu — việc đọc/ghi luôn
/// theo ActorId của JWT nên không cần lọc theo đơn vị.</summary>
[Table("AdminUiPreference", Schema = "dbo")]
public class AdminUiPreference
{
    public long ActorId { get; set; }
    [MaxLength(40)] public string PageKey { get; set; } = "";
    public string SettingsJson { get; set; } = "{}";
    public int Revision { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? TenantId { get; set; }
}
