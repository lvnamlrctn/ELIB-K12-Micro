using Elib.BuildingBlocks.Crud;
using Elib.Space.Application;

namespace Elib.Space.Api;

/// <summary>
/// Duyệt không gian/phòng ẩn danh (gateway: /api/opac/space/browse/** → /api/opac/**, license SPACE kiểm ở gateway, không kiểm
/// lại ở đây — đúng khuôn DigitalOpacEndpoints). Chỉ đọc; đặt phòng cần đăng nhập bạn đọc (xem ReaderSpaceEndpoints).
/// </summary>
public static class OpacSpaceEndpoints
{
    public static IEndpointRouteBuilder MapOpacSpaceEndpoints(this IEndpointRouteBuilder app)
    {
        var opac = app.MapGroup("/api/opac").WithTags("SpaceOpac").AllowAnonymous();

        opac.MapGet("/buildings", (BuildingResource r, CancellationToken ct) => r.SearchAllAsync(new CrudSearch(), ct));
        opac.MapGet("/floors", (long? buildingId, FloorResource r, CancellationToken ct) => r.SearchAllAsync(new FloorSearch { BuildingId = buildingId }, ct));
        opac.MapGet("/spaces", (long? floorId, SpaceResource r, CancellationToken ct) => r.SearchAllAsync(new SpaceSearch { FloorId = floorId }, ct));
        opac.MapGet("/spaces/{spaceId:long}/availability", (long spaceId, DateOnly date, BookingResource r, CancellationToken ct) => r.AvailabilityAsync(spaceId, date, ct));
        opac.MapGet("/floors/{floorId:long}/board", (long floorId, DateOnly date, BookingResource r, CancellationToken ct) => r.BoardAsync(floorId, date, null, ct));

        return app;
    }
}
