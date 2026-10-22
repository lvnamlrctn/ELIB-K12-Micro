using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Digital.Application;

namespace Elib.Digital.Tests.Api;

public sealed class MetadataApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private async Task<(HttpClient Staff, EbookItemDto Item, MetadataFieldDto Creator, MetadataFieldDto Issued)> SetupAsync(long tenantId)
    {
        var staff = Staff(tenantId);
        var schema = await Read<MetadataSchemaDto>(await staff.PostAsJsonAsync(U("/api/metadata-schemas/Add"), new MetadataSchemaRequest("http://purl.org/dc/elements/1.1/", "dc", "vi", null), Json));
        var creator = await Read<MetadataFieldDto>(await staff.PostAsJsonAsync(U("/api/metadata-fields/Add"),
            new MetadataFieldRequest(schema.PublicId, "creator", null, "Tác giả", "Creator", 1, "text", 1), Json));
        var issued = await Read<MetadataFieldDto>(await staff.PostAsJsonAsync(U("/api/metadata-fields/Add"),
            new MetadataFieldRequest(schema.PublicId, "date", "issued", "Ngày phát hành", null, 2, "date"), Json));
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new Elib.Digital.Domain.EbookItemFields("Số đỏ"), Json));
        return (staff, item, creator, issued);
    }

    [Fact]
    public async Task Metadata_is_replaced_as_a_whole_and_repeatable_fields_keep_their_order()
    {
        var tenantId = NewTenant();
        var (staff, item, creator, issued) = await SetupAsync(tenantId);

        var saved = await Read<List<EbookMetadataRow>>(await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"), new EbookMetadataSave(
        [
            new EbookMetadataEntry(creator.PublicId, "Vũ Trọng Phụng"), new EbookMetadataEntry(issued.PublicId, "1936-01"), new EbookMetadataEntry(creator.PublicId, "Nhiều tác giả"),
            new EbookMetadataEntry(creator.PublicId, "   "), // dòng trống bị bỏ
        ]), Json));
        Assert.Equal(["Vũ Trọng Phụng", "Nhiều tác giả", "1936-01"], saved.Select(r => r.Value));

        var after = await Read<List<EbookMetadataRow>>(await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"),
            new EbookMetadataSave([new EbookMetadataEntry(creator.PublicId, "Chỉ một")]), Json));
        Assert.Equal("Chỉ một", Assert.Single(after).Value);
        Assert.Equal("Chỉ một", Assert.Single(await Read<List<EbookMetadataRow>>(await staff.GetAsync(U($"/api/items/{item.PublicId}/metadata")))).Value);
    }

    [Fact]
    public async Task Values_are_checked_against_the_field_input_type()
    {
        var tenantId = NewTenant();
        var (staff, item, _, issued) = await SetupAsync(tenantId);

        var bad = await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"), new EbookMetadataSave([new EbookMetadataEntry(issued.PublicId, "hôm qua")]), Json);
        Assert.Equal("METADATA_VALUE_INVALID", (await Error(bad)).Code);

        var unknown = await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"), new EbookMetadataSave([new EbookMetadataEntry(Guid.CreateVersion7(), "x")]), Json);
        Assert.Equal(404, (int)unknown.StatusCode);
    }

    [Fact]
    public async Task A_field_with_data_cannot_be_deleted_and_a_hidden_field_cannot_take_new_values()
    {
        var tenantId = NewTenant();
        var (staff, item, creator, issued) = await SetupAsync(tenantId);
        await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"), new EbookMetadataSave([new EbookMetadataEntry(creator.PublicId, "A")]), Json);

        var delete = await staff.DeleteAsync(U($"/api/metadata-fields/Delete/{creator.PublicId}"));
        Assert.Equal("FIELD_IN_USE", (await Error(delete)).Code);

        await staff.PutAsJsonAsync(U("/api/metadata-fields/ChangeStatus"), new ChangeStatusRequest(creator.PublicId, 1), Json);
        var hidden = await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"), new EbookMetadataSave([new EbookMetadataEntry(creator.PublicId, "B")]), Json);
        Assert.Equal("FIELD_INACTIVE", (await Error(hidden)).Code);

        // Trường ẩn không hiện ra form và không bị xoá khi lưu metadata của trường khác (dữ liệu đã nhập được giữ).
        Assert.Empty(await Read<List<EbookMetadataRow>>(await staff.GetAsync(U($"/api/items/{item.PublicId}/metadata"))));
        var saved = await Read<List<EbookMetadataRow>>(await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"),
            new EbookMetadataSave([new EbookMetadataEntry(issued.PublicId, "2020")]), Json));
        Assert.Equal("2020", Assert.Single(saved).Value);
        await staff.PutAsJsonAsync(U("/api/metadata-fields/ChangeStatus"), new ChangeStatusRequest(creator.PublicId, 2), Json);
        Assert.Equal(["A", "2020"], (await Read<List<EbookMetadataRow>>(await staff.GetAsync(U($"/api/items/{item.PublicId}/metadata")))).Select(r => r.Value));
    }

    [Fact]
    public async Task Deleting_an_item_removes_its_metadata_and_other_tenants_cannot_see_it()
    {
        var tenantId = NewTenant();
        var (staff, item, creator, _) = await SetupAsync(tenantId);
        await staff.PutAsJsonAsync(U($"/api/items/{item.PublicId}/metadata"), new EbookMetadataSave([new EbookMetadataEntry(creator.PublicId, "A")]), Json);

        var other = Staff(NewTenant());
        Assert.Equal(404, (int)(await other.GetAsync(U($"/api/items/{item.PublicId}/metadata"))).StatusCode);

        var delete = await staff.DeleteAsync(U($"/api/items/Delete/{item.PublicId}"));
        Assert.True(delete.IsSuccessStatusCode, await delete.Content.ReadAsStringAsync());
        // Xoá được trường vì không còn giá trị nào dùng nó.
        Assert.True((await staff.DeleteAsync(U($"/api/metadata-fields/Delete/{creator.PublicId}"))).IsSuccessStatusCode);
    }
}
