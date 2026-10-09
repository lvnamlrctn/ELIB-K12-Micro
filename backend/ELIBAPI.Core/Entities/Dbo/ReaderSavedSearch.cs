using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>Tìm kiếm đã lưu của bạn đọc (Đợt 9, port từ ELIB-LRC ReaderSavedSearch) — tối đa 20/bạn đọc,
/// job nền định kỳ so khớp top-50 kết quả mới nhất với KnownIdsJson để phát hiện kết quả mới, dồn vào
/// MatchesJson (tối đa 200) chờ bạn đọc đọc. Version dùng cho ghi optimistic-concurrency trong job nền
/// (ExecuteUpdateAsync có điều kiện Version — bỏ qua nếu bạn đọc vừa sửa/xoá/đọc entry này ở nơi khác).
/// Có TenantId (khác LRC đơn-tenant) dù mọi truy vấn đã lọc theo ReaderId — giữ đúng quy ước ELIB.</summary>
[Table("ReaderSavedSearch", Schema = "dbo")]
public class ReaderSavedSearch
{
    [Key] public long Id { get; set; }
    public long ReaderId { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    /// <summary>UnifiedSearchRequest tuần tự hoá JSON.</summary>
    public string RequestJson { get; set; } = "{}";
    public bool AlertsEnabled { get; set; } = true;
    /// <summary>Tập GroupId đã biết (mốc so sánh để phát hiện kết quả mới) — JSON array, tối đa 2000.</summary>
    public string KnownIdsJson { get; set; } = "[]";
    /// <summary>Kết quả mới chưa đọc — JSON array [{id,title,source}], tối đa 200.</summary>
    public string MatchesJson { get; set; } = "[]";
    public bool HasUnread { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public long Total { get; set; }
    public Guid Version { get; set; }
    public int? IsDelete { get; set; }
    public long? CreatedRowBy { get; set; }
    public long? UpdateRowBy { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long? TenantId { get; set; }
    public Guid PublicId { get; set; }
}
