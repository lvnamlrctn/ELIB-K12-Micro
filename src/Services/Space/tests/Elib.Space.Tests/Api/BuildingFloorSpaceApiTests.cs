using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

public sealed class BuildingFloorSpaceApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Staff_creates_a_building_a_floor_and_a_bookable_space()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var building = await Read<BuildingDto>(await staff.PostAsJsonAsync(U("/api/buildings/Add"), new BuildingRequest("A1", "Toà A1", "Khu chính"), Json));
        Assert.Equal(("A1", "Toà A1"), (building.Code, building.Name));

        var floor = await Read<FloorDto>(await staff.PostAsJsonAsync(U("/api/floors/Add"), new FloorRequest(building.Id, "Tầng 1", 1), Json));
        Assert.Equal((building.Id, "Toà A1", "Tầng 1"), (floor.BuildingId, floor.BuildingName, floor.Name));

        var space = await Read<SpaceDto>(await staff.PostAsJsonAsync(U("/api/spaces/Add"),
            new SpaceRequest(floor.Id, "P101", "Phòng học nhóm 101", 1 /* Room */, 2, 8, 30, 7, null, 0, 15, false,
                "Giữ gìn trật tự", [1, 2], 2), Json));
        Assert.Equal(("P101", "Phòng học nhóm 101", floor.Id, "Tầng 1", building.Id, "Toà A1"), (space.Code, space.Name, space.FloorId, space.FloorName, space.BuildingId, space.BuildingName));
        Assert.Equal([1, 2], space.AllowedReaderTypeIds);

        var page = await Read<CrudPage<SpaceDto>>(await staff.PostAsJsonAsync(U("/api/spaces/Search"), new SpaceSearch { FloorId = floor.Id }, Json));
        Assert.Equal(space.PublicId, Assert.Single(page.Items).PublicId);
    }

    [Fact]
    public async Task Duplicate_building_code_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        await staff.PostAsJsonAsync(U("/api/buildings/Add"), new BuildingRequest("A1", "Toà A1", null), Json);

        var dup = await staff.PostAsJsonAsync(U("/api/buildings/Add"), new BuildingRequest("a1", "Toà A1 khác", null), Json);
        var (status, code, _) = await Error(dup);
        Assert.Equal((HttpStatusCode.Conflict, "BUILDING_CODE_EXISTS"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Minimum_group_size_cannot_exceed_capacity()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await Read<BuildingDto>(await staff.PostAsJsonAsync(U("/api/buildings/Add"), new BuildingRequest("A1", "Toà A1", null), Json));
        var floor = await Read<FloorDto>(await staff.PostAsJsonAsync(U("/api/floors/Add"), new FloorRequest(building.Id, "Tầng 1", 1), Json));

        var response = await staff.PostAsJsonAsync(U("/api/spaces/Add"),
            new SpaceRequest(floor.Id, "P101", "Phòng 101", 1 /* Room */, 2, 4, 0, 7, null, 0, 15, false, null, null, 10), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.BadRequest, "MIN_GROUP_EXCEEDS_CAPACITY"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Deleting_a_building_with_floors_is_blocked()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await Read<BuildingDto>(await staff.PostAsJsonAsync(U("/api/buildings/Add"), new BuildingRequest("A1", "Toà A1", null), Json));
        await staff.PostAsJsonAsync(U("/api/floors/Add"), new FloorRequest(building.Id, "Tầng 1", 1), Json);

        var response = await staff.DeleteAsync(U($"/api/buildings/Delete/{building.PublicId}"));
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.Conflict, "BUILDING_IN_USE"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Staff_places_a_space_on_the_floor_map_and_toggles_maintenance()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P102");

        var placed = await Read<SpaceDto>(await staff.PostAsJsonAsync(U($"/api/spaces/{space.PublicId}/Place"), new PlaceSpaceRequest(10, 20, 30, 40), Json));
        Assert.Equal((10, 20, 30, 40), (placed.PositionX, placed.PositionY, placed.Width, placed.Height));

        var maintained = await Read<SpaceDto>(await staff.PostAsJsonAsync(U($"/api/spaces/{space.PublicId}/SetMaintenance"),
            new SetMaintenanceRequest(true, "Sửa điều hoà"), Json));
        Assert.True(maintained.Maintenance);
        Assert.Equal("Sửa điều hoà", maintained.MaintenanceNote);

        var cleared = await Read<SpaceDto>(await staff.PostAsJsonAsync(U($"/api/spaces/{space.PublicId}/SetMaintenance"),
            new SetMaintenanceRequest(false, null), Json));
        Assert.False(cleared.Maintenance);
    }

    [Fact]
    public async Task Staff_sets_the_floor_layout_image_from_an_uploaded_media_url()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var url = $"/s3/media-public/{tenantId}/space-floorplan/2026/10/{Guid.NewGuid():N}.png";

        var updated = await Read<FloorDto>(await staff.PostAsJsonAsync(U($"/api/floors/{floor.PublicId}/SetLayout"),
            new SetFloorLayoutRequest(url, 1200, 800), Json));
        Assert.Equal((url, 1200, 800), (updated.LayoutImageUrl, updated.CanvasWidth, updated.CanvasHeight));

        var wrongTenantUrl = $"/s3/media-public/{tenantId + 999}/space-floorplan/2026/10/{Guid.NewGuid():N}.png";
        var rejected = await staff.PostAsJsonAsync(U($"/api/floors/{floor.PublicId}/SetLayout"), new SetFloorLayoutRequest(wrongTenantUrl, 1200, 800), Json);
        var (status, code, _) = await Error(rejected);
        Assert.Equal((System.Net.HttpStatusCode.BadRequest, "LAYOUT_URL_INVALID"), ((System.Net.HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Tenants_do_not_see_each_others_buildings()
    {
        var tenantA = NewTenant();
        var tenantB = NewTenant();
        var staffA = Staff(tenantA);
        var staffB = Staff(tenantB);
        await staffA.PostAsJsonAsync(U("/api/buildings/Add"), new BuildingRequest("A1", "Toà A1", null), Json);

        var page = await Read<CrudPage<BuildingDto>>(await staffB.PostAsJsonAsync(U("/api/buildings/Search"), new CrudSearch(), Json));
        Assert.Empty(page.Items);
    }
}
