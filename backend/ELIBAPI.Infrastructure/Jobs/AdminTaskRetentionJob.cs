using ELIBAPI.Infrastructure.Services;
using Hangfire;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>Lịch quét dọn Payload/Result của AdminTask quá hạn lưu (Đợt 13). Tôn trọng cờ
/// AdminTasks:Retention:Enabled/DryRun bên trong AdminTaskRetentionService — job này luôn đăng ký, không
/// điều kiện, giống 9 recurring job khác.</summary>
public class AdminTaskRetentionJob(AdminTaskRetentionService service)
{
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync() => service.RunSweepAsync(dryRunOverride: null, CancellationToken.None);
}
