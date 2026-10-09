using System.Data.Common;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.Persistence;

/// <summary>
/// Chạy migration EF khi khởi động — chỉ cho môi trường dev/docker (bật bằng Database:MigrateOnStartup).
/// Trên Kubernetes migration chạy bằng Job riêng trước khi rollout (docs 06), không để nhiều pod cùng migrate.
/// </summary>
public static partial class DatabaseMigration
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    public static async Task MigrateElibDatabaseAsync<TContext>(this IServiceProvider services, CancellationToken cancellationToken = default)
        where TContext : ElibDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigration));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                // Migration tạo bảng có FORCE RLS — chạy trong ngữ cảnh hệ thống để seed (nếu có) không bị policy chặn.
                using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();
                await db.Database.MigrateAsync(cancellationToken);
                LogMigrated(logger, typeof(TContext).Name);
                return;
            }
            catch (DbException ex) when (attempt < 10)
            {
                // DB khởi động cùng lúc với service — chờ rồi thử lại.
                LogRetry(logger, ex, typeof(TContext).Name, attempt);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã áp dụng migration cho {Context}")]
    private static partial void LogMigrated(ILogger logger, string context);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migration {Context} thất bại (lần {Attempt}), thử lại")]
    private static partial void LogRetry(ILogger logger, Exception exception, string context, int attempt);
}
