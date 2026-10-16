using Elib.Acquisition.Application;
using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Microsoft.AspNetCore.Mvc;

namespace Elib.Acquisition.Api;

/// <summary>
/// Bổ sung (gateway: /api/admin/acquisition/** → /api/**, cần license ACQUISITION). Mã quyền giữ như monolith: AB_SOURCES, SUPPLIERS,
/// BUDGETS, FUNDS (danh mục), AB_ORDERS (đơn đặt), AB_RECEIPTS (phiếu nhận + đăng ký ĐKCB theo phiếu).
/// </summary>
public static class AcquisitionEndpoints
{
    public const string Module = "ACQUISITION";

    public static IEndpointRouteBuilder MapAcquisitionEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<SourceResource>("/sources", "AB_SOURCES").WithTags("Sources");
        api.MapCrud<SupplierResource>("/suppliers", "SUPPLIERS").WithTags("Suppliers");
        api.MapCrud<BudgetResource>("/budgets", "BUDGETS").WithTags("Budgets");
        api.MapCrud<FundResource>("/funds", "FUNDS").WithTags("Funds");

        // Ô chọn nguồn / nhà cung cấp / kinh phí / quỹ và tìm biểu ghi của màn đơn đặt, phiếu nhận.
        api.MapGet("/options", [PermissionAny("AB_ORDERS:view", "AB_RECEIPTS:view")] (AcquisitionOptionsQuery q, CancellationToken ct) => q.GetAsync(ct))
            .WithTags("Options");
        api.MapPost("/bibs/Lookup", [PermissionAny("AB_ORDERS:view", "AB_RECEIPTS:view")] (CrudSearch search, BibSnapshots bibs, CancellationToken ct)
            => bibs.LookupAsync(search.Keyword, ct)).WithTags("Bibs");

