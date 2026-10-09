using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>
/// Quét các cấu hình "Báo cáo định kỳ" (dbo.ScheduledReport) đang hoạt động, xác định dòng nào đến hạn
/// chạy (so FrequencyType/DayOfWeek/DayOfMonth/TimeOfDay với LastRunAt và giờ hiện tại — job chạy mỗi
/// giờ nên so khớp theo độ chính xác giờ), dựng file Excel qua CirculationReportBuilder/
/// StoreBookReportBuilder (đúng TenantId của dòng cấu hình đó) rồi gửi email đính kèm tới danh sách
/// người nhận.
/// </summary>
public class ScheduledReportEmailJob(
    ELIBAPIDbContext db,
    CirculationReportBuilder circulationBuilder,
    StoreBookReportBuilder storeBookBuilder,
    IEmailService emailService,
    ILogger<ScheduledReportEmailJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var now = DateTime.Now;
        var reports = await db.ScheduledReports.Where(x => x.IsDelete != 2 && x.Status == 2).ToListAsync();
        var dueReports = reports.Where(r => IsDue(r, now)).ToList();

        if (dueReports.Count == 0)
        {
            logger.LogInformation("ScheduledReportEmailJob: không có báo cáo nào đến hạn chạy");
            return;
        }

        foreach (var report in dueReports)
        {
            try
            {
                var (bytes, fileName) = await BuildReportAsync(report, now);
                var recipients = (report.RecipientEmails ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                foreach (var email in recipients)
                {
                    await emailService.SendAsync(email, $"Báo cáo định kỳ: {report.Name}",
                        $"Báo cáo \"{report.Name}\" được đính kèm trong email này.",
                        (fileName, bytes));
                }

                report.LastRunAt = now;
                report.LastRunStatus = $"Thành công — đã gửi {recipients.Count} email";
                logger.LogInformation("ScheduledReportEmailJob: đã gửi báo cáo {Name} tới {Count} người nhận", report.Name, recipients.Count);
            }
            catch (Exception ex)
            {
                report.LastRunAt = now;
                report.LastRunStatus = $"Lỗi: {ex.Message}";
                logger.LogError(ex, "ScheduledReportEmailJob: xử lý báo cáo {Name} thất bại", report.Name);
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task<(byte[] Bytes, string FileName)> BuildReportAsync(Core.Entities.Dbo.ScheduledReport report, DateTime now)
    {
        var (dateFrom, dateTo) = DateRangeFor(report.FrequencyType, now);
        var stamp = now.ToString("yyyyMMddHHmmss");

        switch (report.ReportType)
        {
            case "STORE_BOOK":
                var storeReq = new StoreBookReportRequest { DateFrom = dateFrom, DateTo = dateTo };
                return (await storeBookBuilder.BuildExcelBytesAsync(storeReq, report.TenantId), $"sach-trong-kho-{stamp}.xlsx");

            case "CIRCULATION":
            default:
                var circReq = new CirculationReportRequest { ReportType = 1, DateFrom = dateFrom, DateTo = dateTo };
                return (await circulationBuilder.BuildExcelBytesAsync(circReq, report.TenantId), $"bao-cao-luu-thong-{stamp}.xlsx");
        }
    }

    private static (DateTime From, DateTime To) DateRangeFor(int frequencyType, DateTime now) => frequencyType switch
    {
        2 => (now.Date.AddDays(-7), now.Date),   // Weekly: 7 ngày gần nhất
        3 => (now.Date.AddMonths(-1), now.Date), // Monthly: 1 tháng gần nhất
        _ => (now.Date.AddDays(-1), now.Date),   // Daily: 1 ngày gần nhất
    };

    /// Job chạy mỗi giờ (Cron.Hourly) — chỉ so khớp theo giờ trong TimeOfDay, không theo phút.
    private static bool IsDue(Core.Entities.Dbo.ScheduledReport r, DateTime now)
    {
        if (!TimeSpan.TryParse(r.TimeOfDay, out var timeOfDay)) return false;
        if (now.Hour != timeOfDay.Hours) return false;

        // Đã chạy trong cùng giờ này rồi thì bỏ qua (tránh gửi trùng khi job chạy lại trong cùng khung giờ).
        if (r.LastRunAt.HasValue && r.LastRunAt.Value.Date == now.Date && r.LastRunAt.Value.Hour == now.Hour)
            return false;

        return r.FrequencyType switch
        {
            1 => true, // Daily
            2 => r.DayOfWeek.HasValue && (int)now.DayOfWeek == r.DayOfWeek.Value,
            3 => r.DayOfMonth.HasValue && now.Day == r.DayOfMonth.Value,
            _ => false
        };
    }
}
