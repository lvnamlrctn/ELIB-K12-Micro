using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Application;
using Elib.Circulation.Application.Sip2;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Payment;
using Elib.Contracts.Events.Platform;

namespace Elib.Circulation.Api;

/// <summary>
/// Lưu thông (gateway: /api/admin/circulation/** → /api/**, cần license CIRCULATION). Mã quyền giữ như monolith:
/// BORROW (mượn/trả), LOAN_HISTORY (lịch sử lưu thông), CIRC_POLICIES, CIRC_PLACES, FINES (phiếu phạt), FINE_REASONS (lý do phạt),
/// REQUEST_BOOKS (đặt mượn), C_PHOTO (sao chụp), CIRC_REPORT (báo cáo lưu thông), SIP2LOG (trạm tự phục vụ SIP2 + nhật ký giao dịch),
/// CHECK_IN_OUT (vào/ra thư viện theo thẻ bạn đọc).
/// </summary>
public static class CirculationEndpoints
{
    public const string Module = "CIRCULATION";

    public static IEndpointRouteBuilder MapCirculationEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        // Thống kê toàn hệ thống — chỉ quản trị nền tảng (gateway /api/system/circulation/** → /api/system/**).
        app.MapPost("/api/system/stats/circulation", (PlatformStatsRequest request, PlatformCirculationStatsService s, CancellationToken ct) => s.BuildAsync(request, ct))
            .WithTags("PlatformStats").RequireAuthorization(new RequireSystemContextAttribute());

        api.MapCrud<CircPlaceResource>("/circ-places", "CIRC_PLACES").WithTags("CircPlaces");
        api.MapCrud<LoanPolicyResource>("/loan-policies", "CIRC_POLICIES").WithTags("LoanPolicies");
        api.MapCrud<FineReasonResource>("/fine-reasons", "FINE_REASONS").WithTags("FineReasons")
            .MapPost("/AddDefaults", [Permission("FINE_REASONS", "add")] async (FineReasonResource r, CancellationToken ct)
                => new { Added = await r.AddDefaultsAsync(ct) });

        // Trạm tự phục vụ SIP2: 8 endpoint chuẩn + nhật ký giao dịch.
        api.MapCrud<Sip2StationResource>("/sip2-stations", "SIP2LOG").WithTags("Sip2Stations")
            .MapPost("/Logs", [Permission("SIP2LOG", "view")] (Sip2LogSearch search, Sip2LogQueries q, CancellationToken ct) => q.SearchAsync(search, ct));

