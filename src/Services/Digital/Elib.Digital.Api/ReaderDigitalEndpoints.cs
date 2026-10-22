using System.Security.Claims;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Application;

namespace Elib.Digital.Api;

/// <summary>Đặt trước qua OPAC — theo PublicId của tài liệu số.</summary>
public sealed record ReaderReserveRequest(Guid EbookItemPublicId);

public sealed record ReaderBorrowRequest(Guid EbookItemPublicId);

/// <summary>
/// Bạn đọc tự phục vụ qua OPAC (gateway: /api/opac/digital/** → /api/reader/**, chỉ token bạn đọc, cần license DIGITAL). Mọi
/// endpoint tự suy ra bạn đọc từ claim reader_id của token — không nhận id từ client, khác với <see cref="DigitalEndpoints"/>
/// (cán bộ, chỉ tra cứu + thu hồi).
/// </summary>
public static class ReaderDigitalEndpoints
{
    public static IEndpointRouteBuilder MapReaderDigitalEndpoints(this IEndpointRouteBuilder app)
    {
        var reader = app.MapGroup("/api/reader").WithTags("ReaderSelfService")
            .RequireAuthorization(new RequiresModuleAttribute(DigitalEndpoints.Module))
            .RequireAuthorization(new RequireReaderAttribute());

        reader.MapGet("/loans", async (ClaimsPrincipal principal, EbookLoanResource r, CancellationToken ct) =>
            (await r.ActiveForAsync(ReaderId(principal), ct)).Select(l => l.ForReader()));

        reader.MapPost("/loans/Borrow", async (ReaderBorrowRequest request, ClaimsPrincipal principal, EbookLoanResource r, CancellationToken ct) =>
            Results.Created((string?)null, (await r.BorrowAsync(request.EbookItemPublicId, ReaderId(principal), ct)).ForReader()));

        reader.MapPost("/loans/{loanId:guid}/Return", async (Guid loanId, ClaimsPrincipal principal, EbookLoanResource r, CancellationToken ct) =>
            (await r.ReturnAsync(loanId, ct, ReaderId(principal))).ForReader());

        reader.MapGet("/reservations", async (ClaimsPrincipal principal, EbookReservationResource r, CancellationToken ct) =>
            (await r.ActiveForAsync(ReaderId(principal), ct)).Select(x => x.ForReader()));

        reader.MapPost("/reservations", async (ReaderReserveRequest request, ClaimsPrincipal principal, EbookReservationResource r, CancellationToken ct) =>
            Results.Created((string?)null, (await r.ReserveAsync(request.EbookItemPublicId, ReaderId(principal), ct)).ForReader()));

        reader.MapPost("/reservations/{reservationId:guid}/Cancel",
            async (Guid reservationId, ClaimsPrincipal principal, EbookReservationResource r, CancellationToken ct) =>
                (await r.CancelAsync(reservationId, ct, ReaderId(principal))).ForReader());

        // Trình đọc: nhịp đọc (lượt đọc + tiến độ) và tiến độ "đọc tiếp".
        reader.MapPut("/reading/progress", async (ReadBeatRequest request, ClaimsPrincipal principal, ReaderReading reading, CancellationToken ct) =>
            await reading.BeatAsync(ReaderId(principal), request, ct));
        reader.MapGet("/reading/progress/{ebookPublicId:guid}", async (Guid ebookPublicId, ClaimsPrincipal principal, ReaderReading reading, CancellationToken ct) =>
            await reading.ProgressAsync(ReaderId(principal), ebookPublicId, ct) is { } progress ? Results.Ok(progress) : Results.NoContent());

        reader.MapGet("/bookmarks/{ebookPublicId:guid}", async (Guid ebookPublicId, ClaimsPrincipal principal, ReaderBookmarks bookmarks, CancellationToken ct) =>
            await bookmarks.ListAsync(ReaderId(principal), ebookPublicId, ct));
        reader.MapPost("/bookmarks", async (BookmarkRequest request, ClaimsPrincipal principal, ReaderBookmarks bookmarks, CancellationToken ct) =>
            await bookmarks.SaveAsync(ReaderId(principal), request, ct));
        reader.MapDelete("/bookmarks/{bookmarkPublicId:guid}", async (Guid bookmarkPublicId, ClaimsPrincipal principal, ReaderBookmarks bookmarks, CancellationToken ct) =>
        {
            await bookmarks.DeleteAsync(ReaderId(principal), bookmarkPublicId, ct);
            return Results.NoContent();
        });

        reader.MapGet("/favorites", async (ClaimsPrincipal principal, ReaderFavorites favorites, CancellationToken ct) =>
            await favorites.MyListAsync(ReaderId(principal), ct));

        reader.MapPost("/favorites/{itemPublicId:guid}", async (Guid itemPublicId, ClaimsPrincipal principal, ReaderFavorites favorites, CancellationToken ct) =>
        {
            await favorites.AddAsync(itemPublicId, ReaderId(principal), ct);
            return Results.NoContent();
        });

        reader.MapDelete("/favorites/{itemPublicId:guid}", async (Guid itemPublicId, ClaimsPrincipal principal, ReaderFavorites favorites, CancellationToken ct) =>
        {
            await favorites.RemoveAsync(itemPublicId, ReaderId(principal), ct);
            return Results.NoContent();
        });

        // Nộp tài liệu — xin vé (digital xin hộ media) → trình duyệt PUT thẳng → hoàn tất (SubmitAsync tự gọi media complete).
        reader.MapPost("/document-submissions/upload-ticket", (SubmissionUploadRequest request, DocumentSubmissionResource r, CancellationToken ct) =>
            r.RequestUploadAsync(request, ct));
        reader.MapPost("/document-submissions", async (ReaderSubmitRequest request, ClaimsPrincipal principal, DocumentSubmissionResource r, CancellationToken ct) =>
            Results.Created((string?)null, (await r.SubmitAsync(ReaderId(principal), request, ct)).ForReader()));
        reader.MapGet("/document-submissions", async (ClaimsPrincipal principal, DocumentSubmissionResource r, CancellationToken ct) =>
            (await r.MyListAsync(ReaderId(principal), ct)).Select(x => x.ForReader()));

        return app;
    }

    private static Guid ReaderId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ElibClaimTypes.ReaderId), out var id) ? id : throw Forbidden();

    private static BusinessRuleException Forbidden() => new(ElibErrorCodes.PermissionDenied, "Token bạn đọc không hợp lệ.", 403);
}
