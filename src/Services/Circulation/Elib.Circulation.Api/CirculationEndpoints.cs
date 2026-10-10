using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;

namespace Elib.Circulation.Api;

/// <summary>
/// Lưu thông (gateway: /api/admin/circulation/** → /api/**, cần license CIRCULATION). Mã quyền giữ như monolith:
/// BORROW (mượn/trả), LOAN_HISTORY (lịch sử lưu thông), CIRC_POLICIES, CIRC_PLACES, FINES (phiếu phạt), FINE_REASONS (lý do phạt).
/// </summary>
public static class CirculationEndpoints
{
    public const string Module = "CIRCULATION";

    public static IEndpointRouteBuilder MapCirculationEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<CircPlaceResource>("/circ-places", "CIRC_PLACES").WithTags("CircPlaces");
        api.MapCrud<LoanPolicyResource>("/loan-policies", "CIRC_POLICIES").WithTags("LoanPolicies");
        api.MapCrud<FineReasonResource>("/fine-reasons", "FINE_REASONS").WithTags("FineReasons")
            .MapPost("/AddDefaults", [Permission("FINE_REASONS", "add")] async (FineReasonResource r, CancellationToken ct)
                => new { Added = await r.AddDefaultsAsync(ct) });

        // Phiếu phạt: Add = phiếu thủ công; sửa qua Save (dòng phạt, giảm trừ, đã nộp, trạng thái).
        var fines = api.MapGroup("/fine-tickets").WithTags("FineTickets");
        fines.MapPost("/Search", [Permission("FINES", "view")] (FineTicketSearch search, FineTicketResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        fines.MapPost("/Totals", [Permission("FINES", "view")] (FineTicketSearch search, FineTicketResource r, CancellationToken ct)
            => r.TotalsAsync(search, ct));
        fines.MapGet("/Detail/{publicId:guid}", [Permission("FINES", "view")] (Guid publicId, FineTicketResource r, CancellationToken ct)
            => r.DetailAsync(publicId, ct));
        fines.MapPost("/Add", [Permission("FINES", "add")] async (FineTicketCreateRequest request, FineTicketResource r, CancellationToken ct)
            => Results.Created((string?)null, await r.AddAsync(request, ct)));
        fines.MapPost("/BuildForReader", [Permission("FINES", "add")] (BuildFineTicketRequest request, FineTicketResource r, CancellationToken ct)
            => r.BuildForReaderAsync(request, ct));
        fines.MapPut("/Save/{publicId:guid}", [Permission("FINES", "edit")] (Guid publicId, SaveFineTicketRequest request, FineTicketResource r, CancellationToken ct)
            => r.SaveAsync(publicId, request, ct));
        fines.MapDelete("/Delete/{publicId:guid}", [Permission("FINES", "delete")] async (Guid publicId, FineTicketResource r, CancellationToken ct) =>
        {
            await r.DeleteAsync(publicId, ct);
            return Results.NoContent();
        });

        // Lượt mượn không có Add/Update/Delete chung — chỉ các thao tác nghiệp vụ ở quầy.
        var loans = api.MapGroup("/loans").WithTags("Loans");
        loans.MapPost("/Reader", [Permission("BORROW", "view")] (ReaderPanelRequest request, LoanResource r, CancellationToken ct)
            => r.ReaderPanelAsync(request, ct));
        loans.MapPost("/Checkout", [Permission("BORROW", "add")] (CheckoutRequest request, LoanResource r, CancellationToken ct)
            => r.CheckoutAsync(request, ct));
        loans.MapPost("/Return", [Permission("BORROW", "edit")] (ReturnRequest request, LoanResource r, CancellationToken ct)
            => r.ReturnAsync(request, ct));
        loans.MapPost("/Renew", [Permission("BORROW", "edit")] (RenewRequest request, LoanResource r, CancellationToken ct)
            => r.RenewAsync(request, ct));
        loans.MapPost("/Note", [Permission("BORROW", "edit")] (LoanNoteRequest request, LoanResource r, CancellationToken ct)
            => r.NoteAsync(request, ct));
        loans.MapPost("/Search", [PermissionAny("LOAN_HISTORY:view", "BORROW:view")] (LoanSearch search, LoanResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        loans.MapGet("/GetById/{publicId:guid}", [PermissionAny("LOAN_HISTORY:view", "BORROW:view")] (Guid publicId, LoanResource r, CancellationToken ct)
            => r.GetAsync(publicId, ct));
        return app;
    }
}
