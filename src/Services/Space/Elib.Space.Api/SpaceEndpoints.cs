using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Space.Application;

namespace Elib.Space.Api;

/// <summary>
/// Không gian &amp; đặt phòng (gateway: /api/admin/space/** → /api/**, cần license SPACE). Lát 1: danh mục Toà nhà/Tầng/Không
/// gian. Đặt phòng, giờ mở cửa, cấm đặt, báo cáo, kiểm soát cửa thêm dần theo từng lát (xem plan).
/// </summary>
public static class SpaceEndpoints
{
    public const string Module = "SPACE";

    public static IEndpointRouteBuilder MapSpaceEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<BuildingResource>("/buildings", "SPACE_BUILDINGS").WithTags("Buildings");
        api.MapCrud<FloorResource>("/floors", "SPACE_FLOORS").WithTags("Floors");
        api.MapPost("/floors/{publicId:guid}/SetLayout", [Permission("SPACE_FLOORS", "edit")]
            (Guid publicId, SetFloorLayoutRequest request, FloorResource r, CancellationToken ct) => r.SetLayoutAsync(publicId, request, ct)).WithTags("Floors");
        api.MapCrud<SpaceResource>("/spaces", "SPACE_ROOMS").WithTags("Spaces");
        api.MapPost("/spaces/{publicId:guid}/Place", [Permission("SPACE_ROOMS", "edit")]
            (Guid publicId, PlaceSpaceRequest request, SpaceResource r, CancellationToken ct) => r.PlaceAsync(publicId, request, ct)).WithTags("Spaces");
        api.MapPost("/spaces/{publicId:guid}/SetMaintenance", [Permission("SPACE_ROOMS", "edit")]
            (Guid publicId, SetMaintenanceRequest request, SpaceResource r, CancellationToken ct) => r.SetMaintenanceAsync(publicId, request, ct)).WithTags("Spaces");
        api.MapCrud<FixtureResource>("/fixtures", "SPACE_FIXTURES").WithTags("Fixtures");
        api.MapPost("/fixtures/{publicId:guid}/Place", [Permission("SPACE_FIXTURES", "edit")]
            (Guid publicId, PlaceFixtureRequest request, FixtureResource r, CancellationToken ct) => r.PlaceAsync(publicId, request, ct)).WithTags("Fixtures");
        api.MapGet("/stores", [Permission("SPACE_FIXTURES", "view")]
            (Replicas replicas, CancellationToken ct) => replicas.StoresAsync(ct)).WithTags("Fixtures");
        api.MapCrud<FixtureEquipmentResource>("/fixture-equipment", "SPACE_FIXTURES").WithTags("FixtureEquipment");
        api.MapCrud<FloorUtilityResource>("/floor-utilities", "SPACE_FLOORS").WithTags("FloorUtilities");
        api.MapCrud<SpecialDayResource>("/special-days", "SPACE_HOURS").WithTags("SpecialDays");

        var hours = api.MapGroup("/hours").WithTags("Hours");
        hours.MapGet("/weekly", [Permission("SPACE_HOURS", "view")] (int? category, HoursResource r, CancellationToken ct) => r.GetWeeklyAsync(category, ct));
        hours.MapPut("/weekly", [Permission("SPACE_HOURS", "edit")] (SaveWeeklyHoursRequest request, HoursResource r, CancellationToken ct) => r.SaveWeeklyAsync(request, ct));

        var settings = api.MapGroup("/settings").WithTags("Settings");
        settings.MapGet("/", [Permission("SPACE_SETTINGS", "view")] (SettingsResource r, CancellationToken ct) => r.GetAsync(ct));
        settings.MapPut("/", [Permission("SPACE_SETTINGS", "edit")] (UpdateSettingsRequest request, SettingsResource r, CancellationToken ct) => r.UpdateAsync(request, ct));

        // Lượt đặt phòng không có Add/Update chung — chỉ các thao tác nghiệp vụ (duyệt/từ chối/check-in/trả).
        var bookings = api.MapGroup("/bookings").WithTags("Bookings");
        bookings.MapPost("/Search", [Permission("SPACE_BOOKINGS", "view")] (BookingSearch search, BookingResource r, CancellationToken ct) => r.SearchWithMembersAsync(search, ct));
        bookings.MapGet("/GetById/{publicId:guid}", [Permission("SPACE_BOOKINGS", "view")] (Guid publicId, BookingResource r, CancellationToken ct) => r.GetAsync(publicId, ct));
        bookings.MapPost("/{publicId:guid}/Approve", [Permission("SPACE_BOOKINGS", "edit")] (Guid publicId, BookingResource r, CancellationToken ct) => r.ApproveAsync(publicId, ct));
        bookings.MapPost("/Reject", [Permission("SPACE_BOOKINGS", "edit")] (RejectBookingRequest request, BookingResource r, CancellationToken ct) => r.RejectAsync(request, ct));
        bookings.MapPost("/CheckIn", [Permission("SPACE_BOOKINGS", "edit")] (CheckInRequest request, BookingResource r, CancellationToken ct) => r.CheckInAsync(request, ct));
        bookings.MapPost("/CheckInByCard", [Permission("SPACE_BOOKINGS", "edit")] (CheckInByCardRequest request, BookingResource r, CancellationToken ct) => r.CheckInByCardAsync(request, ct));
        bookings.MapPost("/{publicId:guid}/CheckOut", [Permission("SPACE_BOOKINGS", "edit")] (Guid publicId, BookingResource r, CancellationToken ct) => r.CheckOutAsync(publicId, ct));

