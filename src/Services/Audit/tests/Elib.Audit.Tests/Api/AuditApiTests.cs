using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.Audit.Application;
using Elib.Audit.Infrastructure;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Audit.Tests.Api;

public sealed class AuditApiTests : IClassFixture<AuditApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 3000;
    private static long _nextUserId = 9000;

    private readonly AuditApiFactory _factory;

    public AuditApiTests(AuditApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants.Length == 0 ? ["SYSTEM_LOG:view"] : grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static AuditRecorded Entry(long tenantId, string action, string summary, DateTimeOffset? at = null) => new()
    {
        TenantId = tenantId,
        Actor = new EventActor(7, "staff"),
        ActorName = "Nguyễn An (an)",
        Service = "tenant",
        Action = action,
        EntityType = "Ethnicity",
        EntityId = Guid.NewGuid().ToString(),
        Summary = summary,
        IpAddress = "203.0.113.5",
        OccurredAt = at ?? DateTimeOffset.UtcNow,
    };

    private async Task WaitTenantLogAsync(long tenantId, string summary) =>
        await AuditApiFactory.WaitForAsync(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            return await db.AuditLogs.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Summary == summary);
        }, "nhật ký " + summary);

    [Fact]
    public async Task Tenant_logs_are_stored_once_searchable_and_isolated()
    {
        var a = Interlocked.Increment(ref _nextTenantId);
        var b = Interlocked.Increment(ref _nextTenantId);
        var first = Entry(a, "ADD", "Thêm dân tộc: Kinh");
        await _factory.PublishAsync(first);
        await _factory.PublishAsync(first); // gửi lại cùng EventId — không ghi trùng
        await _factory.PublishAsync(Entry(a, "DELETE", "Xoá dân tộc: Tày"));
        await _factory.PublishAsync(Entry(b, "ADD", "Thêm dân tộc: của B"));
        await WaitTenantLogAsync(a, "Xoá dân tộc: Tày");
        await WaitTenantLogAsync(b, "Thêm dân tộc: của B");
        await WaitTenantLogAsync(a, "Thêm dân tộc: Kinh");

        var all = await Read<CrudPage<AuditLogDto>>(await Staff(a).PostAsJsonAsync("/api/audit-logs/Search", new AuditLogSearch(), Json));
        Assert.Equal(2, all.TotalCount);
        Assert.DoesNotContain(all.Items, l => l.Summary == "Thêm dân tộc: của B");
        Assert.Equal(("Nguyễn An (an)", "203.0.113.5"), (all.Items[0].ActorName, all.Items[0].IpAddress));

        var filtered = await Read<CrudPage<AuditLogDto>>(await Staff(a).PostAsJsonAsync("/api/audit-logs/Search",
            new AuditLogSearch { Action = "DELETE" }, Json));
        Assert.Equal("Xoá dân tộc: Tày", Assert.Single(filtered.Items).Summary);
        var byKeyword = await Read<CrudPage<AuditLogDto>>(await Staff(a).PostAsJsonAsync("/api/audit-logs/Search",
            new AuditLogSearch { Keyword = "KINH" }, Json));
        Assert.Single(byKeyword.Items);

        Assert.Equal(["ADD", "DELETE"], await Read<List<string>>(await Staff(a).GetAsync(new Uri("/api/audit-logs/actions", UriKind.Relative))));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Staff(a, "ORGS:view").PostAsJsonAsync("/api/audit-logs/Search", new AuditLogSearch(), Json)).StatusCode);
    }

    [Fact]
    public async Task Platform_logs_need_system_context_and_show_tenant_names()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        await _factory.PublishAsync(new TenantProvisioned { TenantId = tenantId, Code = "TH" + tenantId, Name = "Trường Hoa Mai", Subdomain = "hm" + tenantId, Modules = [] });
        var summary = $"TH{tenantId}: Tạm ngưng đơn vị";
        await _factory.PublishAsync(new SystemAuditRecorded
        {
            Actor = new EventActor(1, "staff"), ActorName = "Quản trị hệ thống (sysadmin)", Service = "tenant", Action = "TENANT_SUSPEND",
            EntityType = "Tenant", TenantId = tenantId, Summary = summary,
        });
        var sysadmin = _factory.CreateClient().WithClaims(TestClaims.SuperAdmin);
        var page = await AuditApiFactory.WaitForAsync(async () =>
        {
            var p = await Read<CrudPage<PlatformAuditLogDto>>(await sysadmin.PostAsJsonAsync("/api/system/audit-logs/Search",
                new PlatformAuditLogSearch { TenantId = tenantId }, Json));
            return p.Items.Any(i => i.TenantName is not null) ? p : null;
        }, "nhật ký nền tảng có tên đơn vị");
        var row = Assert.Single(page.Items);
        Assert.Equal(("TENANT_SUSPEND", "Trường Hoa Mai", summary), (row.Action, row.TenantName, row.Summary));
        Assert.Contains("TENANT_SUSPEND", await Read<List<string>>(await sysadmin.GetAsync(new Uri("/api/system/audit-logs/actions", UriKind.Relative))));

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Staff(tenantId).PostAsJsonAsync("/api/system/audit-logs/Search", new PlatformAuditLogSearch(), Json)).StatusCode);
    }

    [Fact]
    public async Task Old_entries_are_purged_after_retention()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        await _factory.PublishAsync(Entry(tenantId, "ADD", "Cũ", DateTimeOffset.UtcNow.AddDays(-31)));
        await _factory.PublishAsync(Entry(tenantId, "ADD", "Mới"));
        await WaitTenantLogAsync(tenantId, "Cũ");
        await WaitTenantLogAsync(tenantId, "Mới");

        Assert.True(await _factory.Services.GetRequiredService<AuditRetentionService>().PurgeAsync(CancellationToken.None) >= 1);

        var left = await Read<CrudPage<AuditLogDto>>(await Staff(tenantId).PostAsJsonAsync("/api/audit-logs/Search", new AuditLogSearch(), Json));
        Assert.Equal(["Mới"], left.Items.Select(i => i.Summary));
    }
}
