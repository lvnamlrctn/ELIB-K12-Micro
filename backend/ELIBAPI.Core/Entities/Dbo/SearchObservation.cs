using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>Ghi nhận 1 lượt tìm kiếm OPAC (trang 1, có từ khoá) để tính thống kê chất lượng tìm kiếm
/// (/admin/search-quality, Đợt 9) — KHÔNG lưu ReaderId/JWT/IP, chỉ chuỗi truy vấn + số liệu, tự động dọn
/// sau 90 ngày (RecurringJob "search-observation-purge"). Không đi qua BaseRepository/GenericController
/// (thuần insert-only + đọc gộp thống kê) nên không có PublicId/IsDelete/audit chuẩn như entity CRUD khác.</summary>
[Table("SearchObservation", Schema = "dbo")]
public class SearchObservation
{
    [Key] public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    [MaxLength(200)] public string Query { get; set; } = "";
    /// <summary>"print" | "digital" | "all".</summary>
    [MaxLength(16)] public string DocType { get; set; } = "all";
    public long Total { get; set; }
    public long ElapsedMs { get; set; }
    public bool Failed { get; set; }
    public bool UsedFallback { get; set; }
    /// <summary>Đa tenant — ELIB khác ELIB-LRC (đơn tenant, không có cột này): báo cáo Search Quality
    /// phải lọc đúng tenant của nhân viên xem, không được thấy dữ liệu tenant khác.</summary>
    public long? TenantId { get; set; }
}
