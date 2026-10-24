using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Serials.Application;
using Elib.Serials.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Elib.Serials.Api;

/// <summary>
/// Báo - Tạp chí (gateway: /api/admin/serials/** → /api/**, cần license SERIALS). Mã quyền giữ như monolith: MAGAZINE_TYPES, FREQUENCIES, PATTERNS (danh mục),
/// SUBSCRIPTIONS (đăng ký), SERIAL_ISSUES (nhận kỳ), SERIAL_RECEIPTS (tìm phiếu nhận), cộng SERIAL_BINDING (đóng tập) và SERIAL_REPORT (báo cáo).
/// </summary>
public static class SerialsEndpoints
{
    public const string Module = "SERIALS";

    public static IEndpointRouteBuilder MapSerialsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<PublicationTypeResource>("/types", "MAGAZINE_TYPES").WithTags("Types");
        api.MapCrud<FrequencyResource>("/frequencies", "FREQUENCIES").WithTags("Frequencies");
        api.MapGet("/frequencies/Units", () => FrequencyUnit.Names.Select(u => new { code = u.Key, name = u.Value })).WithTags("Frequencies");
        api.MapCrud<PatternResource>("/patterns", "PATTERNS").WithTags("Patterns");

        MapSubscriptions(api.MapCrud<SubscriptionResource>("/subscriptions", "SUBSCRIPTIONS").WithTags("Subscriptions"));
        MapIssues(api.MapGroup("/issues").WithTags("Issues"));
        MapBindings(api.MapGroup("/bindings").WithTags("Bindings"));
        MapReports(api.MapGroup("/reports").WithTags("Reports"));