        MapOrders(api.MapCrud<PurchaseOrderResource>("/orders", "AB_ORDERS").WithTags("Orders"));
        MapReceipts(api.MapCrud<ReceiptResource>("/receipts", "AB_RECEIPTS").WithTags("Receipts"));
        MapReports(api.MapGroup("/reports").WithTags("Reports"));
        return app;
    }

    /// <summary>Báo cáo bổ sung + tra cứu đơn nhận theo dòng (monolith: AcquisitionReportController, LookupLines) — quyền xem phiếu nhận.</summary>
    private static void MapReports(RouteGroupBuilder g)
    {
        g.MapPost("/AcquisitionList", [Permission("AB_RECEIPTS", "view")] ([FromServices] AcquisitionReports r, AcquisitionReportRequest request, CancellationToken ct)
            => r.ListAsync(request, ct));
        g.MapPost("/AcquisitionListExport", [Permission("AB_RECEIPTS", "view")] async ([FromServices] AcquisitionReports r, AcquisitionReportRequest request,
            CancellationToken ct) => File(await r.ListExportAsync(request, ct)));
        g.MapPost("/LookupLines", [Permission("AB_RECEIPTS", "view")] ([FromServices] AcquisitionReports r, AcquisitionReportRequest request, CancellationToken ct)
            => r.LookupLinesAsync(request, ct));
        g.MapPost("/LookupLinesExport", [Permission("AB_RECEIPTS", "view")] async ([FromServices] AcquisitionReports r, AcquisitionReportRequest request,
            CancellationToken ct) => File(await r.LookupLinesExportAsync(request, ct)));
        g.MapPost("/StoreAllocation", [Permission("AB_RECEIPTS", "view")] ([FromServices] AcquisitionReports r, AcquisitionReportRequest request, CancellationToken ct)
            => r.StoreAllocationAsync(request, ct));
        g.MapPost("/StoreAllocationExport", [Permission("AB_RECEIPTS", "view")] async ([FromServices] AcquisitionReports r, AcquisitionReportRequest request,
            CancellationToken ct) => File(await r.StoreAllocationExportAsync(request, ct)));
        g.MapPost("/AccessionRegister", [Permission("AB_RECEIPTS", "view")] ([FromServices] AcquisitionReports r, AcquisitionReportRequest request, CancellationToken ct)
            => r.AccessionRegisterAsync(request, ct));
        g.MapPost("/AccessionRegisterExport", [Permission("AB_RECEIPTS", "view")] async ([FromServices] AcquisitionReports r, AcquisitionReportRequest request,
            CancellationToken ct) => File(await r.AccessionRegisterExportAsync(request, ct)));
        g.MapPost("/NewBooks", [Permission("AB_RECEIPTS", "view")] ([FromServices] AcquisitionReports r, AcquisitionReportRequest request, CancellationToken ct)
            => r.NewBooksAsync(request, ct));
        g.MapPost("/NewBooksExport", [Permission("AB_RECEIPTS", "view")] async ([FromServices] AcquisitionReports r, AcquisitionReportRequest request,
            CancellationToken ct) => File(await r.NewBooksExportAsync(request, ct)));
    }

    private static IResult File(ExcelFile file) => Results.File(file.Content, CrudExcel.ContentType, file.FileName);

    private static void MapOrders(RouteGroupBuilder g)
    {
        g.MapGet("/Statuses", () => OrderStatus.Names.Select(s => new { code = s.Key, name = s.Value }));
        g.MapGet("/PaymentMethods", () => PaymentMethod.Names.Select(s => new { code = s.Key, name = s.Value }));
        g.MapGet("/Lines/{publicId:guid}", [Permission("AB_ORDERS", "view")] ([FromServices] PurchaseOrderResource r, Guid publicId, CancellationToken ct)
            => r.LinesAsync(publicId, ct));
        g.MapPost("/SaveLine/{publicId:guid}", [Permission("AB_ORDERS", "edit")] ([FromServices] PurchaseOrderResource r, Guid publicId, OrderLineRequest request,
            CancellationToken ct) => r.SaveLineAsync(publicId, request, ct));
        g.MapDelete("/Lines/{linePublicId:guid}", [Permission("AB_ORDERS", "edit")] async ([FromServices] PurchaseOrderResource r, Guid linePublicId, CancellationToken ct) =>
        {
            await r.RemoveLineAsync(linePublicId, ct);
            return Results.NoContent();
        });
    }

    private static void MapReceipts(RouteGroupBuilder g)
    {
        g.MapGet("/PaymentStatuses", () => PaymentStatus.Names.Select(s => new { code = s.Key, name = s.Value }));
        g.MapGet("/PaymentMethods", () => PaymentMethod.Names.Select(s => new { code = s.Key, name = s.Value }));
        g.MapGet("/Lines/{publicId:guid}", [Permission("AB_RECEIPTS", "view")] ([FromServices] ReceiptResource r, Guid publicId, CancellationToken ct)
            => r.LinesAsync(publicId, ct));
        // Chọn từ đơn đặt: dòng đặt còn chờ nhận (cán bộ phiếu nhận không cần quyền AB_ORDERS).
        g.MapPost("/Receivable", [Permission("AB_RECEIPTS", "view")] ([FromServices] PurchaseOrderResource r, ReceivableSearch search, CancellationToken ct)
            => r.ReceivableAsync(search, ct));
        g.MapPost("/SaveLine/{publicId:guid}", [Permission("AB_RECEIPTS", "edit")] ([FromServices] ReceiptResource r, Guid publicId, ReceiptLineRequest request,
            CancellationToken ct) => r.SaveLineAsync(publicId, request, ct));
        g.MapDelete("/Lines/{linePublicId:guid}", [Permission("AB_RECEIPTS", "edit")] async ([FromServices] ReceiptResource r, Guid linePublicId, CancellationToken ct) =>
        {
            await r.RemoveLineAsync(linePublicId, ct);
            return Results.NoContent();
        });
        g.MapPost("/RegisterItems/{linePublicId:guid}", [Permission("AB_RECEIPTS", "edit")] ([FromServices] ReceiptResource r, Guid linePublicId,
            RegisterReceiptItemsRequest request, CancellationToken ct) => r.RegisterItemsAsync(linePublicId, request, ct));
        g.MapDelete("/Items/{publicId:guid}/{itemPublicId:guid}", [Permission("AB_RECEIPTS", "delete")] async ([FromServices] ReceiptResource r, Guid publicId,
            Guid itemPublicId, CancellationToken ct) =>
        {
            await r.RemoveItemAsync(publicId, itemPublicId, ct);
            return Results.NoContent();
        });
        g.MapPost("/Complete/{publicId:guid}", [Permission("AB_RECEIPTS", "edit")] ([FromServices] ReceiptResource r, Guid publicId, CancellationToken ct)
            => r.CompleteAsync(publicId, ct));
        // Chốt thanh toán sau khi phiếu đã hoàn tất: chỉ đổi trạng thái / phương thức thanh toán, nên quyền sửa là đủ.
        g.MapPost("/Payment/{publicId:guid}", [Permission("AB_RECEIPTS", "edit")] ([FromServices] ReceiptResource r, Guid publicId, ReceiptPaymentRequest request,
            CancellationToken ct) => r.SetPaymentAsync(publicId, request, ct));
        // Mở lại phiếu đã hoàn tất: quyền xoá (monolith chỉ quản trị sửa được đơn đã hoàn tất).
        g.MapPost("/Reopen/{publicId:guid}", [Permission("AB_RECEIPTS", "delete")] ([FromServices] ReceiptResource r, Guid publicId, CancellationToken ct)
            => r.ReopenAsync(publicId, ct));
    }
}
