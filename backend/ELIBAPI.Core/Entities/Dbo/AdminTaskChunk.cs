using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>1 nhóm tối đa 200 bản ghi trong 1 <see cref="AdminTask"/> — worker claim đúng 1 chunk/lượt
/// bằng <c>Position == task.CompletedChunks</c>, an toàn nhờ (TaskId, Position) là unique constraint.
/// <see cref="Payload"/> mã hoá (slice request của riêng chunk này); <see cref="Result"/> JSON thường.</summary>
[Table("AdminTaskChunk", Schema = "dbo")]
public class AdminTaskChunk
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public int Position { get; set; }
    public int ItemCount { get; set; }
    public string? Payload { get; set; } = "";
    public string? Result { get; set; }
    public bool Completed { get; set; }
}

/// <summary>Nhịp tim của 1 tiến trình AdminTaskWorker — dùng để phát hiện lệch khoá mã hoá
/// (<see cref="KeyFingerprint"/>) giữa các instance worker cùng chạy trong 1 môi trường, không phải cơ chế
/// bầu leader. <see cref="Id"/> sinh ngẫu nhiên 1 lần lúc worker khởi động (không phải tên máy) — instance
/// khởi động lại sẽ ghi 1 dòng mới, dòng cũ tự hết hạn khỏi cửa sổ 2 phút mà không cần dọn tường minh.</summary>
[Table("AdminWorkerHeartbeat", Schema = "dbo")]
public class AdminWorkerHeartbeat
{
    [Key, MaxLength(100)] public string Id { get; set; } = "";
    public DateTime LastSeen { get; set; }
    [MaxLength(64)] public string KeyFingerprint { get; set; } = "";
}
