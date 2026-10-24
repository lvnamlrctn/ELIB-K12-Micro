using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Platform;
using Elib.Holdings.Application;
using Elib.Holdings.Domain;

namespace Elib.Holdings.Api;

/// <summary>
/// Kho và bản sách (gateway: /api/admin/holdings/** → /api/**, cần license HOLDINGS). Mã quyền giữ như monolith:
/// STORE_TYPES, STORES; đăng ký/sửa/xoá ĐKCB theo CATALOG_BIBS (như CatalogueBookController); tìm tài liệu DOC_SEARCH;
/// xếp giá MAP_SHELVING. Kiểm kê, mất, thanh lý, xuất kho, điều chuyển, giao nhận, thống kê: <see cref="StoreOperationEndpoints"/>.
/// </summary>
public static class HoldingsEndpoints
{
    public const string Module = "HOLDINGS";

    public static IEndpointRouteBuilder MapHoldingsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        // Thống kê toàn hệ thống — chỉ quản trị nền tảng (gateway /api/system/holdings/** → /api/system/**).
        app.MapPost("/api/system/stats/items", (PlatformStatsRequest request, PlatformItemStatsService s, CancellationToken ct) => s.ItemsAsync(request, ct))
            .WithTags("PlatformStats").RequireAuthorization(new RequireSystemContextAttribute());

        // Bảng điều khiển (tab Bổ sung & Kho): số liệu kho in của đơn vị, cùng quyền với báo cáo sách trong kho.
        api.MapGet("/dashboard", [Permission("STORE_BOOK_REPORT", "view")] (HoldingsDashboardService s, CancellationToken ct) => s.BuildAsync(ct)).WithTags("Dashboard");

        api.MapCrud<StoreTypeResource>("/store-types", "STORE_TYPES").WithTags("StoreTypes");
        api.MapCrud<StoreResource>("/stores", "STORES").WithTags("Stores");

