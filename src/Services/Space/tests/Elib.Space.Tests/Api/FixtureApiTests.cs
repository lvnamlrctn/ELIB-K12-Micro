using System.Net;
using System.Net.Http.Json;
using Elib.Contracts.Events.Holdings;
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

    /// <summary>Holdings phát StoreChanged khi tạo/sửa kho — space giữ bản sao để gợi ý khi đặt giá sách (không phải dẫn đường định sách thật).</summary>
    [Fact]
    public async Task Store_replica_updates_from_holdings_event_and_can_be_picked_for_a_shelf()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var storePublicId = Guid.CreateVersion7();

        await Publish(new StoreChanged { TenantId = tenantId, StorePublicId = storePublicId, Code = "KC", Name = "Kho chung" });

        var stores = await Read<List<StoreOptionDto>>(await staff.GetAsync(U("/api/stores")));
        Assert.Contains(stores, s => s.StorePublicId == storePublicId && s.Code == "KC");

        var created = await Read<FixtureDto>(await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, "K03", "Kệ 200-299", "SHELF", null, null, "200 - 299", "Tôn giáo", 50, null, storePublicId), Json));
        Assert.Equal(("Kho chung", storePublicId), (created.StoreName, created.StorePublicId));

        // Xoá kho ở holdings → không còn gợi ý, tạo giá sách mới gắn kho đã xoá bị chặn.
        await Publish(new StoreChanged { TenantId = tenantId, StorePublicId = storePublicId, Code = "KC", Name = "Kho chung", Deleted = true });
        var afterDelete = await Read<List<StoreOptionDto>>(await staff.GetAsync(U("/api/stores")));
        Assert.DoesNotContain(afterDelete, s => s.StorePublicId == storePublicId);

        var blocked = await staff.PostAsJsonAsync(U("/api/fixtures/Add"),
            new FixtureRequest(floor.Id, "K04", "Kệ khác", "SHELF", null, null, null, null, null, null, storePublicId), Json);
        var (status, code, _) = await Error(blocked);
        Assert.Equal((HttpStatusCode.BadRequest, "STORE_NOT_FOUND"), ((HttpStatusCode)status, code));
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
