using Elib.BuildingBlocks.Authorization;
using Elib.Contracts.Events.Platform;
using MassTransit;

namespace Elib.BuildingBlocks.Messaging;

/// <summary>Xoá cache quyền khi identity báo quyền thay đổi (docs 03 §3.2). Mọi service có [Permission] nên đăng ký.</summary>
public sealed class PermissionCacheInvalidationConsumer(IPermissionChecker checker)
    : IConsumer<PermissionChanged>, IConsumer<SystemPermissionChanged>
{
    public async Task Consume(ConsumeContext<PermissionChanged> context)
    {
        var m = context.Message;
        if (m.AllUsersOfTenant) await checker.InvalidateTenantAsync(m.TenantId, context.CancellationToken);
        foreach (var userId in m.UserIds) await checker.InvalidateUserAsync(userId, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<SystemPermissionChanged> context)
    {
        foreach (var userId in context.Message.UserIds) await checker.InvalidateUserAsync(userId, context.CancellationToken);
    }
}

public static class PermissionCacheInvalidationExtensions
{
    public static IBusRegistrationConfigurator AddPermissionCacheInvalidation(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<PermissionCacheInvalidationConsumer>();
        return bus;
    }
}
