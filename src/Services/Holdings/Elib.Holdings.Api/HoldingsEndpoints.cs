using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Holdings.Application;
using Elib.Holdings.Domain;

namespace Elib.Holdings.Api;

/// <summary>
/// Kho và bản sách (gateway: /api/admin/holdings/** → /api/**, cần license HOLDINGS). Mã quyền giữ như monolith:
/// STORE_TYPES, STORES; đăng ký/sửa/xoá ĐKCB theo CATALOG_BIBS (như CatalogueBookController); tìm tài liệu DOC_SEARCH;
/// xếp giá MAP_SHELVING.
/// </summary>
public static class HoldingsEndpoints
{
    public const string Module = "HOLDINGS";

    public static IEndpointRouteBuilder MapHoldingsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<StoreTypeResource>("/store-types", "STORE_TYPES").WithTags("StoreTypes");
        api.MapCrud<StoreResource>("/stores", "STORES").WithTags("Stores");

        var items = api.MapCrud<ItemResource>("/items", "CATALOG_BIBS").WithTags("Items");
        // Màn "Tìm kiếm tài liệu" và "Xếp giá" tìm bản sách bằng quyền riêng của màn đó.
        items.MapPost("/Lookup", [PermissionAny("CATALOG_BIBS:view", "DOC_SEARCH:view", "MAP_SHELVING:view")] (ItemSearch search, ItemResource r, CancellationToken ct)
            => r.SearchAsync(search, ct));
        items.MapPost("/Register", [Permission("CATALOG_BIBS", "edit")] (RegisterItemsRequest request, ItemResource r, CancellationToken ct)
            => r.RegisterAsync(request, ct));
        items.MapGet("/NextBarcode", [Permission("CATALOG_BIBS", "edit")] (string? prefix, int? digits, ItemResource r, CancellationToken ct)
            => r.NextAsync(prefix, digits ?? 6, ct));
        items.MapPost("/Shelve", [Permission("MAP_SHELVING", "edit")] (ShelveRequest request, ItemResource r, CancellationToken ct)
            => r.ShelveAsync(request, ct));
        // Nội bộ: circulation lấy trạng thái bản sách khi bản sao chưa có (không qua gateway, chỉ service token).
        app.MapGet("/internal/tenants/{tenantId:long}/items/by-barcode/{barcode}", async (long tenantId, string barcode, ITenantContext tenant, ItemResource r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.CurrentStateAsync(barcode, ct) is { } state ? Results.Ok(state) : Results.NotFound();
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        items.MapGet("/Statuses", () => ItemStatus.Names.Select(s => new { code = s.Key, name = s.Value }));
        return app;
    }
}
