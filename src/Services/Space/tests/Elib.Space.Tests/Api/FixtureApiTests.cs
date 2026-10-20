using System.Net;
using System.Net.Http.Json;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

/// <summary>Vật dụng đặt trên sơ đồ tầng (map.MapFixture, quyền SPACE_FIXTURES) — giá sách và các loại khác ngoài phòng.</summary>
public sealed class FixtureApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Staff_adds_a_shelf_fixture_and_places_it_on_the_floor_map()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var created = await Read<FixtureDto>(await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, "K01", "Kệ 000-099", "SHELF", "#f59e0b", "menu_book", "000 - 099", "Tổng loại", 100, "Kệ tầng 1", null), Json));
        Assert.Equal(("K01", "SHELF", "000 - 099"), (created.Code, created.ObjectType, created.ShelfCategoryRange));
        Assert.Null(created.PositionX);

        var placed = await Read<FixtureDto>(await staff.PostAsJsonAsync(U($"/api/fixtures/{created.PublicId}/Place"),
            new PlaceFixtureRequest(20, 30, 12, 8), Json));
        Assert.Equal((20d, 30d, 12d, 8d), (placed.PositionX, placed.PositionY, placed.Width, placed.Height));
    }

    [Fact]
    public async Task Non_shelf_fixture_ignores_shelf_only_fields()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var created = await Read<FixtureDto>(await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, "PC01", "Trạm máy 1", "PC", null, "computer", "000 - 099", "Không áp dụng", 5, "ghi chú", null), Json));
        Assert.Equal(("PC", null, null), (created.ObjectType, created.ShelfCategoryRange, created.ShelfCapacity));
    }

    [Fact]
    public async Task Invalid_object_type_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var response = await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, "X01", "Không hợp lệ", "ROOM", null, null, null, null, null, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.BadRequest, "OBJECT_TYPE_INVALID"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Duplicate_code_on_the_same_tenant_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        await staff.PostAsJsonAsync(U("/api/fixtures/Add"), new FixtureRequest(floor.Id, "DUP01", "Cửa 1", "DOOR", null, null, null, null, null, null, null), Json);
        var response = await staff.PostAsJsonAsync(U("/api/fixtures/Add"), new FixtureRequest(floor.Id, "DUP01", "Cửa 2", "DOOR", null, null, null, null, null, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.Conflict, "FIXTURE_CODE_EXISTS"), ((HttpStatusCode)status, code));
    }
}
