using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>Nhật ký 1 lượt quét dọn Payload/Result của AdminTask đã hết hạn lưu (Đợt 13 — port từ
/// ELIB-LRC). Ghi cả lượt dry-run và lượt chạy thật để đối chiếu số liệu. Xem
/// <see cref="ELIBAPI.Infrastructure.Services.AdminTaskRetentionService"/>.</summary>
[Table("AdminTaskRetentionRun", Schema = "dbo")]
public class AdminTaskRetentionRun
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public bool DryRun { get; set; }
    public int TaskCount { get; set; }
    public int ChunkCount { get; set; }
    public long EstimatedBytes { get; set; }
}
