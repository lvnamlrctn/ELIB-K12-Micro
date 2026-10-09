using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.BuildingBlocks.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký DbContext PostgreSQL chuẩn của service: snake_case, interceptor tenant/audit/soft-delete và RLS.
    /// Yêu cầu đã gọi <c>AddElibTenancy</c>. Migration nằm trong assembly của <typeparamref name="TContext"/>.
    /// </summary>
    public static IServiceCollection AddElibPostgres<TContext>(
        this IServiceCollection services, string connectionString, Action<DbContextOptionsBuilder>? configure = null)
        where TContext : ElibDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddElibPersistenceCore();
        services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name)
                          .EnableRetryOnFailure(maxRetryCount: 3))
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(
                       sp.GetRequiredService<ElibSaveChangesInterceptor>(),
                       sp.GetRequiredService<TenantRlsConnectionInterceptor>());
            configure?.Invoke(options);
        });
        return services;
    }

    /// <summary>Đăng ký các interceptor dùng chung (cũng dùng cho provider khác trong test).</summary>
    public static IServiceCollection AddElibPersistenceCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ElibSaveChangesInterceptor>();
        services.TryAddScoped<TenantRlsConnectionInterceptor>();
        return services;
    }
}
