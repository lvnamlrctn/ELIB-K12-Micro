using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;

namespace Elib.Circulation.Api;

/// <summary>
/// Lưu thông (gateway: /api/admin/circulation/** → /api/**, cần license CIRCULATION). Mã quyền giữ như monolith:
/// BORROW (mượn/trả), LOAN_HISTORY (lịch sử lưu thông), CIRC_POLICIES, CIRC_PLACES, FINES (phiếu phạt), FINE_REASONS (lý do phạt),
/// REQUEST_BOOKS (đặt mượn), C_PHOTO (sao chụp), CIRC_REPORT (báo cáo lưu thông).
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

        // Sao chụp: 8 endpoint chuẩn + tổng tiền, đánh dấu đã thu.
        var photos = api.MapCrud<PhotocopyResource>("/photocopies", "C_PHOTO").WithTags("Photocopies");
        photos.MapPost("/Totals", [Permission("C_PHOTO", "view")] (PhotocopySearch search, PhotocopyResource r, CancellationToken ct) => r.TotalsAsync(search, ct));
        photos.MapPost("/SetPaid", [Permission("C_PHOTO", "edit")] (PhotocopyPaidRequest request, PhotocopyResource r, CancellationToken ct) => r.SetPaidAsync(request, ct));

        // Báo cáo lưu thông: 11 loại như monolith, xem theo trang hoặc xuất Excel.
        var reports = api.MapGroup("/reports").WithTags("Reports");
        reports.MapPost("/Search", [Permission("CIRC_REPORT", "view")] (CirculationReportRequest request, CirculationReports r, CancellationToken ct)
            => r.BuildAsync(request, ct));
        reports.MapPost("/Export", [Permission("CIRC_REPORT", "view")] async (CirculationReportRequest request, CirculationReports r, CancellationToken ct) =>
        {
            var file = await r.ExportAsync(request, ct);
            return Results.File(file.Content, CrudExcel.ContentType, file.FileName);
        });

        // Đặt mượn: cán bộ đặt hộ / huỷ; không sửa (huỷ rồi đặt lại).
        var holds = api.MapGroup("/holds").WithTags("Holds");
        holds.MapPost("/Search", [PermissionAny("REQUEST_BOOKS:view", "BORROW:view")] (HoldSearch search, HoldResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        holds.MapGet("/GetById/{publicId:guid}", [PermissionAny("REQUEST_BOOKS:view", "BORROW:view")] (Guid publicId, HoldResource r, CancellationToken ct)
            => r.GetAsync(publicId, ct));
        holds.MapPost("/Place", [Permission("REQUEST_BOOKS", "add")] async (PlaceHoldRequest request, HoldResource r, CancellationToken ct)
            => Results.Created((string?)null, await r.PlaceAsync(request, ct)));
        holds.MapPost("/Cancel", [Permission("REQUEST_BOOKS", "edit")] (CancelHoldRequest request, HoldResource r, CancellationToken ct)
            => r.CancelAsync(request, ct));

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
        loans.MapPost("/Export", [PermissionAny("LOAN_HISTORY:view", "BORROW:view")] async (LoanSearch search, LoanResource r, CancellationToken ct)
            => Results.File(await r.ExportAsync(search, ct), CrudExcel.ContentType, "lich-su-luu-thong.xlsx"));
        loans.MapGet("/GetById/{publicId:guid}", [PermissionAny("LOAN_HISTORY:view", "BORROW:view")] (Guid publicId, LoanResource r, CancellationToken ct)
            => r.GetAsync(publicId, ct));
        return app;
    }
}
