using System.Net;
using System.Net.Http.Json;
using Elib.Contracts.Events.Space;
using Microsoft.Extensions.DependencyInjection;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

/// <summary>Ngăn theo khoảng DDC của giá sách (map.MapShelfRow) và các sự kiện ShelfChanged/FloorChanged cho holdings.</summary>
public sealed class ShelfRowApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    private async Task<(HttpClient Staff, BuildingDto Building, FloorDto Floor, FixtureDto Shelf, long TenantId)> ShelfAsync(string code = "KE1")
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var shelf = await Read<FixtureDto>(await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, code, "Kệ khoa học", "SHELF", "#f59e0b", null, "300 - 399", "Khoa học xã hội", 100, null, null), Json));
        return (staff, building, floor, shelf, tenantId);
    }

    private static Task<HttpResponseMessage> AddRow(HttpClient staff, long fixtureId, int index, string from, string to) =>
        staff.PostAsJsonAsync(U("/api/shelf-rows/Add"), new ShelfRowRequest(fixtureId, index, from, to, null), Json);

    private ShelfChanged LastShelfEvent(Guid shelfPublicId) => Factory.PublishedOf<ShelfChanged>().Last(e => e.ShelfPublicId == shelfPublicId);

    [Fact]
    public async Task Rows_are_added_with_non_overlapping_ddc_ranges_and_each_change_republishes_the_shelf()
    {
        var (staff, _, _, shelf, _) = await ShelfAsync("KE2");
        Assert.Empty(LastShelfEvent(shelf.PublicId).Rows); // giá mới tạo: chưa có ngăn

        var first = await Read<ShelfRowDto>(await AddRow(staff, shelf.Id, 1, "300", "329"));
        await Read<ShelfRowDto>(await AddRow(staff, shelf.Id, 2, "330", "339"));
        var rows = LastShelfEvent(shelf.PublicId).Rows;
        Assert.Equal([("300", "329"), ("330", "339")], rows.Select(r => (r.DdcStart, r.DdcEnd)));
        Assert.Equal(first.PublicId, rows[0].RowPublicId);

        // "339.5" thuộc khoảng 330–339 (tiền tố): ngăn mới 339–345 chồng với ngăn 2.
        var overlap = await Error(await AddRow(staff, shelf.Id, 3, "339", "345"));
        Assert.Equal(("SHELF_ROW_OVERLAP", (int)HttpStatusCode.Conflict), (overlap.Item2, overlap.Item1));
        Assert.Equal("ROW_INDEX_EXISTS", (await Error(await AddRow(staff, shelf.Id, 2, "340", "349"))).Item2);
        Assert.Equal("DDC_RANGE_INVALID", (await Error(await AddRow(staff, shelf.Id, 3, "349", "340"))).Item2);

        // Sửa ngăn: đổi khoảng, ngăn khác không bị coi là chồng với chính nó.
        var updated = await Read<ShelfRowDto>(await staff.PutAsJsonAsync(U($"/api/shelf-rows/Update/{first.PublicId}"),
            new ShelfRowRequest(shelf.Id, 1, "300", "325", "Tổng quát"), Json));
        Assert.Equal("325", updated.DdcEnd);
        Assert.Equal("325", LastShelfEvent(shelf.PublicId).Rows[0].DdcEnd);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/shelf-rows/Delete/{first.PublicId}"))).StatusCode);
        Assert.Equal([2], LastShelfEvent(shelf.PublicId).Rows.Select(r => r.RowIndex));
    }

    [Fact]
    public async Task Rows_can_only_belong_to_a_shelf_fixture()
    {
        var (staff, _, floor, _, _) = await ShelfAsync("KE3");
        var pc = await Read<FixtureDto>(await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, "PC9", "Máy", "PC", null, null, null, null, null, null, null), Json));
        Assert.Equal("SHELF_NOT_FOUND", (await Error(await AddRow(staff, pc.Id, 1, "000", "099"))).Item2);
    }

    [Fact]
    public async Task Placing_deleting_or_changing_the_type_of_a_shelf_is_published_for_holdings()
    {
        var (staff, _, floor, shelf, _) = await ShelfAsync("KE4");
        await Read<FixtureDto>(await staff.PostAsJsonAsync(U($"/api/fixtures/{shelf.PublicId}/Place"), new PlaceFixtureRequest(10, 20, 15, 8), Json));
        var placed = LastShelfEvent(shelf.PublicId);
        Assert.Equal((10d, 20d, 15d, 8d, false), (placed.PositionX, placed.PositionY, placed.Width, placed.Height, placed.Deleted));
        Assert.Equal(floor.PublicId, placed.FloorPublicId);

        // Đổi loại khỏi SHELF = giá không còn (holdings đánh dấu đã xoá).
        await Read<FixtureDto>(await staff.PutAsJsonAsync(U($"/api/fixtures/Update/{shelf.PublicId}"),
            new FixtureRequest(floor.Id, "KE4", "Không còn là giá", "OTHER", null, null, null, null, null, null, null), Json));
        Assert.True(LastShelfEvent(shelf.PublicId).Deleted);

        var second = await Read<FixtureDto>(await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, "KE5", "Kệ 2", "SHELF", null, null, null, null, null, null, null), Json));
        Assert.False(LastShelfEvent(second.PublicId).Deleted);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/fixtures/Delete/{second.PublicId}"))).StatusCode);
        Assert.True(LastShelfEvent(second.PublicId).Deleted);
    }

    [Fact]
    public async Task Floor_and_building_changes_are_published_and_everything_can_be_republished()
    {
        var (staff, building, floor, shelf, tenantId) = await ShelfAsync("KE6");
        Assert.Contains(Factory.PublishedOf<FloorChanged>(), e => e.FloorPublicId == floor.PublicId && e.Name == floor.Name);

        await Read<BuildingDto>(await staff.PutAsJsonAsync(U($"/api/buildings/Update/{building.PublicId}"),
            new BuildingRequest(building.Code, "Toà mới", null), Json));
        Assert.Equal("Toà mới", Factory.PublishedOf<FloorChanged>().Last(e => e.FloorPublicId == floor.PublicId).BuildingName);

        await Read<FloorDto>(await staff.PutAsJsonAsync(U($"/api/floors/Update/{floor.PublicId}"), new FloorRequest(building.Id, "Tầng hai", 2), Json));
        Assert.Equal("Tầng hai", Factory.PublishedOf<FloorChanged>().Last(e => e.FloorPublicId == floor.PublicId).Name);

        // Phát lại toàn bộ (space khởi động) — mỗi giá một sự kiện mới.
        var before = Factory.PublishedOf<ShelfChanged>().Count(e => e.ShelfPublicId == shelf.PublicId);
        using var scope = Factory.Services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<Elib.BuildingBlocks.Tenancy.ITenantContext>();
        using (tenant.Use(tenantId))
        {
            var count = await scope.ServiceProvider.GetRequiredService<ShelfEvents>().RepublishAllAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<Elib.BuildingBlocks.Crud.ICrudDbContext>().SaveChangesAsync();
            Assert.Equal(1, count);
        }
        await Task.Delay(300);
        Assert.True(Factory.PublishedOf<ShelfChanged>().Count(e => e.ShelfPublicId == shelf.PublicId) > before);
    }
}
