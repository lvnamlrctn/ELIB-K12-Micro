using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>Tác vụ nền cho thao tác hàng loạt tốn thời gian (Đợt 10, nền tảng "AdminTask v2" — port từ
/// ELIB-LRC). Chunk hoá 200 bản ghi/lần (<see cref="ELIBAPI.Infrastructure.Services.AdminTaskService"/>),
/// worker claim bằng compare-and-swap trên (Attempts, State) lồng trong transaction Serializable — không
/// dùng lock DB tường minh, đúng cơ chế đã áp dụng cho race-condition đặt phòng ở Đợt 7.
/// <see cref="Payload"/>/<see cref="AdminTaskChunk.Payload"/> mã hoá AES-GCM
/// (<see cref="ELIBAPI.Infrastructure.Services.AdminTaskCrypto"/>); <see cref="Result"/> lưu JSON thường
/// (không mã hoá — đầu ra đã qua duyệt, ít nhạy cảm hơn đầu vào thô).
/// State: Queued | Running | Paused | Completed | Cancelled | NeedsReview | Failed (chuỗi thường, không
/// enum — khớp cách dùng trực tiếp trong các <c>ExecuteUpdateAsync</c> điều kiện).</summary>
[Table("AdminTask", Schema = "dbo")]
public class AdminTask
{
    [Key] public Guid Id { get; set; }
    public long ActorId { get; set; }

    /// <summary>Đa tenant — ELIB khác ELIB-LRC (đơn tenant, không có cột này). null = tài khoản đặc quyền
    /// không tenant (đúng quy ước <c>BaseApiController.GetTenantId()</c>). Stamp lúc Enqueue từ tenant của
    /// người tạo; danh sách "tác vụ của tôi" lọc theo cột này.</summary>
    public long? TenantId { get; set; }

    [MaxLength(40)] public string Kind { get; set; } = "";
    [MaxLength(24)] public string State { get; set; } = "Queued";
    public bool Preview { get; set; }
    public string? Payload { get; set; } = "";
    public string? Result { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int Version { get; set; }
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int ConsecutiveFailures { get; set; }
    public bool StopRequested { get; set; }

    /// <summary>Tác vụ xác nhận (Preview=false) trỏ về Id của tác vụ xem trước (Preview=true) đã sinh ra
    /// token này — dùng để truy vết preview→confirm (Đợt 13 sẽ dùng cho giám sát/dọn dữ liệu).</summary>
    public Guid? SourcePreviewId { get; set; }

    /// <summary>Token xác nhận dạng "{unixExpiry}:{operationIdN}:{hmacHex}", chỉ có ở tác vụ Preview đã
    /// hoàn tất — xem <see cref="ELIBAPI.Infrastructure.Services.AdminMutationGuard"/>.</summary>
    public string? ReviewToken { get; set; }

    public DateTime? FinishedAt { get; set; }

    /// <summary>Dọn dữ liệu/thời gian lưu (Đợt 13) — chỉ set khi đã dọn thật (không phải dry-run).
    /// <see cref="RetentionHold"/> loại tác vụ khỏi diện dọn dù đủ điều kiện (ví dụ đang bị khiếu nại, cần
    /// giữ lại để đối chiếu). Xem <see cref="ELIBAPI.Infrastructure.Services.AdminTaskRetentionService"/>.</summary>
    public bool RetentionHold { get; set; }
    public DateTime? PayloadPurgedAt { get; set; }
    public DateTime? ResultPurgedAt { get; set; }
}
