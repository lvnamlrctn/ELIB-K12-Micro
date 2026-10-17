using Elib.BuildingBlocks.Tenancy;
using Elib.Search.Application;
using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Search.Infrastructure;

public sealed class SearchQueryRetentionOptions
{
    public const string SectionName = "SearchQueryRetention";

    /// <summary>Lượt tìm (SearchQuery) cũ hơn số ngày này bị xoá, chạy mỗi ngày. 0 = giữ mãi.</summary>
    public int RetentionDays { get; set; } = 90;
}

/// <summary>
/// Dọn lượt tìm OPAC cũ dùng cho báo cáo chất lượng tìm kiếm (<see cref="SearchStats"/>) — monolith: SearchObservationPurgeJob
/// (Hangfire, cùng hạn mặc định 90 ngày). Ngữ cảnh hệ thống, xoá cho mọi đơn vị một lượt như AuditRetentionService.
/// </summary>
public sealed partial class SearchQueryPurgeJob(IServiceScopeFactory scopes, IOptions<SearchQueryRetentionOptions> options, TimeProvider clock,
    ILogger<SearchQueryPurgeJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), clock);
        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        if (options.Value.RetentionDays <= 0) return 0;
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        using var scope = scopes.CreateScope();
        using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
        var db = scope.ServiceProvider.GetRequiredService<ISearchDb>();
        var removed = await db.Set<SearchQuery>().IgnoreQueryFilters().Where(q => q.At < cutoff).ExecuteDeleteAsync(ct);
        if (removed > 0) LogPurged(logger, removed, options.Value.RetentionDays);
        return removed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã xoá {Count} lượt tìm quá {RetentionDays} ngày")]
    private static partial void LogPurged(ILogger logger, int count, int retentionDays);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dọn lượt tìm cũ thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
