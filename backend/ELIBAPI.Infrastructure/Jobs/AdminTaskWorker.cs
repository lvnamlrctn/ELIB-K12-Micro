using ELIBAPI.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>Worker xử lý tác vụ nền (Đợt 10 — port từ ELIB-LRC AdminTaskWorker). KHÔNG qua Hangfire — là
/// <see cref="BackgroundService"/> riêng, poll liên tục 3 giây/lần (1 chunk/lượt), khác 9 job định kỳ
/// theo lịch cron hiện có của ELIB. Vòng nhịp tim chạy song song, độc lập, để không bị coi là "chết" khi
/// đang xử lý 1 chunk chậm. Tắt hoàn toàn khi <c>AdminTasks:Enabled=false</c> (mặc định).</summary>
public sealed class AdminTaskWorker(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<AdminTaskWorker> logger) : BackgroundService
{
    private readonly string _workerId = Guid.NewGuid().ToString();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("AdminTasks:Enabled")) return;
        await Task.WhenAll(Work(stoppingToken), Heartbeat(stoppingToken));
    }

    private async Task Heartbeat(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AdminTaskHealth>().Beat(_workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "AdminTaskWorker: lỗi ghi nhịp tim."); }

            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task Work(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var health = scope.ServiceProvider.GetRequiredService<AdminTaskHealth>();
                if (await health.CanProcess(_workerId, stoppingToken))
                    await scope.ServiceProvider.GetRequiredService<AdminTaskService>().RunNext(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "AdminTaskWorker: lỗi xử lý tác vụ."); }

            try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
