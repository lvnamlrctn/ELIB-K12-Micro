using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Domain;
using Elib.Digital.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Tests.Api;

public sealed class DigitalApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    [Fact]
    public async Task Healthz_is_reachable()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync(U("/healthz"));
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ReaderChanged_is_applied_to_the_local_patron_replica()
    {
        var tenantId = NewTenant();
        var reader = Reader(tenantId, "DG-001", typeId: 1);
        await Publish(reader);

        using var scope = Factory.Services.CreateScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<DigitalDbContext>();
        var replica = await db.Set<PatronReplica>().FirstOrDefaultAsync(x => x.ReaderPublicId == reader.ReaderPublicId);

        Assert.NotNull(replica);
        Assert.Equal("DG-001", replica!.CardNo);
        Assert.Equal(reader.FullName, replica.FullName);
        Assert.Equal(1, replica.ReaderTypeId);
    }
}
