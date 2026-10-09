using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using MassTransit;
using MassTransit.Testing;
using Elib.BuildingBlocks.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.BuildingBlocks.Tests.TenantReplica;

public sealed class ReplicaDb(DbContextOptions<ReplicaDb> options, ITenantContext tenant) : ElibDbContext(options, tenant)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddTenantReplica();
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class RecordingSeeder : ITenantSeeder
{
    public List<(long TenantId, long? ContextTenant)> Calls { get; } = [];
    public bool Fail { get; set; }
    public ITenantContext? Context { get; set; }

    public Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("thiếu dữ liệu mẫu");
        Calls.Add((tenant.TenantId, Context?.TenantId));
        return Task.CompletedTask;
    }
}

public sealed class TenantReplicaTests : IAsyncLifetime
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly SqliteTestDatabase _database = new();
    private readonly RecordingSeeder _seeder = new();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddElibTenancy(new ConfigurationBuilder().Build());
        services.AddElibPersistenceCore();
        services.AddDbContext<ReplicaDb>((sp, o) => o.UseSqlite(_database.ConnectionString).AddInterceptors(sp.GetRequiredService<ElibSaveChangesInterceptor>()));
        services.AddTenantReplica<ReplicaDb>("catalog");
        services.AddScoped<ITenantSeeder>(sp =>
        {
            _seeder.Context = sp.GetRequiredService<ITenantContext>();
            return _seeder;
        });
        services.AddMassTransitTestHarness(x =>
        {
            x.AddTenantReplicaConsumers<ReplicaDb>();
            x.UsingInMemory((context, bus) =>
            {
                bus.UseElibFilters(context);
                bus.ConfigureEndpoints(context);
            });
        });

        _provider = services.BuildServiceProvider(validateScopes: true);
        using (var scope = _provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<ReplicaDb>().Database.EnsureCreatedAsync();
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
        _database.Dispose();
    }

    private async Task<bool> Licensed(long tenantId, string module)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IModuleLicenseSource>().IsLicensedAsync(tenantId, module, CancellationToken.None);
    }

    private async Task<TenantReplicaRecord?> Replica(long tenantId)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReplicaDb>().Set<TenantReplicaRecord>().AsNoTracking()
            .Include(t => t.Modules).FirstOrDefaultAsync(t => t.TenantId == tenantId);
    }

    private async Task PublishAndWait<T>(T message) where T : class
    {
        await _harness.Bus.Publish(message);
        Assert.True(await _harness.Consumed.Any<T>(c => ReferenceEquals(c.Context.Message, message) || c.Context.MessageId == (message as Contracts.Events.IntegrationEvent)?.EventId));
    }

    private static TenantProvisioned Provisioned(long id, params string[] modules) => new()
    {
        TenantId = id,
        Code = "T" + id,
        Name = "Trường " + id,
        Subdomain = "t" + id,
        Modules = modules.Select(m => new ModuleLicense(m, "Active", null, null)).ToList(),
    };

    private static TenantUpdated Updated(long id, TenantStatus status, long version) => new()
    {
        TenantId = id, Code = "T" + id, Name = "Tên mới " + version, Subdomain = "t" + id, TimeZone = "Asia/Ho_Chi_Minh",
        Status = status, SourceVersion = version,
    };

    [Fact]
    public async Task Provisioned_creates_replica_runs_seeder_in_tenant_context_and_reports_success()
    {
        await PublishAndWait(Provisioned(1, "CATALOG"));

        Assert.True(await _harness.Published.Any<TenantSeeded>(p => p.Context.Message.TenantId == 1));
        var seeded = _harness.Published.Select<TenantSeeded>().First(p => p.Context.Message.TenantId == 1).Context.Message;
        Assert.True(seeded.Succeeded);
        Assert.Equal("catalog", seeded.Service);
        Assert.Contains((1L, (long?)1L), _seeder.Calls);

        var replica = await Replica(1);
        Assert.Equal("Provisioning", replica!.Status);
        Assert.Equal("CATALOG", Assert.Single(replica.Modules).ModuleCode);
    }

    [Fact]
    public async Task Seeder_failure_is_reported_not_thrown()
    {
        _seeder.Fail = true;
        await PublishAndWait(Provisioned(2));

        Assert.True(await _harness.Published.Any<TenantSeeded>(p => p.Context.Message.TenantId == 2));
        var seeded = _harness.Published.Select<TenantSeeded>().First(p => p.Context.Message.TenantId == 2).Context.Message;
        Assert.False(seeded.Succeeded);
        Assert.Equal("thiếu dữ liệu mẫu", seeded.Error);
    }

    [Fact]
    public async Task License_requires_active_tenant_and_effective_module()
    {
        await PublishAndWait(Provisioned(3, "CATALOG"));
        Assert.False(await Licensed(3, "CATALOG")); // còn đang khởi tạo

        await PublishAndWait(Updated(3, TenantStatus.Active, 2));
        Assert.True(await Licensed(3, "catalog"));
        Assert.False(await Licensed(3, "AI"));

        await PublishAndWait(new ModuleLicenseChanged
        {
            TenantId = 3,
            SourceVersion = 3,
            Modules = [new("CATALOG", "Active", null, null), new("AI", "Trial", null, Today.AddDays(-1))],
        });
        Assert.True(await Licensed(3, "CATALOG")); // cache đã bị xoá khi bản sao đổi
        Assert.False(await Licensed(3, "AI"));      // trial đã hết hạn

        await PublishAndWait(Updated(3, TenantStatus.Suspended, 4));
        Assert.False(await Licensed(3, "CATALOG"));
    }

    [Fact]
    public async Task Older_versions_are_ignored()
    {
        await PublishAndWait(Updated(4, TenantStatus.Active, 5));
        await PublishAndWait(Updated(4, TenantStatus.Suspended, 3));
        await PublishAndWait(new ModuleLicenseChanged { TenantId = 4, SourceVersion = 7, Modules = [new("SEARCH", "Active", null, null)] });
        await PublishAndWait(new ModuleLicenseChanged { TenantId = 4, SourceVersion = 6, Modules = [] });
        // Provisioned đến trễ không ghi đè dữ liệu đã có version
        await PublishAndWait(Provisioned(4, "PORTAL"));

        var replica = await Replica(4);
        Assert.Equal("Active", replica!.Status);
        Assert.Equal(5, replica.SourceVersion);
        Assert.Equal("SEARCH", Assert.Single(replica.Modules).ModuleCode);
    }

    [Fact]
    public async Task Same_module_set_can_be_replaced_without_key_conflicts()
    {
        await PublishAndWait(Updated(5, TenantStatus.Active, 1));
        await PublishAndWait(new ModuleLicenseChanged { TenantId = 5, SourceVersion = 2, Modules = [new("SEARCH", "Active", null, null), new("AI", "Active", null, null)] });
        await PublishAndWait(new ModuleLicenseChanged { TenantId = 5, SourceVersion = 3, Modules = [new("SEARCH", "Suspended", null, null), new("PORTAL", "Active", null, null)] });

        var replica = await Replica(5);
        Assert.Equal(["PORTAL", "SEARCH"], replica!.Modules.Select(m => m.ModuleCode).Order());
        Assert.Equal("Suspended", replica.Modules.Single(m => m.ModuleCode == "SEARCH").Status);
    }
}
