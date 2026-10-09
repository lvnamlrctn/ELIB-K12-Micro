using System.Collections.Concurrent;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.Tests.Messaging;

public sealed class Observed
{
    public ConcurrentBag<(string Consumer, long? TenantId, bool IsSystem, long? ActorId)> Calls { get; } = [];
}

public sealed class SuspendedProbe(ITenantContext tenant, ICurrentActor actor, Observed observed) : IConsumer<TenantSuspended>
{
    public Task Consume(ConsumeContext<TenantSuspended> context)
    {
        observed.Calls.Add((nameof(SuspendedProbe), tenant.TenantId, tenant.IsSystem, actor.Id));
        return Task.CompletedTask;
    }
}

public sealed class SystemProbe(ITenantContext tenant, Observed observed) : IConsumer<SystemPermissionChanged>
{
    public Task Consume(ConsumeContext<SystemPermissionChanged> context)
    {
        observed.Calls.Add((nameof(SystemProbe), tenant.TenantId, tenant.IsSystem, null));
        return Task.CompletedTask;
    }
}

/// <summary>Consumer thuộc module AI — chỉ chạy cho đơn vị có license.</summary>
public sealed class AiModuleProbe(IModuleLicenseSource licenses, ILogger<AiModuleProbe> logger, Observed observed)
    : ElibConsumer<PermissionChanged>(licenses, logger)
{
    protected override string? RequiredModule => "AI";

    protected override Task HandleAsync(PermissionChanged message, ConsumeContext<PermissionChanged> context)
    {
        observed.Calls.Add((nameof(AiModuleProbe), message.TenantId, false, null));
        return Task.CompletedTask;
    }
}

public sealed class MessagingTests : IAsyncLifetime
{
    private readonly FakeLicenseSource _licenses = new();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private Observed Observed => _provider.GetRequiredService<Observed>();

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddElibTenancy(new ConfigurationBuilder().Build());
        services.AddSingleton<IModuleLicenseSource>(_licenses);
        services.AddSingleton<Observed>();
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<SuspendedProbe>();
            x.AddConsumer<SystemProbe>();
            x.AddConsumer<AiModuleProbe>();
            x.UsingInMemory((context, bus) =>
            {
                bus.UseElibFilters(context);
                bus.ConfigureEndpoints(context);
            });
        });

        _provider = services.BuildServiceProvider(validateScopes: true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_runs_in_event_tenant_and_actor()
    {
        await _harness.Bus.Publish(new TenantSuspended { TenantId = 5, Actor = new EventActor(9, "staff") });

        Assert.True(await _harness.GetConsumerHarness<SuspendedProbe>().Consumed.Any<TenantSuspended>());
        var call = Assert.Single(Observed.Calls, c => c.Consumer == nameof(SuspendedProbe));
        Assert.Equal(5, call.TenantId);
        Assert.False(call.IsSystem);
        Assert.Equal(9, call.ActorId);
    }

    [Fact]
    public async Task Message_id_equals_event_id()
    {
        var evt = new TenantSuspended { TenantId = 5 };
        await _harness.Bus.Publish(evt);

        Assert.True(await _harness.Published.Any<TenantSuspended>());
        var published = _harness.Published.Select<TenantSuspended>().First();
        Assert.Equal(evt.EventId, published.Context.MessageId);
    }

    [Fact]
    public async Task System_event_runs_in_system_context()
    {
        await _harness.Bus.Publish(new SystemPermissionChanged { UserIds = [1] });

        Assert.True(await _harness.GetConsumerHarness<SystemProbe>().Consumed.Any<SystemPermissionChanged>());
        var call = Assert.Single(Observed.Calls, c => c.Consumer == nameof(SystemProbe));
        Assert.True(call.IsSystem);
        Assert.Null(call.TenantId);
    }

    [Fact]
    public async Task Module_consumer_skips_unlicensed_tenant_and_handles_licensed()
    {
        _licenses.Licensed.Add((2, "AI"));
        await _harness.Bus.Publish(new PermissionChanged { TenantId = 1, AllUsersOfTenant = true });
        await _harness.Bus.Publish(new PermissionChanged { TenantId = 2, AllUsersOfTenant = true });

        var consumer = _harness.GetConsumerHarness<AiModuleProbe>();
        Assert.True(await consumer.Consumed.Any<PermissionChanged>(m => m.Context.Message.TenantId == 1));
        Assert.True(await consumer.Consumed.Any<PermissionChanged>(m => m.Context.Message.TenantId == 2));

        var handled = Observed.Calls.Where(c => c.Consumer == nameof(AiModuleProbe)).Select(c => c.TenantId).ToArray();
        Assert.Equal([2L], handled);
    }
}
