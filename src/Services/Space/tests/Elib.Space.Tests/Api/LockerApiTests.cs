using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
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

    [Fact]
    public async Task A_large_grid_generates_unique_compartment_codes()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        // Dòng 1 cột 11 và dòng 11 cột 1 không được cùng ra "-111" (mã ngăn duy nhất trong đơn vị).
        var cabinet = await Read<LockerCabinetDto>(await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"),
            new LockerCabinetRequest(floor.Id, "BIG", "Tủ lớn", 11, 11, null, null, null), Json));
        var compartments = await Read<List<LockerCompartmentDto>>(await staff.PostAsJsonAsync(U("/api/locker-compartments/SearchAll"),
            new LockerCompartmentSearch { CabinetId = cabinet.Id }, Json));
        Assert.Equal(121, compartments.Count);
        Assert.Equal(121, compartments.Select(c => c.Code).Distinct().Count());
        Assert.Contains(compartments, c => c.Code == "BIG-11");
        Assert.Contains(compartments, c => c.Code == "BIG-1-11");
        Assert.Contains(compartments, c => c.Code == "BIG-11-1");

        var tooLong = await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"),
            new LockerCabinetRequest(floor.Id, "TU-CO-MA-RAT-DAI-1", "Tủ", 12, 12, null, null, null), Json);
        Assert.Equal("CABINET_CODE_TOO_LONG", (await Error(tooLong)).Item2);
    }

    [Fact]
    public async Task A_deleted_compartment_stays_deleted_after_saving_the_cabinet_and_can_be_added_back_in_its_cell()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);

        var cabinet = await Read<LockerCabinetDto>(await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"),
            new LockerCabinetRequest(floor.Id, "T09", "Tủ 9", 1, 3, null, null, null), Json));
        async Task<List<LockerCompartmentDto>> ListAsync() => await Read<List<LockerCompartmentDto>>(await staff.PostAsJsonAsync(U("/api/locker-compartments/SearchAll"),
            new LockerCompartmentSearch { CabinetId = cabinet.Id }, Json));
        var middle = (await ListAsync()).Single(c => c.ColIndex == 2);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/locker-compartments/Delete/{middle.PublicId}"))).StatusCode);

        // Lưu lại tủ (cùng lưới) không sinh lại ngăn đã xoá.
        await staff.PutAsJsonAsync(U($"/api/locker-cabinets/Update/{cabinet.PublicId}"), new LockerCabinetRequest(floor.Id, "T09", "Tủ 9 (đổi tên)", 1, 3, null, null, null), Json);
        Assert.Equal([1, 3], (await ListAsync()).Select(c => c.ColIndex).Order());

        // Lưới lớn lên chỉ sinh ngăn cho ô mới lộ ra.
        await staff.PutAsJsonAsync(U($"/api/locker-cabinets/Update/{cabinet.PublicId}"), new LockerCabinetRequest(floor.Id, "T09", "Tủ 9", 1, 4, null, null, null), Json);
        Assert.Equal([1, 3, 4], (await ListAsync()).Select(c => c.ColIndex).Order());

        // Thêm lại ngăn vào ô đã xoá; ô đã có ngăn hoặc ngoài lưới bị từ chối.
        var back = await Read<LockerCompartmentDto>(await staff.PostAsJsonAsync(U("/api/locker-compartments/Add"),
            new LockerCompartmentRequest(cabinet.Id, 1, 2, "T09-2", "Ngăn giữa", null), Json));
        Assert.Equal((1, 2, "T09-2"), (back.RowIndex, back.ColIndex, back.Code));
        var taken = await staff.PostAsJsonAsync(U("/api/locker-compartments/Add"), new LockerCompartmentRequest(cabinet.Id, 1, 2, "T09-X", null, null), Json);
        Assert.Equal("COMPARTMENT_CELL_TAKEN", (await Error(taken)).Item2);
        var outside = await staff.PostAsJsonAsync(U("/api/locker-compartments/Add"), new LockerCompartmentRequest(cabinet.Id, 2, 1, "T09-Y", null, null), Json);
        Assert.Equal("COMPARTMENT_OUT_OF_GRID", (await Error(outside)).Item2);
    }

    private static async Task<(LockerCabinetDto Cabinet, LockerCompartmentDto Compartment)> AddCabinetWithOneCompartmentAsync(HttpClient staff, long floorId, string code)
    {
        var cabinet = await Read<LockerCabinetDto>(await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"),
            new LockerCabinetRequest(floorId, code, "Tủ " + code, 1, 1, null, null, null), Json));
        var compartments = await Read<List<LockerCompartmentDto>>(await staff.PostAsJsonAsync(U("/api/locker-compartments/SearchAll"),
            new LockerCompartmentSearch { CabinetId = cabinet.Id }, Json));
        return (cabinet, compartments[0]);
    }

    [Fact]
    public async Task Borrow_and_return_a_compartment_round_trip()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var reader = Reader(tenantId, "hs-l01");
        await Publish(reader);
        var (_, compartment) = await AddCabinetWithOneCompartmentAsync(staff, floor.Id, "L01");

        var borrowed = await Read<LockerLoanDto>(await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"),
            new BorrowLockerRequest(compartment.Code, "hs-l01", null), Json));
        Assert.Equal((compartment.Id, "HS-L01", 1), (borrowed.CompartmentId, borrowed.CardNo, borrowed.LoanStatus));
        Assert.Null(borrowed.ReturnedAt);

        var returned = await Read<LockerLoanDto>(await staff.PostAsJsonAsync(U("/api/locker-loans/Return"),
            new ReturnLockerRequest(compartment.Code, null), Json));
        Assert.Equal(2, returned.LoanStatus);
        Assert.NotNull(returned.ReturnedAt);
    }

    [Fact]
    public async Task Borrowing_the_same_compartment_again_by_the_same_reader_is_idempotent()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var reader = Reader(tenantId, "hs-l02");
        await Publish(reader);
        var (_, compartment) = await AddCabinetWithOneCompartmentAsync(staff, floor.Id, "L02");

        var first = await Read<LockerLoanDto>(await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"),
            new BorrowLockerRequest(compartment.Code, "hs-l02", null), Json));
        var again = await Read<LockerLoanDto>(await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"),
            new BorrowLockerRequest(compartment.Code, "hs-l02", null), Json));
        Assert.Equal(first.PublicId, again.PublicId);
    }

    [Fact]
    public async Task Borrowing_a_compartment_held_by_another_reader_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var readerA = Reader(tenantId, "hs-l03a");
        var readerB = Reader(tenantId, "hs-l03b");
        await Publish(readerA, readerB);
        var (_, compartment) = await AddCabinetWithOneCompartmentAsync(staff, floor.Id, "L03");

        await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"), new BorrowLockerRequest(compartment.Code, "hs-l03a", null), Json);
        var blocked = await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"), new BorrowLockerRequest(compartment.Code, "hs-l03b", null), Json);
        var (status, code, _) = await Error(blocked);
        Assert.Equal((HttpStatusCode.Conflict, "COMPARTMENT_OCCUPIED"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Borrowing_an_inactive_compartment_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var reader = Reader(tenantId, "hs-l04");
        await Publish(reader);
        var (_, compartment) = await AddCabinetWithOneCompartmentAsync(staff, floor.Id, "L04");

        await staff.PutAsJsonAsync(U("/api/locker-compartments/ChangeStatus"), new ChangeStatusRequest(compartment.PublicId, IHasStatus.Inactive), Json);
        var blocked = await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"), new BorrowLockerRequest(compartment.Code, "hs-l04", null), Json);
        var (status, code, _) = await Error(blocked);
        Assert.Equal((HttpStatusCode.BadRequest, "COMPARTMENT_INACTIVE"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Shrinking_the_grid_is_blocked_while_a_compartment_has_an_active_loan()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var reader = Reader(tenantId, "hs-l05");
        await Publish(reader);

        var cabinet = await Read<LockerCabinetDto>(await staff.PostAsJsonAsync(U("/api/locker-cabinets/Add"),
            new LockerCabinetRequest(floor.Id, "L05", "Tủ 5", 1, 2, null, null, null), Json));
        var compartments = await Read<List<LockerCompartmentDto>>(await staff.PostAsJsonAsync(U("/api/locker-compartments/SearchAll"),
            new LockerCompartmentSearch { CabinetId = cabinet.Id }, Json));
        var toBeRemoved = compartments.First(c => c.ColIndex == 2);
        await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"), new BorrowLockerRequest(toBeRemoved.Code, "hs-l05", null), Json);

        var response = await staff.PutAsJsonAsync(U($"/api/locker-cabinets/Update/{cabinet.PublicId}"),
            new LockerCabinetRequest(floor.Id, "L05", "Tủ 5", 1, 1, null, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.BadRequest, "GRID_SHRINK_HAS_ACTIVE_LOAN"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Deleting_a_compartment_with_an_active_loan_is_blocked()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var reader = Reader(tenantId, "hs-l06");
        await Publish(reader);
        var (_, compartment) = await AddCabinetWithOneCompartmentAsync(staff, floor.Id, "L06");
        await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"), new BorrowLockerRequest(compartment.Code, "hs-l06", null), Json);

        var response = await staff.DeleteAsync(U($"/api/locker-compartments/Delete/{compartment.PublicId}"));
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.BadRequest, "COMPARTMENT_HAS_ACTIVE_LOAN"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Snapshot_and_search_reflect_the_reader_s_open_loan()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var reader = Reader(tenantId, "hs-l07");
        await Publish(reader);
        var (_, compartment) = await AddCabinetWithOneCompartmentAsync(staff, floor.Id, "L07");
        await staff.PostAsJsonAsync(U("/api/locker-loans/Borrow"), new BorrowLockerRequest(compartment.Code, "hs-l07", null), Json);

        var snapshot = await Read<List<LockerLoanDto>>(await staff.PostAsJsonAsync(U("/api/locker-loans/Snapshot"), new SnapshotLockerRequest("hs-l07"), Json));
        Assert.Single(snapshot);
        Assert.Equal(compartment.Id, snapshot[0].CompartmentId);

        var openResults = await Read<CrudPage<LockerLoanDto>>(await staff.PostAsJsonAsync(U("/api/locker-loans/Search"),
            new LockerLoanSearch { CardNo = "hs-l07", LoanStatus = 1 }, Json));
        Assert.Single(openResults.Items);

        await staff.PostAsJsonAsync(U("/api/locker-loans/Return"), new ReturnLockerRequest(compartment.Code, null), Json);
        var afterReturn = await Read<List<LockerLoanDto>>(await staff.PostAsJsonAsync(U("/api/locker-loans/Snapshot"), new SnapshotLockerRequest("hs-l07"), Json));
        Assert.Empty(afterReturn);
    }
}
