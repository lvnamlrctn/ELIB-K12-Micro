using System.Net.Http.Json;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

/// <summary>Thiết bị gắn theo vật dụng (map.MapEquipment) và tiện ích theo tầng (map.MapFloorUtility) — quyền SPACE_FIXTURES/SPACE_FLOORS.</summary>
public sealed class FixtureEquipmentAndFloorUtilityApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Staff_adds_equipment_to_a_fixture()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var fixture = await Read<FixtureDto>(await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, "K02", "Kệ 100-199", "SHELF", null, null, "100 - 199", "Triết học", 100, null, null), Json));

        var created = await Read<FixtureEquipmentDto>(await staff.PostAsJsonAsync(U("/api/fixture-equipment/Add"),
            new FixtureEquipmentRequest(fixture.Id, "Đèn chiếu sáng", "Gắn phía trên kệ", 2, 1), Json));
        Assert.Equal(("Đèn chiếu sáng", 2, 1), (created.Name, created.Quantity, created.ConditionStatus));

        var list = await Read<List<FixtureEquipmentDto>>(await staff.PostAsJsonAsync(U("/api/fixture-equipment/SearchAll"),
            new FixtureEquipmentSearch { FixtureId = fixture.Id }, Json));
        Assert.Single(list);
    }

    [Fact]
    public async Task Staff_adds_a_floor_utility()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var created = await Read<FloorUtilityDto>(await staff.PostAsJsonAsync(U("/api/floor-utilities/Add"),
            new FloorUtilityRequest(floor.Id, "Bình chữa cháy", null, 1, 2, "local_fire_department"), Json));
        Assert.Equal(("Bình chữa cháy", 2), (created.Name, created.ConditionStatus));

        var list = await Read<List<FloorUtilityDto>>(await staff.PostAsJsonAsync(U("/api/floor-utilities/SearchAll"),
            new FloorUtilitySearch { FloorId = floor.Id }, Json));
        Assert.Single(list);
    }

    [Fact]
    public async Task Invalid_condition_status_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var response = await staff.PostAsJsonAsync(U("/api/floor-utilities/Add"), new FloorUtilityRequest(floor.Id, "X", null, null, 99, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((System.Net.HttpStatusCode.BadRequest, "CONDITION_STATUS_INVALID"), ((System.Net.HttpStatusCode)status, code));
    }
}
