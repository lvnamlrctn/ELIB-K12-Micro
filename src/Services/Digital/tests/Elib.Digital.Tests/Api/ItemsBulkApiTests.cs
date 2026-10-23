using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Application;
using Elib.Digital.Domain;

namespace Elib.Digital.Tests.Api;

/// <summary>Chọn nhiều tài liệu số: chuyển bộ sưu tập, đổi trạng thái, xoá — theo các dòng đã tích hoặc theo bộ lọc.</summary>
public sealed class ItemsBulkApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static async Task<EbookItemDto> Add(HttpClient staff, string title, long? collectionId = null) =>
        await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields(title, CollectionId: collectionId), Json));

    private static async Task<EbookCollectionDto> NewCollection(HttpClient staff, string name) =>
        await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest(name), Json));

    private static async Task<EbookItemDto> Get(HttpClient staff, Guid id) => await Read<EbookItemDto>(await staff.GetAsync(U($"/api/items/GetById/{id}")));

    [Fact]
    public async Task Selected_rows_move_to_another_collection_and_each_one_publishes_its_new_state()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var from = await NewCollection(staff, "Nguồn");
        var to = await NewCollection(staff, "Đích");
        var a = await Add(staff, "A", from.Id);
        var b = await Add(staff, "B", from.Id);
        var untouched = await Add(staff, "C", from.Id);
        var published = Factory.Published.Count;

        var result = await Read<ItemBulkResult>(await staff.PutAsJsonAsync(U("/api/items/BulkMoveCollection"),
            new ItemBulkMoveRequest(new ItemSelection([a.PublicId, b.PublicId]), to.Id), Json));

        Assert.Equal((2, 0), (result.Updated, result.Skipped.Count));
        Assert.Equal(to.Id, (await Get(staff, a.PublicId)).CollectionId);
        Assert.Equal(from.Id, (await Get(staff, untouched.PublicId)).CollectionId);
        Assert.True(Factory.Published.Count >= published + 2); // search biết bộ sưu tập mới

        Assert.Equal("COLLECTION_NOT_FOUND", (await Error(await staff.PutAsJsonAsync(U("/api/items/BulkMoveCollection"),
            new ItemBulkMoveRequest(new ItemSelection([a.PublicId]), to.Id + 999), Json))).Code);
    }

    [Fact]
    public async Task Bulk_move_and_status_keep_the_people_lists_of_items_that_have_authors_and_advisors()
    {
        var staff = Staff(NewTenant());
        var target = await NewCollection(staff, "Đích");
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Có tác giả",
            Authors: [new EbookPerson { Last = "Lê", First = "A" }, new EbookPerson { Last = "Phạm", First = "B" }], Advisors: [new EbookPerson { Last = "Vũ", First = "C", Title = "TS" }],
            Identifiers: [new EbookIdentifier { Type = "isbn", Value = "123" }]), Json));
        var selection = new ItemSelection([item.PublicId]);

        await Read<ItemBulkResult>(await staff.PutAsJsonAsync(U("/api/items/BulkMoveCollection"), new ItemBulkMoveRequest(selection, target.Id), Json));
        await Read<ItemBulkResult>(await staff.PutAsJsonAsync(U("/api/items/BulkStatus"), new ItemBulkStatusRequest(selection, IHasStatus.Inactive), Json));
        var after = await Get(staff, item.PublicId);
        Assert.Equal((target.Id, IHasStatus.Inactive, 2, "TS", "123"), (after.CollectionId, after.Status, after.Authors.Count, Assert.Single(after.Advisors).Title, Assert.Single(after.Identifiers).Value));

        var deleted = await Read<ItemBulkResult>(await staff.PostAsJsonAsync(U("/api/items/BulkDelete"), selection, Json));
        Assert.Equal(1, deleted.Updated); // xoá mềm một dòng có cột JSON không lỗi
    }

    [Fact]
    public async Task The_current_filter_can_be_the_selection_and_the_preview_counts_it()
    {
        var staff = Staff(NewTenant());
        var target = await NewCollection(staff, "Đích");
        await Add(staff, "Toán lớp 1");
        await Add(staff, "Toán lớp 2");
        await Add(staff, "Văn lớp 1");

        var selection = new ItemSelection(Search: new EbookItemSearch { Title = "toán" });
        Assert.Equal(2, (await Read<ItemSelectionPreview>(await staff.PostAsJsonAsync(U("/api/items/Selection/Preview"), selection, Json))).Count);

        await Read<ItemBulkResult>(await staff.PutAsJsonAsync(U("/api/items/BulkMoveCollection"), new ItemBulkMoveRequest(selection, target.Id), Json));
        var inTarget = await Read<CrudPage<EbookItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Search"), new EbookItemSearch { CollectionId = target.Id }, Json));
        Assert.Equal(["Toán lớp 2", "Toán lớp 1"], inTarget.Items.Select(i => i.Title));

        // Cả hai kiểu chọn cùng lúc, hoặc không chọn gì, đều bị từ chối.
        Assert.Equal("SELECTION_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/items/Selection/Preview"),
            new ItemSelection([Guid.NewGuid()], new EbookItemSearch()), Json))).Code);
        Assert.Equal("SELECTION_EMPTY", (await Error(await staff.PostAsJsonAsync(U("/api/items/Selection/Preview"), new ItemSelection([Guid.NewGuid()]), Json))).Code);
    }

    [Fact]
    public async Task Bulk_status_hides_and_republishes_and_bulk_delete_skips_items_that_still_have_files()
    {
        var staff = Staff(NewTenant());
        var plain = await Add(staff, "Không file");
        var withFile = await Add(staff, "Có file");
        await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(withFile.PublicId, Guid.CreateVersion7(), FileExt: "pdf"), Json);
        var both = new ItemSelection([plain.PublicId, withFile.PublicId]);

        await Read<ItemBulkResult>(await staff.PutAsJsonAsync(U("/api/items/BulkStatus"), new ItemBulkStatusRequest(both, IHasStatus.Inactive), Json));
        Assert.Equal(IHasStatus.Inactive, (await Get(staff, plain.PublicId)).Status);
        Assert.Equal("STATUS_INVALID", (await Error(await staff.PutAsJsonAsync(U("/api/items/BulkStatus"), new ItemBulkStatusRequest(both, 99), Json))).Code);

        var result = await Read<ItemBulkResult>(await staff.PostAsJsonAsync(U("/api/items/BulkDelete"), both, Json));
        Assert.Equal(1, result.Updated);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal((withFile.PublicId, "EBOOK_ITEM_HAS_FILES"), (skipped.PublicId, skipped.Code));
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(U($"/api/items/GetById/{plain.PublicId}"))).StatusCode);
        Assert.Equal(withFile.PublicId, (await Get(staff, withFile.PublicId)).PublicId); // còn nguyên, kể cả metadata
    }

    [Fact]
    public async Task The_cover_can_be_set_alone_without_touching_other_fields_and_only_from_own_uploads()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"),
            new EbookItemFields("Có bìa", Authors: [new EbookPerson { Last = "Lê", First = "A" }], Abstract: "Tóm tắt"), Json));
        var cover = $"/s3/media-public/{tenantId}/ebook-cover/2026/10/{Guid.NewGuid():N}.jpg";

        var updated = await Read<EbookItemDto>(await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/cover"), new SetCoverRequest(cover), Json));
        Assert.Equal((cover, "Lê A", "Tóm tắt"), (updated.CoverUrl, updated.Author, updated.Abstract));
        Assert.Equal("COVER_URL_INVALID", (await Error(await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/cover"), new SetCoverRequest("https://nguoi-la.example/a.png"), Json))).Code);
        Assert.Null((await Read<EbookItemDto>(await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/cover"), new SetCoverRequest(null), Json))).CoverUrl);
    }

    [Fact]
    public async Task Bulk_actions_need_the_matching_permission_and_stay_inside_the_tenant()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var item = await Add(staff, "Của đơn vị A");
        var other = Staff(NewTenant());
        var selection = new ItemSelection([item.PublicId]);

        Assert.Equal("SELECTION_EMPTY", (await Error(await other.PostAsJsonAsync(U("/api/items/BulkDelete"), selection, Json))).Code); // không thấy tài liệu đơn vị khác
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_ITEMS:view").PostAsJsonAsync(U("/api/items/BulkDelete"), selection, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_ITEMS:view", "DIGITAL_ITEMS:delete").PutAsJsonAsync(U("/api/items/BulkStatus"),
            new ItemBulkStatusRequest(selection, IHasStatus.Inactive), Json)).StatusCode);
    }
}
