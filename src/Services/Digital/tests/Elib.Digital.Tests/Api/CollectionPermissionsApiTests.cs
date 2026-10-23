using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Digital.Application;

namespace Elib.Digital.Tests.Api;

/// <summary>Hộp "Phân quyền" của cây bộ sưu tập: quyền đọc/tải theo loại bạn đọc, đặt lại cả tập trong một lần lưu.</summary>
public sealed class CollectionPermissionsApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static async Task<List<PolicyDigitalByCollectionDto>> Policies(HttpClient staff, long collectionId) =>
        (await Read<CrudPage<PolicyDigitalByCollectionDto>>(await staff.PostAsJsonAsync(U("/api/policies-by-collection/Search"), new CrudSearch { PageSize = 100 }, Json)))
        .Items.Where(p => p.CollectionId == collectionId).OrderBy(p => p.ReaderTypeId).ToList();

    [Fact]
    public async Task Saving_replaces_the_per_type_rows_keeps_hidden_fields_and_the_all_types_row()
    {
        var staff = Staff(NewTenant());
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Luận văn"), Json));
        var url = $"/api/collections/{collection.PublicId}/permissions";
        // Có sẵn: dòng "mọi loại", loại 1 (có giới hạn trang, bình luận) và loại 3 (sẽ bị bỏ khỏi danh sách).
        await staff.PostAsJsonAsync(U("/api/policies-by-collection/Add"), new PolicyDigitalByCollectionRequest(null, collection.Id, MaxPage: 5), Json);
        await staff.PostAsJsonAsync(U("/api/policies-by-collection/Add"), new PolicyDigitalByCollectionRequest(1, collection.Id, MaxPage: 10, Comment: true), Json);
        await staff.PostAsJsonAsync(U("/api/policies-by-collection/Add"), new PolicyDigitalByCollectionRequest(3, collection.Id), Json);

        var saved = await Read<List<CollectionPermissionRow>>(await staff.PutAsJsonAsync(U(url), new[]
        {
            new CollectionPermissionRow(1, Read: false, Download: true, MaxDocument: 2, OfflineDays: 7), // tải ⇒ tự bật đọc
            new CollectionPermissionRow(2, Read: true, Download: false, ReservationExpireHours: 24),
        }, Json));

        Assert.Equal([1L, 2L], saved.Select(r => r.ReaderTypeId));
        Assert.Equal((true, true, 2, 7), (saved[0].Read, saved[0].Download, saved[0].MaxDocument, saved[0].OfflineDays));
        Assert.Equal(24, saved[1].ReservationExpireHours);
        Assert.Equal(saved, await Read<List<CollectionPermissionRow>>(await staff.GetAsync(U(url))));

        var policies = await Policies(staff, collection.Id);
        Assert.Equal([null, 1L, 2L], policies.Select(p => p.ReaderTypeId)); // loại 3 đã xoá, dòng "mọi loại" còn
        Assert.Equal(5, policies[0].MaxPage);
        Assert.Equal((10, true), (policies[1].MaxPage, policies[1].Comment)); // trường hộp không hiện được giữ nguyên

        // Đặt lại rỗng: xoá mọi dòng theo loại, vẫn giữ dòng "mọi loại".
        Assert.Empty(await Read<List<CollectionPermissionRow>>(await staff.PutAsJsonAsync(U(url), Array.Empty<CollectionPermissionRow>(), Json)));
        Assert.Equal([null], (await Policies(staff, collection.Id)).Select(p => p.ReaderTypeId));
    }

    [Fact]
    public async Task Duplicate_types_unknown_collections_and_missing_permissions_are_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("X"), Json));
        var url = $"/api/collections/{collection.PublicId}/permissions";

        Assert.Equal("PERMISSION_DUPLICATE", (await Error(await staff.PutAsJsonAsync(U(url),
            new[] { new CollectionPermissionRow(1, true, false), new CollectionPermissionRow(1, true, true) }, Json))).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(U($"/api/collections/{Guid.NewGuid()}/permissions"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "DIGITAL_COLLECTIONS:view").GetAsync(U(url))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_COLLECTIONS:view").PutAsJsonAsync(U(url), Array.Empty<CollectionPermissionRow>(), Json)).StatusCode);
        // Đơn vị khác không thấy bộ sưu tập này.
        Assert.Equal(HttpStatusCode.NotFound, (await Staff(NewTenant()).GetAsync(U(url))).StatusCode);
    }
}
