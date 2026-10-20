using System.Net;
using System.Net.Http.Json;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

/// <summary>Tủ đựng đồ trên sơ đồ tầng (PrintBook.Cabinet/CabinetCompartment, quyền SPACE_LOCKERS).</summary>
public sealed class LockerApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Creating_a_cabinet_auto_generates_the_compartment_grid()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var created = await Read<LockerCabinetDto>(await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"),
            new LockerCabinetRequest(floor.Id, "T01", "Tủ hành lang", 2, 3, "#0ea5e9", "lock", null), Json));
        Assert.Equal(("T01", 2, 3), (created.Code, created.Rows, created.Cols));

        var compartments = await Read<List<LockerCompartmentDto>>(await staff.PostAsJsonAsync(U("/api/locker-compartments/SearchAll"),
            new LockerCompartmentSearch { CabinetId = created.Id }, Json));
        Assert.Equal(6, compartments.Count);
        Assert.Contains(compartments, c => c.Code == "T01-11");
        Assert.Contains(compartments, c => c.Code == "T01-23");

        var placed = await Read<LockerCabinetDto>(await staff.PostAsJsonAsync(U($"/api/locker-cabinets/{created.PublicId}/Place"),
            new PlaceLockerRequest(10, 20, 15, 10), Json));
        Assert.Equal((10d, 20d, 15d, 10d), (placed.PositionX, placed.PositionY, placed.Width, placed.Height));
    }

    [Fact]
    public async Task Growing_the_grid_adds_compartments_and_shrinking_removes_the_extra_ones()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var created = await Read<LockerCabinetDto>(await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"),
            new LockerCabinetRequest(floor.Id, "T02", "Tủ 1", 1, 2, null, null, null), Json));

        await staff.PutAsJsonAsync(U($"/api/locker-cabinets/Update/{created.PublicId}"),
            new LockerCabinetRequest(floor.Id, "T02", "Tủ 1", 2, 2, null, null, null), Json);
        var grown = await Read<List<LockerCompartmentDto>>(await staff.PostAsJsonAsync(U("/api/locker-compartments/SearchAll"),
            new LockerCompartmentSearch { CabinetId = created.Id }, Json));
        Assert.Equal(4, grown.Count);

        await staff.PutAsJsonAsync(U($"/api/locker-cabinets/Update/{created.PublicId}"),
            new LockerCabinetRequest(floor.Id, "T02", "Tủ 1", 1, 1, null, null, null), Json);
        var shrunk = await Read<List<LockerCompartmentDto>>(await staff.PostAsJsonAsync(U("/api/locker-compartments/SearchAll"),
            new LockerCompartmentSearch { CabinetId = created.Id }, Json));
        Assert.Single(shrunk);
        Assert.Equal("T02-11", shrunk[0].Code);
    }

    [Fact]
    public async Task Duplicate_cabinet_code_on_the_same_tenant_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"), new LockerCabinetRequest(floor.Id, "DUP", "Tủ 1", 1, 1, null, null, null), Json);
        var response = await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"), new LockerCabinetRequest(floor.Id, "DUP", "Tủ 2", 1, 1, null, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.Conflict, "CABINET_CODE_EXISTS"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Rows_outside_the_allowed_range_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var response = await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"), new LockerCabinetRequest(floor.Id, "BAD", "Tủ", 0, 1, null, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.BadRequest, "ROWS_INVALID"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Staff_can_rename_a_single_compartment_without_affecting_others()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var cabinet = await Read<LockerCabinetDto>(await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"),
            new LockerCabinetRequest(floor.Id, "T03", "Tủ 3", 1, 2, null, null, null), Json));
        var compartments = await Read<List<LockerCompartmentDto>>(await staff.PostAsJsonAsync(U("/api/locker-compartments/SearchAll"),
            new LockerCompartmentSearch { CabinetId = cabinet.Id }, Json));
        var first = compartments.First(c => c.ColIndex == 1);

        var updated = await Read<LockerCompartmentDto>(await staff.PutAsJsonAsync(U($"/api/locker-compartments/Update/{first.PublicId}"),
            new LockerCompartmentRequest(cabinet.Id, first.RowIndex, first.ColIndex, first.Code, "Ngăn VIP", null), Json));
        Assert.Equal("Ngăn VIP", updated.Name);
    }
}
