using Elib.BuildingBlocks.Tenancy;
using Elib.Search.Domain;
using Elib.Search.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Search.Tests.Api;

/// <summary>Dọn lượt tìm cũ (monolith: SearchObservationPurgeJob) — chỉ xoá quá hạn lưu, không đụng đơn vị khác.</summary>
public sealed class SearchQueryPurgeTests(SearchApiFactory factory) : IClassFixture<SearchApiFactory>, IAsyncLifetime
{
    private static long _nextTenantId = 28000;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Purge_removes_only_queries_older_than_the_retention_window()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        using (var scope = factory.Services.CreateScope())
        {
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
            db.Set<SearchQuery>().AddRange(
                new SearchQuery { QueryId = Guid.NewGuid(), At = DateTimeOffset.UtcNow.AddDays(-31), Text = "cũ", TextFold = "cu", Total = 1 },
                new SearchQuery { QueryId = Guid.NewGuid(), At = DateTimeOffset.UtcNow, Text = "mới", TextFold = "moi", Total = 1 });
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await factory.Services.GetRequiredService<SearchQueryPurgeJob>().PurgeAsync(CancellationToken.None));

        using var verify = factory.Services.CreateScope();
        using var system = verify.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
        var left = await verify.ServiceProvider.GetRequiredService<SearchDbContext>().Set<SearchQuery>().IgnoreQueryFilters()
            .Where(q => q.TenantId == tenantId).Select(q => q.Text).ToListAsync();
        Assert.Equal(["mới"], left);
    }
}
