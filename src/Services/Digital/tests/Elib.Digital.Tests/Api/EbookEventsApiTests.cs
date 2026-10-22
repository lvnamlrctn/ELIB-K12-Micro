using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Digital;
using Elib.Contracts.Events.Platform;
using Elib.Digital.Application;
using Elib.Digital.Domain;

namespace Elib.Digital.Tests.Api;

public sealed class EbookEventsApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private IEnumerable<EbookChanged> Events(long tenantId, Guid publicId) =>
        Factory.PublishedOf<EbookChanged>().Where(e => e.TenantId == tenantId && e.EbookPublicId == publicId).OrderBy(e => e.Version);

    [Fact]
    public async Task Every_change_publishes_the_new_state_with_a_growing_version()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Văn học"), Json));
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"),
            new EbookItemFields("Số đỏ", CollectionId: collection.Id, Author: "Vũ Trọng Phụng", Keyword: "tiểu thuyết", Free: true), Json));

        var created = Assert.Single(Events(tenantId, item.PublicId));
        Assert.Equal(("Số đỏ", "Văn học", "Vũ Trọng Phụng", true, false, false), (created.Title, created.CollectionName, created.Author, created.Free, created.HasFile, created.Deleted));
        Assert.Equal(item.Id, created.EbookId);

        await staff.PutAsJsonAsync(U("/api/items/ChangeStatus"), new ChangeStatusRequest(item.PublicId, IHasStatus.Inactive), Json);
        await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Guid.CreateVersion7(), FileExt: "pdf"), Json);

        var schema = await Read<MetadataSchemaDto>(await staff.PostAsJsonAsync(U("/api/metadata-schemas/Add"), new MetadataSchemaRequest("dc", "dc", null, null), Json));
        var field = await Read<MetadataFieldDto>(await staff.PostAsJsonAsync(U("/api/metadata-fields/Add"), new MetadataFieldRequest(schema.PublicId, "subject", null, "Chủ đề", null), Json));
        await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"), new EbookMetadataSave([new EbookMetadataEntry(field.PublicId, "Châm biếm")]), Json);

        var all = Events(tenantId, item.PublicId).ToList();
        Assert.Equal(4, all.Count);
        Assert.True(all.Zip(all.Skip(1), (a, b) => b.Version > a.Version).All(x => x), "phiên bản phải tăng dần");
        Assert.Equal(IHasStatus.Inactive, all[1].Status);
        Assert.True(all[2].HasFile);
        Assert.Equal(["Châm biếm"], all[3].Metadata);
        Assert.True(all[3].HasFile);

        await staff.DeleteAsync(U($"/api/files/Delete/{(await Read<CrudPage<EbookFileDto>>(await staff.PostAsJsonAsync(U("/api/files/Search"), new EbookFileSearch(), Json))).Items.Single().PublicId}"));
        Assert.False(Events(tenantId, item.PublicId).Last().HasFile);

        // Xoá phải sau khi gỡ file; sự kiện xoá có phiên bản lớn hơn mọi phiên bản trước.
        var before = Events(tenantId, item.PublicId).Max(e => e.Version);
        var deleted = await staff.DeleteAsync(U($"/api/items/Delete/{item.PublicId}"));
        Assert.True(deleted.IsSuccessStatusCode, await deleted.Content.ReadAsStringAsync());
        var last = Events(tenantId, item.PublicId).Last();
        Assert.True(last.Deleted);
        Assert.True(last.Version > before);
    }

    [Fact]
    public async Task Internal_listing_pages_every_item_in_id_order_for_the_service_only()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        for (var i = 1; i <= 3; i++) await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields($"Tài liệu {i}", Status: i == 2 ? IHasStatus.Inactive : IHasStatus.Active), Json);

        var service = Factory.CreateClient().WithClaims(TestClaims.Service);
        var first = await Read<StatePage<EbookChanged>>(await service.GetAsync(U($"/internal/tenants/{tenantId}/ebooks?take=2")));
        Assert.Equal(["Tài liệu 1", "Tài liệu 2"], first.Items.Select(e => e.Title));
        Assert.NotNull(first.Next);
        var second = await Read<StatePage<EbookChanged>>(await service.GetAsync(U($"/internal/tenants/{tenantId}/ebooks?take=2&after={first.Next}")));
        Assert.Equal("Tài liệu 3", Assert.Single(second.Items).Title);
        Assert.Null(second.Next);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U($"/internal/tenants/{tenantId}/ebooks"))).StatusCode);
        Assert.Empty((await Read<StatePage<EbookChanged>>(await service.GetAsync(U($"/internal/tenants/{NewTenant()}/ebooks")))).Items);
    }
}
