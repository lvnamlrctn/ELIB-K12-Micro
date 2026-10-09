using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.BuildingBlocks.Crud;

public static class CrudServiceCollectionExtensions
{
    /// <summary>
    /// DbContext của service dùng cho mọi resource CRUD + nhật ký thao tác (AuditRecorded, gắn tên service).
    /// Service phải đăng ký MassTransit (IPublishEndpoint) — AddElibMessaging.
    /// </summary>
    public static IServiceCollection AddCrudDbContext<TContext>(this IServiceCollection services, string serviceName) where TContext : class, ICrudDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        services.AddScoped<ICrudDbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddHttpContextAccessor();
        services.Configure<CrudAuditOptions>(o => o.ServiceName = serviceName);
        services.AddScoped<ICrudAuditSink, PublishingCrudAuditSink>();
        return services;
    }

    public static IServiceCollection AddCrudResource<TResource>(this IServiceCollection services) where TResource : class
        => services.AddScoped(sp =>
        {
            var resource = ActivatorUtilities.CreateInstance<TResource>(sp);
            if (resource is ICrudAuditable auditable) auditable.AuditSink = sp.GetService<ICrudAuditSink>();
            return resource;
        });
}

public static class CrudEndpointExtensions
{
    /// <summary>
    /// Map 8 endpoint chuẩn dưới <paramref name="prefix"/>, ví dụ <c>app.MapCrud&lt;NationalityResource&gt;("/api/nationalities", "NATIONALITIES")</c>.
    /// Trả về group để thêm endpoint riêng của danh mục.
    /// </summary>
    public static RouteGroupBuilder MapCrud<TResource>(this IEndpointRouteBuilder app, string prefix, string permissionModule)
        where TResource : ICrudEndpoints
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionModule);
        var group = app.MapGroup(prefix);
        TResource.MapEndpoints(group, permissionModule);
        return group;
    }
}
