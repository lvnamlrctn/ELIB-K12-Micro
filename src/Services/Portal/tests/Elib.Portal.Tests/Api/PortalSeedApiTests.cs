using Elib.BuildingBlocks.Crud;
using Elib.Contracts.Events.Platform;
using Elib.Portal.Application;
using System.Net.Http.Json;

namespace Elib.Portal.Tests.Api;

public sealed class PortalSeedApiTests(PortalApiFactory factory) : PortalTestBase(factory)
{
    [Fact]
    public async Task A_new_tenant_gets_a_starter_site_exactly_once_and_reports_seeded()
    {
        var tenantId = NewTenant();
        var provisioned = new TenantProvisioned { TenantId = tenantId, Code = "T" + tenantId, Name = "Trường thử", Subdomain = "t" + tenantId, Modules = [] };
        await Factory.PublishAsync(provisioned);
        TenantSeeded? seeded = null;
        for (var i = 0; i < 50 && seeded is null; i++)
        {
            seeded = Factory.PublishedOf<TenantSeeded>().FirstOrDefault(e => e.TenantId == tenantId);
            if (seeded is null) await Task.Delay(100);
        }
        Assert.NotNull(seeded);
        Assert.Equal(("portal", true), (seeded.Service, seeded.Succeeded));

        var opac = Opac(tenantId);
        var menus = await Read<List<OpacMenu>>(await opac.GetAsync(U("/api/opac/menus")));
        Assert.Equal(["footer", "main"], menus.Select(m => m.Code).Order());
        Assert.Equal(["Trang chủ", "Tin tức", "Liên hệ"], menus.Single(m => m.Code == "main").Items.Select(i => i.Name));
        Assert.Equal(["/", "/tin-tuc", "/lien-he"], menus.Single(m => m.Code == "main").Items.Select(i => i.Href));
        Assert.Single(await Read<List<OpacContactGroup>>(await opac.GetAsync(U("/api/opac/contact-groups"))));
        Assert.Single(await Read<List<OpacCategory>>(await opac.GetAsync(U("/api/opac/categories"))));

        // Seed lại (event phát lại) không nhân đôi dữ liệu.
        await Factory.PublishAsync(provisioned with { EventId = Guid.CreateVersion7() });
        await Task.Delay(500);
        Assert.Equal(2, (await Read<CrudPage<MenuTypeDto>>(await Staff(tenantId).PostAsJsonAsync(U("/api/menu-types/Search"), new CrudSearch(), Json))).TotalCount);
    }
}