        // Chọn biểu ghi thư mục cho đăng ký; nhà cung cấp là tuỳ chọn (rỗng khi đơn vị không có phân hệ Bổ sung).
        api.MapPost("/bibs/Lookup", [PermissionAny("SUBSCRIPTIONS:view", "SERIAL_BINDING:view")] (CrudSearch search, BibSnapshots bibs, CancellationToken ct)
            => bibs.LookupAsync(search.Keyword, ct)).WithTags("Bibs");
        api.MapGet("/suppliers", [Permission("SUBSCRIPTIONS", "view")] (ISupplierSource suppliers, ITenantContext tenant, CancellationToken ct)
            => suppliers.ListAsync(tenant.RequireTenantId(), ct)).WithTags("Suppliers");
        api.MapGet("/dashboard", [PermissionAny("SUBSCRIPTIONS:view", "SERIAL_ISSUES:view")] (SerialsDashboardService s, CancellationToken ct) => s.BuildAsync(ct))
            .WithTags("Dashboard");
        return app;
    }

    private static void MapSubscriptions(RouteGroupBuilder g)
    {
        g.MapGet("/Statuses", () => SubscriptionStatus.Names.Select(s => new { code = s.Key, name = s.Value }));
        // Duyệt một chiều (khoá sửa/xoá); cần biểu ghi thư mục. Gắn biểu ghi cho đăng ký chưa duyệt.
        g.MapPost("/Approve/{publicId:guid}", [Permission("SUBSCRIPTIONS", "edit")] ([FromServices] SubscriptionResource r, Guid publicId, CancellationToken ct)
            => r.ApproveAsync(publicId, ct));
        g.MapPost("/LinkBib/{publicId:guid}/{bibPublicId:guid}", [Permission("SUBSCRIPTIONS", "edit")] ([FromServices] SubscriptionResource r, Guid publicId,
            Guid bibPublicId, CancellationToken ct) => r.LinkBibAsync(publicId, bibPublicId, ct));
    }

    private static void MapIssues(RouteGroupBuilder g)
    {
        g.MapGet("/Statuses", () => new[] { (IssueStatus.Planned, "Dự kiến"), (IssueStatus.Received, "Đã nhận"), (IssueStatus.Claimed, "Đã khiếu nại"), (IssueStatus.Missing, "Thiếu") }
            .Select(s => new { code = s.Item1, name = s.Item2 }));
        g.MapPost("/Search", [PermissionAny("SERIAL_ISSUES:view", "SERIAL_RECEIPTS:view")] (IssueService s, IssueSearch search, CancellationToken ct) => s.SearchAsync(search, ct));
        g.MapGet("/{publicId:guid}", [PermissionAny("SERIAL_ISSUES:view", "SERIAL_RECEIPTS:view")] (IssueService s, Guid publicId, CancellationToken ct) => s.GetAsync(publicId, ct));
        g.MapGet("/Statistics/{subscriptionPublicId:guid}", [Permission("SERIAL_ISSUES", "view")] (IssueService s, Guid subscriptionPublicId, CancellationToken ct)
            => s.StatisticsAsync(subscriptionPublicId, ct));
        g.MapPost("/Save", [Permission("SERIAL_ISSUES", "edit")] (IssueService s, IssueSaveRequest request, CancellationToken ct) => s.SaveAsync(request, ct));
        g.MapPost("/Receive", [Permission("SERIAL_ISSUES", "edit")] (IssueService s, IssueReceiveRequest request, CancellationToken ct) => s.ReceiveAsync(request, ct));
        g.MapPost("/Claim", [Permission("SERIAL_ISSUES", "edit")] (IssueService s, IssueClaimRequest request, CancellationToken ct) => s.ClaimAsync(request, ct));
        g.MapPost("/Predict", [Permission("SERIAL_ISSUES", "add")] (IssueService s, IssuePredictRequest request, CancellationToken ct) => s.PredictAsync(request, ct));
        g.MapPost("/Merge", [Permission("SERIAL_ISSUES", "edit")] (IssueService s, IssueMergeRequest request, CancellationToken ct) => s.MergeAsync(request, ct));
        g.MapDelete("/Delete/{publicId:guid}", [Permission("SERIAL_ISSUES", "delete")] async (IssueService s, Guid publicId, CancellationToken ct) =>
        {
            await s.DeleteAsync(publicId, ct);
            return Results.NoContent();
        });
    }

    private static void MapBindings(RouteGroupBuilder g)
    {
        g.MapPost("/Search", [Permission("SERIAL_BINDING", "view")] (BindingService s, BindingSearch search, CancellationToken ct) => s.SearchAsync(search, ct));
        g.MapGet("/GetById/{publicId:guid}", [Permission("SERIAL_BINDING", "view")] (BindingService s, Guid publicId, CancellationToken ct) => s.GetAsync(publicId, ct));
        g.MapPost("/Add", [Permission("SERIAL_BINDING", "add")] async (BindingService s, BindingSaveRequest request, CancellationToken ct)
            => Results.Created((string?)null, await s.SaveAsync(request with { PublicId = null }, ct)));
        g.MapPut("/Update/{publicId:guid}", [Permission("SERIAL_BINDING", "edit")] (BindingService s, Guid publicId, BindingSaveRequest request, CancellationToken ct)
            => s.SaveAsync(request with { PublicId = publicId }, ct));
        g.MapDelete("/Delete/{publicId:guid}", [Permission("SERIAL_BINDING", "delete")] async (BindingService s, Guid publicId, CancellationToken ct) =>
        {
            await s.DeleteAsync(publicId, ct);
            return Results.NoContent();
        });
    }

    private static IResult File(ExcelFile file) => Results.File(file.Content, CrudExcel.ContentType, file.FileName);

    private static void MapReports(RouteGroupBuilder g)
    {
        g.MapPost("/ReceivedSummary", [Permission("SERIAL_REPORT", "view")] ([FromServices] SerialReports r, SerialReportRequest request, CancellationToken ct)
            => r.ReceivedSummaryAsync(request, ct));
        g.MapPost("/ReceivedSummaryExport", [Permission("SERIAL_REPORT", "view")] async ([FromServices] SerialReports r, SerialReportRequest request, CancellationToken ct)
            => File(await r.ReceivedSummaryExportAsync(request, ct)));
        g.MapPost("/ReceivedDetail", [Permission("SERIAL_REPORT", "view")] ([FromServices] SerialReports r, SerialReportRequest request, CancellationToken ct)
            => r.ReceivedDetailAsync(request, ct));
        g.MapPost("/ReceivedDetailExport", [Permission("SERIAL_REPORT", "view")] async ([FromServices] SerialReports r, SerialReportRequest request, CancellationToken ct)
            => File(await r.ReceivedDetailExportAsync(request, ct)));
        g.MapPost("/MissingClaim", [Permission("SERIAL_REPORT", "view")] ([FromServices] SerialReports r, SerialReportRequest request, CancellationToken ct)
            => r.MissingClaimAsync(request, ct));
        g.MapPost("/MissingClaimExport", [Permission("SERIAL_REPORT", "view")] async ([FromServices] SerialReports r, SerialReportRequest request, CancellationToken ct)
            => File(await r.MissingClaimExportAsync(request, ct)));
    }
}
