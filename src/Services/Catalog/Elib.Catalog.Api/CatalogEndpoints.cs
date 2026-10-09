using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;

namespace Elib.Catalog.Api;

/// <summary>
/// Biên mục (gateway: /api/admin/catalog/** → /api/**, cần license CATALOG). Mã quyền giữ như monolith:
/// CATALOG_BIBS (biểu ghi), BIB_TYPES (loại biểu ghi), WORKSHEETS (biểu mẫu biên mục).
/// </summary>
public static class CatalogEndpoints
{
    public const string Module = "CATALOG";

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        // Lớp thứ hai sau gateway: đơn vị chưa mua phân hệ Biên mục bị chặn ngay ở service.
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        var types = api.MapCrud<BibTypeResource>("/bib-types", "BIB_TYPES").WithTags("BibTypes");
        types.MapPost("/RestoreDefaults", [Permission("BIB_TYPES", "add")] async (BibTypeResource types, WorksheetResource worksheets, CancellationToken ct) =>
            Results.Ok(new { added = await types.AddMissingDefaultsAsync(ct), worksheets = await worksheets.AddMissingDefaultsAsync(ct) }));

        var worksheets = api.MapCrud<WorksheetResource>("/worksheets", "WORKSHEETS").WithTags("Worksheets");
        worksheets.MapGet("/GetByBibType/{bibTypeId:long}", [Permission("WORKSHEETS", "view")] (long bibTypeId, WorksheetResource r, CancellationToken ct)
            => r.ByBibTypeAsync(bibTypeId, ct));

        var bibs = api.MapCrud<BibResource>("/bibs", "CATALOG_BIBS").WithTags("Bibs");
        bibs.MapGet("/GetByMfn/{mfn:long}", [Permission("CATALOG_BIBS", "view")] (long mfn, BibResource r, CancellationToken ct) => r.GetByMfnAsync(mfn, ct));
        bibs.MapGet("/CheckIsbn", [PermissionAny("CATALOG_BIBS:view", "CATALOG_BIBS:add")] (string? isbn, Guid? excludePublicId, BibResource r, CancellationToken ct)
            => r.CheckIsbnAsync(isbn, excludePublicId, ct));

        // Từ điển MARC21 dùng chung — cán bộ nào dùng phân hệ Biên mục cũng đọc được (màn biên mục, biểu mẫu).
        api.MapGet("/marc21/fields", () => Marc21Definitions.Fields).WithTags("Marc21");
        return app;
    }
}
