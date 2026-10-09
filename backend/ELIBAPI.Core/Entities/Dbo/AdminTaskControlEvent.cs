using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>Nhật ký can thiệp (tạm dừng/tiếp tục) của người GIÁM SÁT lên tác vụ của người khác (Đợt 13 —
/// port từ ELIB-LRC). Ghi cùng transaction với đổi trạng thái tác vụ; chống gửi lặp bằng
/// <see cref="RequestId"/> (unique index) — gửi lại đúng RequestId trả lại kết quả cũ, không tạo sự kiện
/// thứ hai. Xem <see cref="ELIBAPI.Infrastructure.Services.AdminTaskControlService"/>.</summary>
[Table("AdminTaskControlEvent", Schema = "dbo")]
public class AdminTaskControlEvent
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public long OperatorId { get; set; }
    public long OwnerId { get; set; }
    [MaxLength(16)] public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    [MaxLength(24)] public string StateBefore { get; set; } = "";
    [MaxLength(24)] public string StateAfter { get; set; } = "";
    [MaxLength(100)] public string RequestId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
