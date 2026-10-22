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
    public async Task Entity_history_is_visible_to_readers_view_with_changes_and_only_for_that_record()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var readerId = Guid.NewGuid();
        Task Publish(long tenant, string type, Guid id, string summary, string? changes) => _factory.PublishAsync(Entry(tenant, "UPDATE", summary) with
        {
            Service = "patron", EntityType = type, EntityId = id.ToString(), ChangesJson = changes,
        });
        await Publish(tenantId, "Reader", readerId, "Sửa bạn đọc: HS01", """{"classId":[1,2],"_reason":"Lên lớp"}""");
        await Publish(tenantId, "Reader", Guid.NewGuid(), "Sửa bạn đọc: HS02", null);
        await Publish(tenantId + 1, "Reader", readerId, "Sửa bạn đọc: của đơn vị khác", null);
        await Publish(tenantId, "Ethnicity", readerId, "Sửa dân tộc", null);
        await WaitTenantLogAsync(tenantId, "Sửa bạn đọc: HS01");
        await WaitTenantLogAsync(tenantId, "Sửa dân tộc");
        await WaitTenantLogAsync(tenantId + 1, "Sửa bạn đọc: của đơn vị khác");

        var url = new Uri($"/api/audit-logs/entity/Reader/{readerId}", UriKind.Relative);
        var rows = await Read<List<EntityHistoryDto>>(await Staff(tenantId, "READERS:view").GetAsync(url));
        var row = Assert.Single(rows);
        Assert.Equal(("Sửa bạn đọc: HS01", "patron"), (row.Summary, row.Service));
        Assert.Equal("Lên lớp", JsonDocument.Parse(row.Changes!).RootElement.GetProperty("_reason").GetString());
        Assert.Single(await Read<List<EntityHistoryDto>>(await Staff(tenantId).GetAsync(url))); // SYSTEM_LOG:view cũng xem được

        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "ORGS:view").GetAsync(url)).StatusCode);
        // Loại bản ghi chưa khai báo màn lịch sử → không có route.
        Assert.Equal(HttpStatusCode.NotFound, (await Staff(tenantId).GetAsync(new Uri($"/api/audit-logs/entity/Ethnicity/{readerId}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Catalog_log_and_bib_history_are_visible_to_catalogers()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var bibId = Guid.NewGuid();
        await _factory.PublishAsync(Entry(tenantId, "UPDATE", "Sửa biểu ghi: MFN 7") with
        {
            Service = "catalog", EntityType = "Bib", EntityId = bibId.ToString(), ChangesJson = """{"264":["a","b"]}""",
        });
        await _factory.PublishAsync(Entry(tenantId, "UPDATE", "Sửa bạn đọc: HS09") with { Service = "patron", EntityType = "Reader", EntityId = Guid.NewGuid().ToString() });
        await WaitTenantLogAsync(tenantId, "Sửa biểu ghi: MFN 7");
        await WaitTenantLogAsync(tenantId, "Sửa bạn đọc: HS09");

        var cataloger = Staff(tenantId, "CATALOG_BIBS:view");
        var log = await Read<CrudPage<AuditLogDto>>(await cataloger.PostAsJsonAsync("/api/audit-logs/catalog/Search", new AuditLogSearch { Service = "patron" }, Json));
        Assert.Equal(["Sửa biểu ghi: MFN 7"], log.Items.Select(i => i.Summary)); // luôn chỉ catalog/Bib
        Assert.Single(await Read<List<EntityHistoryDto>>(await cataloger.GetAsync(new Uri($"/api/audit-logs/entity/Bib/{bibId}", UriKind.Relative))));
        Assert.Equal(HttpStatusCode.Forbidden, (await cataloger.PostAsJsonAsync("/api/audit-logs/Search", new AuditLogSearch(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "READERS:view").PostAsJsonAsync("/api/audit-logs/catalog/Search", new AuditLogSearch(), Json)).StatusCode);
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

    [Fact]
    public async Task Fine_ticket_history_is_kept_far_longer_than_ordinary_entries_and_visible_to_fine_staff()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var ticketId = Guid.NewGuid();
        await _factory.PublishAsync(Entry(tenantId, "UPDATE", "Phiếu cũ 200 ngày", DateTimeOffset.UtcNow.AddDays(-200)) with { EntityType = "FineTicket", EntityId = ticketId.ToString() });
        await _factory.PublishAsync(Entry(tenantId, "UPDATE", "Phiếu quá 10 năm", DateTimeOffset.UtcNow.AddDays(-4000)) with { EntityType = "FineTicket", EntityId = ticketId.ToString() });
        await _factory.PublishAsync(Entry(tenantId, "ADD", "Thường cũ 200 ngày", DateTimeOffset.UtcNow.AddDays(-200)));
        await WaitTenantLogAsync(tenantId, "Phiếu cũ 200 ngày");
        await WaitTenantLogAsync(tenantId, "Phiếu quá 10 năm");
        await WaitTenantLogAsync(tenantId, "Thường cũ 200 ngày");

        await _factory.Services.GetRequiredService<AuditRetentionService>().PurgeAsync(CancellationToken.None);

        var history = await Read<List<EntityHistoryDto>>(await Staff(tenantId, "FINES:view").GetAsync(new Uri($"/api/audit-logs/entity/FineTicket/{ticketId}", UriKind.Relative)));
        Assert.Equal(["Phiếu cũ 200 ngày"], history.Select(h => h.Summary));
        var all = await Read<CrudPage<AuditLogDto>>(await Staff(tenantId).PostAsJsonAsync("/api/audit-logs/Search", new AuditLogSearch(), Json));
        Assert.DoesNotContain(all.Items, i => i.Summary == "Thường cũ 200 ngày");
    }
}