        // Cấm đặt phòng — không có Add/Update chung (tạo qua AddAsync, chỉ gỡ qua LiftAsync).
        var bans = api.MapGroup("/bans").WithTags("Bans");
        bans.MapPost("/Search", [Permission("SPACE_BANS", "view")] (BanSearch search, BanResource r, CancellationToken ct) => r.SearchAsync(search, ct));
        bans.MapPost("/Add", [Permission("SPACE_BANS", "edit")] async (AddBanRequest request, BanResource r, CancellationToken ct) =>
            Results.Created((string?)null, await r.AddAsync(request, ct)));
        bans.MapPost("/{publicId:guid}/Lift", [Permission("SPACE_BANS", "edit")] (Guid publicId, BanResource r, CancellationToken ct) => r.LiftAsync(publicId, ct));

        var reports = api.MapGroup("/reports").WithTags("Reports");
        reports.MapPost("/Search", [Permission("SPACE_REPORT", "view")] (ReportRequest request, ReportResource r, CancellationToken ct) => r.BuildAsync(request, ct));
        reports.MapPost("/Export", [Permission("SPACE_REPORT", "view")] async (ReportRequest request, ReportResource r, CancellationToken ct) =>
            Results.File(await r.ExportAsync(request, ct), CrudExcel.ContentType, "bao-cao-dat-phong.xlsx"));

        // Thiết bị kiểm soát cửa — không có Add/Update chung (Add cần sinh khoá API, trả về 1 lần duy nhất).
        var devices = api.MapGroup("/access/devices").WithTags("AccessDevices");
        devices.MapPost("/Search", [Permission("SPACE_ACCESS", "view")] (CrudSearch search, AccessDeviceResource r, CancellationToken ct) => r.SearchAsync(search, ct));
        devices.MapGet("/GetById/{publicId:guid}", [Permission("SPACE_ACCESS", "view")] (Guid publicId, AccessDeviceResource r, CancellationToken ct) => r.GetAsync(publicId, ct));
        devices.MapPost("/Add", [Permission("SPACE_ACCESS", "add")] async (AccessDeviceRequest request, AccessDeviceResource r, CancellationToken ct) =>
        {
            var (device, key) = await r.AddWithKeyAsync(request, ct);
            return Results.Created((string?)null, new { device, key });
        });
        devices.MapPut("/Update/{publicId:guid}", [Permission("SPACE_ACCESS", "edit")] (Guid publicId, AccessDeviceRequest request, AccessDeviceResource r, CancellationToken ct) => r.UpdateAsync(publicId, request, ct));
        devices.MapPost("/{publicId:guid}/RegenerateKey", [Permission("SPACE_ACCESS", "edit")] (Guid publicId, AccessDeviceResource r, CancellationToken ct) => r.RegenerateKeyAsync(publicId, ct));
        devices.MapPut("/ChangeStatus", [Permission("SPACE_ACCESS", "edit")] async (ChangeStatusRequest request, AccessDeviceResource r, CancellationToken ct) =>
        {
            await r.ChangeStatusAsync(request, ct);
            return Results.NoContent();
        });
        devices.MapDelete("/Delete/{publicId:guid}", [Permission("SPACE_ACCESS", "delete")] async (Guid publicId, AccessDeviceResource r, CancellationToken ct) =>
        {
            await r.DeleteAsync(publicId, ct);
            return Results.NoContent();
        });
        api.MapPost("/access/unlock", [Permission("SPACE_ACCESS", "edit")] (UnlockRequest request, AccessDeviceResource r, CancellationToken ct) => r.UnlockAsync(request, ct)).WithTags("AccessDevices");

        api.MapCrud<AccessStaffCardResource>("/access/staff-cards", "SPACE_ACCESS").WithTags("AccessStaffCards");

        var scanLogs = api.MapGroup("/access/scan-logs").WithTags("AccessScanLogs");
        scanLogs.MapPost("/Search", [Permission("SPACE_ACCESS", "view")] (AccessScanLogSearch search, AccessScanLogResource r, CancellationToken ct) => r.SearchAsync(search, ct));

        return app;
    }
}
