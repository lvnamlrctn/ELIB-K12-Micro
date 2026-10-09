using Elib.BuildingBlocks.Storage;
using Elib.BuildingBlocks.Tenancy;
using Elib.Media.Application;
using Elib.Media.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Media.Infrastructure;

/// <summary>Tạo bucket lúc khởi động; MinIO chưa sẵn sàng thì thử lại mỗi 10 giây (service vẫn nhận request trong lúc đó).</summary>
public sealed partial class BucketInitializer(IObjectStorage storage, IOptions<MediaOptions> options, TimeProvider clock, ILogger<BucketInitializer> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10), clock);
        do
        {
            try
            {
                await storage.EnsureBucketAsync(options.Value.PublicBucket, publicRead: true, stoppingToken);
                await storage.EnsureBucketAsync(options.Value.PrivateBucket, publicRead: false, stoppingToken);
                LogReady(logger, options.Value.PublicBucket, options.Value.PrivateBucket);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRetry(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket {PublicBucket}, {PrivateBucket} đã sẵn sàng")]
    private static partial void LogReady(ILogger logger, string publicBucket, string privateBucket);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chưa tạo được bucket trên MinIO, sẽ thử lại")]
    private static partial void LogRetry(ILogger logger, Exception exception);
}

/// <summary>Dọn upload bỏ dở/bị từ chối quá hạn: xoá object (nếu còn) rồi xoá bản ghi. Chạy mỗi giờ, ngữ cảnh hệ thống.</summary>
public sealed partial class PendingUploadCleanup(IServiceScopeFactory scopes, IObjectStorage storage, IOptions<MediaOptions> options, TimeProvider clock,
    ILogger<PendingUploadCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
    }

    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().AddHours(-options.Value.PendingRetentionHours);
        using var scope = scopes.CreateScope();
        using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var stale = await db.Files.IgnoreQueryFilters()
            .Where(f => f.Status != MediaFileStatus.Ready && f.CreatedAt < cutoff)
            .OrderBy(f => f.Id).Take(500).ToListAsync(ct);
        foreach (var file in stale)
            await storage.DeleteAsync(file.Bucket, file.ObjectKey, ct);
        db.Files.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
        if (stale.Count > 0) LogCleaned(logger, stale.Count);
        return stale.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã dọn {Count} upload bỏ dở")]
    private static partial void LogCleaned(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dọn upload bỏ dở thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