        var items = api.MapCrud<ItemResource>("/items", "CATALOG_BIBS").WithTags("Items");
        // Màn "Tìm kiếm tài liệu" và "Xếp giá" tìm bản sách bằng quyền riêng của màn đó.
        items.MapPost("/Lookup", [PermissionAny("CATALOG_BIBS:view", "DOC_SEARCH:view", "MAP_SHELVING:view", "LOST_BOOKS:view", "LIQUIDATES:view", "RE_REGISTER:view")] (ItemSearch search, ItemResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        items.MapPost("/Register", [Permission("CATALOG_BIBS", "edit")] (RegisterItemsRequest request, ItemResource r, CancellationToken ct)
            => r.RegisterAsync(request, ct));
        // Số ĐKCB tiếp theo: cán bộ biên mục, hoặc cán bộ bổ sung đăng ký ĐKCB theo phiếu nhận.
        items.MapGet("/NextBarcode", [PermissionAny("CATALOG_BIBS:edit", "AB_RECEIPTS:edit")] (string? prefix, int? digits, ItemResource r, CancellationToken ct)
            => r.NextAsync(prefix, digits ?? 6, ct));
        items.MapPost("/Shelve", [Permission("MAP_SHELVING", "edit")] (ShelveRequest request, ItemResource r, CancellationToken ct)
            => r.ShelveAsync(request, ct));
        // Xếp giá trực quan và xếp giá lại (ELIB-LRC: StoreMapShelving): bản sách + gợi ý theo DDC, sơ đồ giá của kho, xếp vào giá/ngăn.
        var shelving = api.MapGroup("/map-shelving").WithTags("MapShelving");
        shelving.MapPost("/Search", [Permission("MAP_SHELVING", "view")] (MapShelvingSearch search, MapShelvingResource r, CancellationToken ct) => r.SearchAsync(search, ct));
        shelving.MapGet("/Shelves", [Permission("MAP_SHELVING", "view")] (long storeId, MapShelvingResource r, CancellationToken ct) => r.ShelvesAsync(storeId, ct));
        shelving.MapPost("/Place", [Permission("MAP_SHELVING", "edit")] (PlaceRequest request, MapShelvingResource r, CancellationToken ct) => r.PlaceAsync(request, ct));
        // Lịch sử di chuyển của một bản sách (xếp giá, xếp giá lại, chuyển kho, đăng ký lại mã vạch).
        items.MapGet("/Movements/{publicId:guid}", [PermissionAny("CATALOG_BIBS:view", "DOC_SEARCH:view", "MAP_SHELVING:view", "LOST_BOOKS:view")] (Guid publicId, ItemMovements m, CancellationToken ct)
            => m.ListAsync(publicId, ct));
        // In nhãn mã vạch ĐKCB / nhãn gáy (ELIB-LRC: In mã vạch, In nhãn môn loại) — cán bộ biên mục in ngay khi đăng ký cá biệt.
        items.MapPost("/Labels", [PermissionAny("PRINT_LABEL:view", "CATALOG_BIBS:view")] (ItemLabelRequest request, ItemResource r, CancellationToken ct)
            => r.LabelsAsync(request, ct));
        // Nội bộ: circulation lấy trạng thái bản sách khi bản sao chưa có (không qua gateway, chỉ service token).
        app.MapGet("/internal/tenants/{tenantId:long}/items/by-barcode/{barcode}", async (long tenantId, string barcode, ITenantContext tenant, ItemResource r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.CurrentStateAsync(barcode, ct) is { } state ? Results.Ok(state) : Results.NotFound();
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        app.MapGet("/internal/tenants/{tenantId:long}/items", async (long tenantId, long? after, int? take, ITenantContext tenant, ItemResource r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
            {
                var (list, next) = await r.StatesAsync(after ?? 0, take ?? 1000, ct);
                return new StatePage<ItemChanged>(list, next);
            }
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        // Nội bộ: acquisition đăng ký / liệt kê / xoá ĐKCB theo phiếu nhận (quyền AB_RECEIPTS và khoá phiếu kiểm tra ở acquisition).
        // Người thao tác thật (acquisition gửi kèm) thay tài khoản service trong cột audit, event và nhật ký — filter chỉ chạy sau khi đã qua RequireServiceCaller.
        var receiptItems = app.MapGroup("/internal/tenants/{tenantId:long}/receipt-items").WithTags("Internal")
            .RequireAuthorization(new RequireServiceCallerAttribute()).AddEndpointFilter<ActingAsFilter>();
        // Báo cáo bổ sung (acquisition lọc phiếu nhận, holdings trả bản sách): trang bản sách theo số ĐKCB và số bản theo kho.
        receiptItems.MapPost("/Report", async (long tenantId, ReceiptItemReportRequest request, ITenantContext tenant, ReceiptItemReports r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.PageAsync(request, ct);
        });
        receiptItems.MapGet("/Stores", async (long tenantId, ITenantContext tenant, ReceiptItemReports r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.StoresAsync(ct);
        });
        receiptItems.MapPost("/StoreCounts", async (long tenantId, ReceiptItemReportRequest request, ITenantContext tenant, ReceiptItemReports r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.StoreCountsAsync(request, ct);
        });
        receiptItems.MapPost("/Register", async (long tenantId, ReceiptItemsRequest request, ITenantContext tenant, ReceiptItems r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.RegisterAsync(request, ct);
        });
        receiptItems.MapGet("/{receiptPublicId:guid}", async (long tenantId, Guid receiptPublicId, ITenantContext tenant, ReceiptItems r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.ListAsync(receiptPublicId, ct);
        });
        receiptItems.MapDelete("/{receiptPublicId:guid}/{itemPublicId:guid}", async (long tenantId, Guid receiptPublicId, Guid itemPublicId, ITenantContext tenant,
            ReceiptItems r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                await r.RemoveAsync(receiptPublicId, itemPublicId, ct);
            return Results.NoContent();
        });
        // Nội bộ: serials tạo / gỡ ĐKCB của tập đóng báo-tạp chí (quyền SERIAL_BINDING kiểm tra ở serials). Idempotent theo mã tập đóng.
        var serialItems = app.MapGroup("/internal/tenants/{tenantId:long}/serial-items").WithTags("Internal")
            .RequireAuthorization(new RequireServiceCallerAttribute()).AddEndpointFilter<ActingAsFilter>();
        serialItems.MapPost("/Register", async (long tenantId, SerialItemRegisterRequest request, ITenantContext tenant, SerialItems r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.RegisterAsync(request, ct);
        });
        serialItems.MapDelete("/{bindingPublicId:guid}", async (long tenantId, Guid bindingPublicId, ITenantContext tenant, SerialItems r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                await r.RemoveAsync(bindingPublicId, ct);
            return Results.NoContent();
        });
        items.MapGet("/Statuses", () => ItemStatus.Names.Select(s => new { code = s.Key, name = s.Value }));
        api.MapStoreOperationEndpoints();
        api.MapApprovals();
        return app;
    }
}
