using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

[Table("ScheduledReport", Schema = "dbo")]
public class ScheduledReport
{
    [Key] public long Id { get; set; }
    public string? Name { get; set; }
    public string? ReportType { get; set; }        // "CIRCULATION" | "STORE_BOOK"
    public string? ReportParamsJson { get; set; }   // snapshot filter (không gồm khoảng ngày — tính theo FrequencyType lúc chạy)
    public int FrequencyType { get; set; }          // 1 = Daily, 2 = Weekly, 3 = Monthly
    public int? DayOfWeek { get; set; }             // dùng khi FrequencyType = Weekly (0 = Chủ nhật .. 6 = Thứ 7)
    public int? DayOfMonth { get; set; }            // dùng khi FrequencyType = Monthly
    public string? TimeOfDay { get; set; }          // "HH:mm"
    public string? RecipientEmails { get; set; }    // phân tách bằng dấu phẩy
    public int Status { get; set; } = 2;            // 2 = Hoạt động, 1 = Tạm dừng
    public DateTime? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    // Audit Trail
    public int?      IsDelete       { get; set; }
    public long?      CreatedRowBy   { get; set; }
    public long?      UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public Guid      PublicId       { get; set; }
    public long?     TenantId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}
