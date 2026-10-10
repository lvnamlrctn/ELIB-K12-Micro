using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;

namespace Elib.Circulation.Api;

/// <summary>
/// Lưu thông (gateway: /api/admin/circulation/** → /api/**, cần license CIRCULATION). Mã quyền giữ như monolith:
/// BORROW (mượn/trả), LOAN_HISTORY (lịch sử lưu thông), CIRC_POLICIES, CIRC_PLACES.
/// </summary>
public static class CirculationEndpoints
{
    public const string Module = "CIRCULATION";

    public static IEndpointRouteBuilder MapCirculationEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<CircPlaceResource>("/circ-places", "CIRC_PLACES").WithTags("CircPlaces");
        api.MapCrud<LoanPolicyResource>("/loan-policies", "CIRC_POLICIES").WithTags("LoanPolicies");

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
