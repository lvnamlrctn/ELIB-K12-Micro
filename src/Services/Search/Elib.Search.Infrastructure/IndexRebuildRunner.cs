using Elib.BuildingBlocks.Tenancy;
using Elib.Search.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elib.Search.Infrastructure;

/// <summary>Chạy "Dựng lại chỉ mục" ở nền (scope riêng, ngữ cảnh đơn vị) — request trả ngay, màn quản trị xem trạng thái.</summary>
public sealed partial class IndexRebuildRunner(IServiceScopeFactory scopes, ILogger<IndexRebuildRunner> logger)
{
    public Task Start(long tenantId) => Task.Run(async () =>
    {
        using var scope = scopes.CreateScope();
        using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        try
        {
            var status = await scope.ServiceProvider.GetRequiredService<IndexRebuilder>().RebuildAsync(CancellationToken.None);
            LogDone(logger, tenantId, status.Bibs, status.Items, status.Loans);
        }
        catch (Exception ex)
        {
            LogFailed(logger, ex, tenantId);
        }
    }, CancellationToken.None);

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: dựng lại chỉ mục xong ({Bibs} biểu ghi, {Items} bản sách, {Loans} lượt đang mượn)")]
    private static partial void LogDone(ILogger logger, long tenantId, int bibs, int items, int loans);

    [LoggerMessage(Level = LogLevel.Error, Message = "Đơn vị {TenantId}: dựng lại chỉ mục thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception, long tenantId);
}
