using System.Security.Claims;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Space.Application;

namespace Elib.Space.Api;

/// <summary>
/// Bạn đọc tự phục vụ qua OPAC (gateway: /api/opac/space/** → /api/reader/**, chỉ token bạn đọc, cần license SPACE). Mọi
/// endpoint tự suy ra bạn đọc từ claim reader_id của token — không nhận id từ client, đúng khuôn ReaderDigitalEndpoints.
/// </summary>
public static class ReaderSpaceEndpoints
{
    public static IEndpointRouteBuilder MapReaderSpaceEndpoints(this IEndpointRouteBuilder app)
    {
        var reader = app.MapGroup("/api/reader").WithTags("SpaceReaderSelfService")
            .RequireAuthorization(new RequiresModuleAttribute(SpaceEndpoints.Module))
            .RequireAuthorization(new RequireReaderAttribute());

        reader.MapPost("/bookings", async (ReaderBookRequest request, ClaimsPrincipal principal, BookingResource r, CancellationToken ct) =>
            Results.Created((string?)null, (await r.BookAsync(ReaderId(principal), request, ct)).ForReader()));

        reader.MapGet("/bookings", async (ClaimsPrincipal principal, BookingResource r, CancellationToken ct) =>
            await r.MyBookingsAsync(ReaderId(principal), ct));

        reader.MapPost("/bookings/{publicId:guid}/CheckIn", async (Guid publicId, ClaimsPrincipal principal, BookingResource r, CancellationToken ct) =>
            (await r.SelfCheckInAsync(ReaderId(principal), publicId, ct)).ForReader());

        reader.MapPost("/bookings/{publicId:guid}/CheckOut", async (Guid publicId, ClaimsPrincipal principal, BookingResource r, CancellationToken ct) =>
            (await r.SelfCheckOutAsync(ReaderId(principal), publicId, ct)).ForReader());

        reader.MapDelete("/bookings/{publicId:guid}", async (Guid publicId, ClaimsPrincipal principal, BookingResource r, CancellationToken ct) =>
            (await r.CancelAsync(ReaderId(principal), publicId, ct)).ForReader());

        reader.MapGet("/members/{cardNo}", async (string cardNo, ClaimsPrincipal principal, BookingResource r, CancellationToken ct) =>
        {
            var member = await r.LookupMemberAsync(cardNo, ReaderId(principal), ct);
            return member is null ? Results.NotFound() : Results.Ok(member);
        });

        reader.MapGet("/floors/{floorId:long}/board", async (long floorId, DateOnly date, ClaimsPrincipal principal, BookingResource r, CancellationToken ct) =>
            await r.BoardAsync(floorId, date, ReaderId(principal), ct));

        return app;
    }

    private static Guid ReaderId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ElibClaimTypes.ReaderId), out var id) ? id : throw Forbidden();

    private static BusinessRuleException Forbidden() => new(ElibErrorCodes.PermissionDenied, "Token bạn đọc không hợp lệ.", 403);
}
