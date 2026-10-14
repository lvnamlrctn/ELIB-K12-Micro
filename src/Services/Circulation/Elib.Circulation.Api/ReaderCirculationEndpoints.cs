using System.Security.Claims;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Application;

namespace Elib.Circulation.Api;

/// <summary>Lịch sử mượn của chính mình (gateway: /api/opac/circulation/loans/History → /api/reader/loans/History).</summary>
public sealed record ReaderLoanHistoryRequest(string? State = null, DateOnly? From = null, DateOnly? To = null, int PageIndex = 1, int PageSize = 20);

/// <summary>Đặt mượn qua OPAC — theo MFN hoặc theo một số ĐKCB của biểu ghi.</summary>
public sealed record ReaderPlaceHoldRequest(long? Mfn = null, string? Barcode = null);

/// <summary>
/// Bạn đọc tự phục vụ qua OPAC (gateway: /api/opac/circulation/** → /api/reader/**, chỉ token bạn đọc, cần license CIRCULATION).
/// Mọi endpoint tự suy ra bạn đọc từ claim card_no/reader_id của token bạn đọc — không nhận số thẻ/id từ client, khác với các
/// endpoint ở <see cref="CirculationEndpoints"/> (cán bộ, tự nhập số thẻ của bạn đọc tại quầy).
/// </summary>
public static class ReaderCirculationEndpoints
{
    public static IEndpointRouteBuilder MapReaderCirculationEndpoints(this IEndpointRouteBuilder app)
    {
        var reader = app.MapGroup("/api/reader").WithTags("ReaderSelfService")
            .RequireAuthorization(new RequiresModuleAttribute(CirculationEndpoints.Module))
            .RequireAuthorization(new RequireReaderAttribute());

        // Sách đang mượn + hạn trả + chính sách gia hạn (ReaderPanel cũng kèm đặt mượn còn hiệu lực).
        reader.MapGet("/loans", async (ClaimsPrincipal principal, LoanResource r, CancellationToken ct) =>
        {
            var panel = await r.ReaderPanelAsync(new ReaderPanelRequest(CardNo(principal)), ct);
            return panel with { CurrentLoans = [.. panel.CurrentLoans.Select(l => l.ForReader())] };
        });

        reader.MapPost("/loans/{loanId:guid}/Renew", async (Guid loanId, ClaimsPrincipal principal, LoanResource r, CancellationToken ct) =>
            (await r.RenewAsync(new RenewRequest(loanId, SelfRenewReason), ct, ReaderId(principal))).ForReader());

        // Lịch sử mượn: luôn ép theo thẻ của chính mình, không nhận CardNo từ client.
        reader.MapPost("/loans/History", async (ReaderLoanHistoryRequest? request, ClaimsPrincipal principal, LoanResource r, CancellationToken ct) =>
        {
            var page = await r.SearchAsync(new LoanSearch
            {
                CardNo = CardNo(principal),
                State = request?.State,
                From = request?.From,
                To = request?.To,
                PageIndex = request?.PageIndex ?? 1,
                PageSize = request?.PageSize ?? 20,
            }, ct);
            return page with { Items = [.. page.Items.Select(l => l.ForReader())] };
        });

        // Đặt mượn còn hiệu lực của chính mình (chờ sách hoặc đang giữ).
        reader.MapGet("/holds", (ClaimsPrincipal principal, HoldResource r, CancellationToken ct) =>
            r.ActiveForAsync(ReaderId(principal), ct));

        reader.MapPost("/holds", async (ReaderPlaceHoldRequest request, ClaimsPrincipal principal, HoldResource r, CancellationToken ct) =>
            Results.Created((string?)null, await r.PlaceAsync(new PlaceHoldRequest(CardNo(principal), request?.Mfn, request?.Barcode), ct)));

        reader.MapPost("/holds/{holdId:guid}/Cancel", (Guid holdId, ClaimsPrincipal principal, HoldResource r, CancellationToken ct) =>
            r.CancelAsync(new CancelHoldRequest(holdId, SelfCancelReason), ct, ReaderId(principal)));

        return app;
    }

    private const string SelfRenewReason = "Bạn đọc tự gia hạn qua OPAC.";
    private const string SelfCancelReason = "Bạn đọc tự huỷ đặt mượn qua OPAC.";

    private static string CardNo(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ElibClaimTypes.CardNo) is { Length: > 0 } cardNo ? cardNo : throw Forbidden();

    private static Guid ReaderId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ElibClaimTypes.ReaderId), out var id) ? id : throw Forbidden();

    private static BusinessRuleException Forbidden() => new(ElibErrorCodes.PermissionDenied, "Token bạn đọc không hợp lệ.", 403);
}
