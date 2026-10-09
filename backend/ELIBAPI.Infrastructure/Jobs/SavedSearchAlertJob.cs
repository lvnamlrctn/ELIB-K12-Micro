using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>Đợt 9 — quét mọi tìm kiếm đã lưu đang bật cảnh báo, so khớp với kết quả mới nhất từ ES, gắn
/// cờ HasUnread nếu có kết quả mới. Mirror khoảng chạy ~30 phút của ELIB-LRC (ReaderExperienceWorker),
/// nhưng dùng Hangfire thay vì BackgroundService/PeriodicTimer riêng — khớp quy ước lập lịch sẵn có của
/// ELIB (7 job Hangfire khác cùng đăng ký ở Program.cs). Hangfire.Cron không có sẵn khoảng 30 phút nên
/// dùng cron thô "*/30 * * * *".</summary>
public class SavedSearchAlertJob(ReaderWorkspaceService service, ILogger<SavedSearchAlertJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 1200)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        await service.CheckAlertsAsync();
        logger.LogInformation("SavedSearchAlertJob: đã kiểm tra xong lượt này.");
    }
}
