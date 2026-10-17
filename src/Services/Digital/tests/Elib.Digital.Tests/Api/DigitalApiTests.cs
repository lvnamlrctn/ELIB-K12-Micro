using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Application;
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

    [Fact]
    public async Task Collections_form_a_tree_and_reject_duplicate_names_under_the_same_parent()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var root = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Văn học"), Json));
        Assert.Equal((1, 0), (root.Level, root.ParentId ?? 0));

        var child = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(
            U("/api/collections/Add"), new EbookCollectionRequest("Văn học Việt Nam", ParentId: root.Id, Language: "vi"), Json));
        Assert.Equal((2, root.Id), (child.Level, child.ParentId));

        Assert.Equal("NAME_EXISTS", (await Error(await staff.PostAsJsonAsync(
            U("/api/collections/Add"), new EbookCollectionRequest("Văn học Việt Nam", ParentId: root.Id), Json))).Code);

        Assert.Equal("CATALOG_HAS_CHILDREN", (await Error(await staff.DeleteAsync(U($"/api/collections/Delete/{root.PublicId}")))).Code);

        // Đơn vị khác không thấy bộ sưu tập của đơn vị này.
        Assert.Equal(0, (await Read<CrudPage<EbookCollectionDto>>(
            await Staff(NewTenant()).PostAsJsonAsync(U("/api/collections/Search"), new TreeCatalogSearch(), Json))).TotalCount);
    }

    [Fact]
    public async Task Moving_a_subject_to_a_new_parent_shifts_the_level_of_its_descendants()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var a = await Read<EbookSubjectDto>(await staff.PostAsJsonAsync(U("/api/subjects/Add"), new EbookSubjectRequest("Khoa học", Ddc: "500"), Json));
        var b = await Read<EbookSubjectDto>(await staff.PostAsJsonAsync(U("/api/subjects/Add"), new EbookSubjectRequest("Công nghệ", Ddc: "600"), Json));
        var child = await Read<EbookSubjectDto>(await staff.PostAsJsonAsync(
            U("/api/subjects/Add"), new EbookSubjectRequest("Vật lý", ParentId: a.Id), Json));
        Assert.Equal(2, child.Level);

        var moved = await Read<EbookSubjectDto>(await staff.PutAsJsonAsync(
            U($"/api/subjects/Update/{a.PublicId}"), new EbookSubjectRequest("Khoa học", ParentId: b.Id), Json));
        Assert.Equal((2, b.Id), (moved.Level, moved.ParentId));

        var grandchild = await Read<EbookSubjectDto>(await staff.GetAsync(U($"/api/subjects/GetById/{child.PublicId}")));
        Assert.Equal(3, grandchild.Level);

        Assert.Equal("CATALOG_MOVE_CYCLE", (await Error(await staff.PutAsJsonAsync(
            U($"/api/subjects/Update/{b.PublicId}"), new EbookSubjectRequest("Công nghệ", ParentId: child.Id), Json))).Code);
    }

    [Fact]
    public async Task Dig_type_codes_are_unique_per_tenant()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        await Read<EbookDigTypeDto>(await staff.PostAsJsonAsync(
            U("/api/dig-types/Add"), new EbookDigTypeRequest("PDF", "Tài liệu PDF", "PDF document"), Json));
        Assert.Equal("CODE_EXISTS", (await Error(await staff.PostAsJsonAsync(U("/api/dig-types/Add"), new EbookDigTypeRequest("PDF"), Json))).Code);
    }

    [Fact]
    public async Task Policies_are_unique_per_reader_type_and_collection_and_resolve_most_specific_first()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        await Read<PolicyDigitalDto>(await staff.PostAsJsonAsync(U("/api/policies/Add"), new PolicyDigitalRequest(MaxDocument: 3), Json));
        await Read<PolicyDigitalDto>(await staff.PostAsJsonAsync(U("/api/policies/Add"), new PolicyDigitalRequest(ReaderTypeId: 1, MaxDocument: 10), Json));
        Assert.Equal("POLICY_EXISTS", (await Error(await staff.PostAsJsonAsync(U("/api/policies/Add"), new PolicyDigitalRequest(ReaderTypeId: 1), Json))).Code);

        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Kỹ thuật"), Json));
        await Read<PolicyDigitalByCollectionDto>(await staff.PostAsJsonAsync(
            U("/api/policies-by-collection/Add"), new PolicyDigitalByCollectionRequest(null, collection.Id, MaxDocument: 2), Json));
        Assert.Equal("COLLECTION_NOT_FOUND", (await Error(await staff.PostAsJsonAsync(
            U("/api/policies-by-collection/Add"), new PolicyDigitalByCollectionRequest(null, collection.Id + 999), Json))).Code);
        Assert.Equal("POLICY_EXISTS", (await Error(await staff.PostAsJsonAsync(
            U("/api/policies-by-collection/Add"), new PolicyDigitalByCollectionRequest(null, collection.Id), Json))).Code);
    }
}
