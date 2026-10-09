using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

/// <summary>Đợt 22.4 — port từ ELIB-LRC. Kết quả lần chạy gần nhất của <c>DigitalStorageAuditJob</c> — đối
/// soát file trên MinIO (bucket riêng tư DÙNG CHUNG mọi đơn vị) với bảng EbookFile để tìm file "mồ côi" +
/// tổng dung lượng lưu trữ. Không theo tenant: bucket vật lý không tách theo đơn vị, nên kết quả chỉ hiện
/// ở tab DevOps (chỉ tài khoản hệ thống — xem DashboardLibraryController.DevOps). Chạy nền định kỳ, KHÔNG
/// quét đồng bộ lúc mở dashboard (bucket có thể có hàng chục nghìn object).</summary>
[Table("DigitalStorageAuditResult", Schema = "Ebook")]
public class DigitalStorageAuditResult
{
    [Key] public long Id { get; set; }
    public DateTime RunAt          { get; set; }
    public long      TotalObjects  { get; set; }
    public long      TotalSizeBytes{ get; set; }
    public long      OrphanCount   { get; set; }
    public long      DbFileCount   { get; set; }
    /// <summary>Danh sách JSON tối đa 100 object mồ côi đầu tiên (object name + size).</summary>
    public string?   OrphanSampleJson { get; set; }
    /// <summary>Số dòng EbookFile đang hoạt động nhưng Url không khớp bất kỳ object nào còn tồn tại
    /// trên bucket (file đã mất/hỏng link) — chiều ngược lại với "mồ côi".</summary>
    public long?     BrokenFileCount  { get; set; }
    /// <summary>Danh sách JSON tối đa 100 dòng EbookFile hỏng đầu tiên (publicId + ebookId + url).</summary>
    public string?   BrokenSampleJson { get; set; }
}
