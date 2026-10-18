using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
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

    private static string CoverUrl(long tenantId, string purpose = "ebook-cover") =>
        $"/s3/media-public/{tenantId}/{purpose}/2026/10/{Guid.NewGuid():N}.jpg";

    [Fact]
    public async Task Collection_cover_url_must_belong_to_the_own_tenant_and_purpose()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var withCover = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(
            U("/api/collections/Add"), new EbookCollectionRequest("Có bìa", CoverUrl: CoverUrl(tenantId)), Json));
        Assert.NotNull(withCover.CoverUrl);

        Assert.Equal("COVER_URL_INVALID", (await Error(await staff.PostAsJsonAsync(
            U("/api/collections/Add"), new EbookCollectionRequest("Bìa đơn vị khác", CoverUrl: CoverUrl(tenantId + 1)), Json))).Code);
        Assert.Equal("COVER_URL_INVALID", (await Error(await staff.PostAsJsonAsync(
            U("/api/collections/Add"), new EbookCollectionRequest("Bìa sai mục đích", CoverUrl: CoverUrl(tenantId, "bib-cover")), Json))).Code);
    }

    [Fact]
    public async Task Ebook_items_validate_foreign_keys_and_block_delete_while_files_exist()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Văn học"), Json));

        Assert.Equal("COLLECTION_NOT_FOUND", (await Error(await staff.PostAsJsonAsync(
            U("/api/items/Add"), new EbookItemFields("Tắt đèn", CollectionId: collection.Id + 999), Json))).Code);

        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"),
            new EbookItemFields("Tắt đèn", CollectionId: collection.Id, Author: "Ngô Tất Tố", Free: true, AllowDownload: true, CoverUrl: CoverUrl(tenantId)),
            Json));
        Assert.Equal(("Tắt đèn", collection.Id, collection.Name, true, IHasStatus.Active), (item.Title, item.CollectionId, item.CollectionName, item.Free, item.Status));

        var file = await Read<EbookFileDto>(await staff.PostAsJsonAsync(U("/api/files/Add"),
            new EbookFileRequest(item.PublicId, Guid.CreateVersion7(), FileExt: "pdf", FileType: "application/pdf", FileSize: 1048576), Json));
        Assert.Equal(item.PublicId, file.EbookItemPublicId);

        Assert.Equal("EBOOK_ITEM_HAS_FILES", (await Error(await staff.DeleteAsync(U($"/api/items/Delete/{item.PublicId}")))).Code);

        Assert.Equal(204, (int)(await staff.DeleteAsync(U($"/api/files/Delete/{file.PublicId}"))).StatusCode);
        Assert.Equal(204, (int)(await staff.DeleteAsync(U($"/api/items/Delete/{item.PublicId}"))).StatusCode);
    }

    [Fact]
    public async Task Bib_options_proxies_to_catalog_for_the_print_book_link_picker()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var option = new BibLinkOption(Guid.CreateVersion7(), "Số đỏ", "Vũ Trọng Phụng", "NXB Văn học", "1936");
        Factory.CatalogBibs.Results.Add(option);

        var found = await Read<List<BibLinkOption>>(await staff.GetAsync(U("/api/items/bib-options?q=so+do")));
        Assert.Equal(option, Assert.Single(found));

        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(
            U("/api/items/Add"), new EbookItemFields("Số đỏ (số hoá)", LinkedBibPublicId: option.BibPublicId), Json));
        Assert.Equal(option.BibPublicId, item.LinkedBibPublicId);
    }

    [Fact]
    public async Task Opac_finds_the_published_item_linked_to_a_print_bib_but_not_an_unpublished_or_unlinked_one()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bibPublicId = Guid.CreateVersion7();
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Số đỏ (số hoá)", LinkedBibPublicId: bibPublicId), Json));
        await staff.PutAsJsonAsync(U("/api/items/ChangeStatus"), new ChangeStatusRequest(item.PublicId, IHasStatus.Active), Json);

        var opac = Opac(tenantId);
        var found = await Read<EbookByBibSummary>(await opac.GetAsync(U($"/api/opac/items/by-bib/{bibPublicId}")));
        Assert.Equal((item.PublicId, item.Title), (found.PublicId, found.Title));

        Assert.Equal(204, (int)(await opac.GetAsync(U($"/api/opac/items/by-bib/{Guid.CreateVersion7()}"))).StatusCode);

        var hiddenBib = Guid.CreateVersion7();
        await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Ẩn", LinkedBibPublicId: hiddenBib, Status: IHasStatus.Inactive), Json);
        Assert.Equal(204, (int)(await opac.GetAsync(U($"/api/opac/items/by-bib/{hiddenBib}"))).StatusCode);
    }

    [Fact]
    public async Task Opac_only_shows_published_items_and_hides_hidden_ones()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Văn học"), Json));
        var published = await Read<EbookItemDto>(await staff.PostAsJsonAsync(
            U("/api/items/Add"), new EbookItemFields("Dế Mèn phiêu lưu ký", CollectionId: collection.Id, Author: "Tô Hoài"), Json));
        var hidden = await Read<EbookItemDto>(await staff.PostAsJsonAsync(
            U("/api/items/Add"), new EbookItemFields("Bản thảo chưa duyệt", Status: IHasStatus.Inactive), Json));

        var opac = Opac(tenantId);
        var found = await Read<CrudPage<OpacItemSummary>>(await opac.PostAsJsonAsync(U("/api/opac/items/Search"), new OpacItemSearch("dế mèn"), Json));
        Assert.Equal(published.PublicId, Assert.Single(found.Items).PublicId);

        var detail = await Read<OpacItemDetail>(await opac.GetAsync(U($"/api/opac/items/{published.PublicId}")));
        Assert.Equal((published.Title, collection.Name), (detail.Title, detail.CollectionName));

        Assert.Equal(404, (int)(await opac.GetAsync(U($"/api/opac/items/{hidden.PublicId}"))).StatusCode);

        // Đơn vị khác không thấy tài liệu của đơn vị này.
        Assert.Equal(0, (await Read<CrudPage<OpacItemSummary>>(
            await Opac(NewTenant()).PostAsJsonAsync(U("/api/opac/items/Search"), new OpacItemSearch(), Json))).TotalCount);
    }

    [Fact]
    public async Task Opac_catalog_trees_only_list_active_nodes_with_child_counts()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var root = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Khoa học"), Json));
        await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Vật lý", ParentId: root.Id), Json));
        var hidden = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Ẩn"), Json));
        await staff.PutAsJsonAsync(U("/api/collections/ChangeStatus"), new ChangeStatusRequest(hidden.PublicId, IHasStatus.Inactive), Json);

        var tree = await Read<List<OpacCatalogNode>>(await Opac(tenantId).GetAsync(U("/api/opac/collections")));
        Assert.DoesNotContain(tree, n => n.Id == hidden.Id);
        Assert.True(Assert.Single(tree, n => n.Id == root.Id).HasChildren);
    }
}
