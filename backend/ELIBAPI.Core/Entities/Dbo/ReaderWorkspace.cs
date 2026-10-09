using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>Không gian nghiên cứu của bạn đọc (Đợt 9, port từ ELIB-LRC ReaderWorkspace) — 1 dòng/bạn đọc
/// (PK = ReaderId), đồng bộ đa thiết bị qua 1 khối JSON mờ (projects/highlights tự do phía client), ghi
/// bằng compare-and-swap trên Version (client gửi Version cũ, server so khớp trước khi ghi — khác Version
/// thì trả 409, không tự hợp nhất từng trường). Có TenantId (khác LRC đơn-tenant) dù ReaderId đã đủ khoá
/// 1-1, giữ đúng quy ước ELIB cho mục đích thống kê/quản trị sau này.</summary>
[Table("ReaderWorkspace", Schema = "dbo")]
public class ReaderWorkspace
{
    // PK = ReaderId thật (không phải identity tự sinh) — EF Core mặc định coi PK kiểu long là identity,
    // phải khai rõ None để INSERT giữ đúng giá trị ReaderId gán tay trong ReaderWorkspaceService.
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long ReaderId { get; set; }
    public Guid Version { get; set; }
    /// <summary>{"projects":[...],"highlights":[...]} — cấu trúc tự do phía client, server chỉ validate
    /// khoá bắt buộc + giới hạn kích thước, không diễn giải nội dung.</summary>
    public string SnapshotJson { get; set; } = "{\"projects\":[],\"highlights\":[]}";
    public DateTime UpdatedAt { get; set; }
    public long? TenantId { get; set; }
}