        // Nội bộ: search dựng chỉ mục lần đầu lấy các lượt đang mượn (không qua gateway, chỉ service token).
        app.MapGet("/internal/tenants/{tenantId:long}/loans/open", async (long tenantId, long? after, int? take, ITenantContext tenant, LoanResource r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
            {
                var (list, next) = await r.OpenStatesAsync(after ?? 0, take ?? 1000, ct);
                return new StatePage<LoanChanged>(list, next);
            }
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());

        // Nội bộ: payment dựng khoản thu lần đầu — các phiếu phạt còn nợ (chỉ service token).
        app.MapGet("/internal/tenants/{tenantId:long}/charges/open", async (long tenantId, string? kind, long? after, int? take, ITenantContext tenant,
            FineTicketResource fines, PhotocopyResource photos, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
            {
                var (list, next) = string.Equals(kind, "PHOTOCOPY", StringComparison.OrdinalIgnoreCase)
                    ? await photos.OpenChargesAsync(after ?? 0, take ?? 1000, ct)
                    : await fines.OpenChargesAsync(after ?? 0, take ?? 1000, ct);
                return new StatePage<ChargeIssued>(list, next);
            }
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());

        // Nội bộ: patron hỏi trước khi xoá loại bạn đọc (điểm lưu thông phục vụ + chính sách lưu thông theo loại).
        app.MapGet("/internal/tenants/{tenantId:long}/reader-types/{readerTypeId:long}/usage",
            async (long tenantId, long readerTypeId, ITenantContext tenant, CircPlaceResource r, CancellationToken ct) =>
            {
                using (tenant.Use(tenantId)) return await r.ReaderTypeUsageAsync(readerTypeId, ct);
            }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());

        // Sao chụp: 8 endpoint chuẩn + tổng tiền, đánh dấu đã thu.
        var photos = api.MapCrud<PhotocopyResource>("/photocopies", "C_PHOTO").WithTags("Photocopies");
        photos.MapPost("/Totals", [Permission("C_PHOTO", "view")] (PhotocopySearch search, PhotocopyResource r, CancellationToken ct) => r.TotalsAsync(search, ct));
        photos.MapPost("/SetPaid", [Permission("C_PHOTO", "edit")] (PhotocopyPaidRequest request, PhotocopyResource r, CancellationToken ct) => r.SetPaidAsync(request, ct));

        // Bảng điều khiển (tab Lưu thông): số liệu của đơn vị, cùng quyền với báo cáo lưu thông.
        api.MapGet("/dashboard", [Permission("CIRC_REPORT", "view")] (CirculationDashboardService s, CancellationToken ct) => s.BuildAsync(ct)).WithTags("Dashboard");

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
        fines.MapPut("/Save/{publicId:guid}", [Permission("FINES", "edit")] async (Guid publicId, SaveFineTicketRequest request, FineTicketResource r, CancellationToken ct)
            => Outcome(await r.SaveAsync(publicId, request, ct)));
        fines.MapPost("/Adjust/{publicId:guid}", [Permission("FINES", "edit")] async (Guid publicId, AdjustFinePaymentRequest request, FineTicketResource r, CancellationToken ct)
            => Outcome(await r.AdjustAsync(publicId, request, ct)));
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
        loans.MapPost("/ForceClose", [Permission("BORROW", "edit")] async (LoanActionRequest request, LoanResource r, CancellationToken ct)
            => Outcome(await r.ForceCloseAsync(request, ct)));
        loans.MapPost("/Reopen", [Permission("BORROW", "edit")] async (LoanActionRequest request, LoanResource r, CancellationToken ct)
            => Outcome(await r.ReopenAsync(request, ct)));
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

        // Vào/ra thư viện theo thẻ bạn đọc — không có Add/Update chung, chỉ Status/CheckIn/CheckOut ở cổng.
        var visits = api.MapGroup("/visits").WithTags("Visits");
        visits.MapPost("/Status", [Permission("CHECK_IN_OUT", "view")] (GateStatusRequest request, VisitResource r, CancellationToken ct)
            => r.StatusAsync(request, ct));
        visits.MapPost("/CheckIn", [Permission("CHECK_IN_OUT", "add")] (CheckInGateRequest request, VisitResource r, CancellationToken ct)
            => r.CheckInAsync(request, ct));
        visits.MapPost("/CheckOut", [Permission("CHECK_IN_OUT", "edit")] (CheckOutGateRequest request, VisitResource r, CancellationToken ct)
            => r.CheckOutAsync(request, ct));
        visits.MapPost("/Search", [Permission("CHECK_IN_OUT", "view")] (VisitSearch search, VisitResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        visits.MapGet("/GetById/{publicId:guid}", [Permission("CHECK_IN_OUT", "view")] (Guid publicId, VisitResource r, CancellationToken ct)
            => r.GetAsync(publicId, ct));
        visits.MapPost("/Export", [Permission("CHECK_IN_OUT", "view")] async (VisitSearch search, VisitResource r, CancellationToken ct)
            => Results.File(await r.ExportAsync(search, ct), CrudExcel.ContentType, "lich-su-vao-ra.xlsx"));
        visits.MapDelete("/Delete/{publicId:guid}", [Permission("CHECK_IN_OUT", "delete")] async (Guid publicId, VisitResource r, CancellationToken ct) =>
        {
            await r.DeleteAsync(publicId, ct);
            return Results.NoContent();
        });
        api.MapApprovals();
        return app;
    }

    /// <summary>200 khi đã áp dụng; 202 khi vượt thẩm quyền và chỉ mới tạo yêu cầu chờ duyệt (chưa đổi gì).</summary>
    private static IResult Outcome(FineSaveOutcome outcome) =>
        outcome.Pending is { } pending ? Results.Json(pending, statusCode: StatusCodes.Status202Accepted) : Results.Ok(outcome.Detail);

    private static IResult Outcome(LoanActionOutcome outcome) =>
        outcome.Pending is { } pending ? Results.Json(pending, statusCode: StatusCodes.Status202Accepted) : Results.Ok(outcome.Loan);
}
