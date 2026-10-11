using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Platform;
using Microsoft.Extensions.Options;

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

        // File MARC (multipart, trường "file"): ISO2709 .mrc, MARCXML .xml hoặc text MARC hệ cũ — định dạng nhận theo nội dung.
        bibs.MapPost("/PreviewMarc", [PermissionAny("CATALOG_BIBS:add", "CATALOG_BIBS:edit")] async (IFormFile file, BibResource r, CancellationToken ct)
            => await r.PreviewMarcAsync(await ReadAsync(file, ct), file.FileName, ct)).DisableAntiforgery();
        bibs.MapPost("/ImportMarc", [Permission("CATALOG_BIBS", "add")] async (IFormFile file, long? bibTypeId, int? status, bool? skipDuplicates, bool? skipInvalid,
            BibResource r, CancellationToken ct) =>
        {
            var result = await r.ImportMarcAsync(await ReadAsync(file, ct), file.FileName,
                new MarcImportOptions(bibTypeId, status, skipDuplicates ?? true, skipInvalid ?? false), ct);
            return result.Rejected ? Results.Json(result, statusCode: StatusCodes.Status400BadRequest) : Results.Ok(result);
        }).DisableAntiforgery();
        // Ảnh bìa: đặt URL (ảnh đã upload qua media, purpose bib-cover, hoặc https ngoài) / tra theo ISBN (Google Books, Open Library).
        bibs.MapPut("/Cover", [Permission("CATALOG_BIBS", "edit")] (SetCoverRequest request, BibResource r, IOptions<CatalogOptions> options, CancellationToken ct)
            => r.SetCoverAsync(request, options.Value.MediaPublicPrefix, ct));
        bibs.MapGet("/LookupCover", [PermissionAny("CATALOG_BIBS:add", "CATALOG_BIBS:edit")] (string? isbn, ICoverLookup lookup, CancellationToken ct)
            => BibResource.LookupCoverAsync(isbn, lookup, ct));
        bibs.MapPost("/ExportMarc", [Permission("CATALOG_BIBS", "view")] async (MarcExportRequest request, BibResource r, CancellationToken ct) =>
        {
            var file = await r.ExportMarcAsync(request, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        // OPAC (gateway: /api/opac/catalog/** → /api/opac/**, công khai theo host đơn vị, license SEARCH ở gateway): xem MARC/ISBD và
        // tải biểu ghi (.mrc / MARCXML) — chỉ biểu ghi đang hiện trên OPAC.
        var opac = app.MapGroup("/api/opac/bibs").WithTags("Opac").AllowAnonymous();
        opac.MapGet("/{publicId:guid}/marc", (Guid publicId, BibResource r, CancellationToken ct) => r.OpacMarcAsync(publicId, ct));
        opac.MapGet("/{publicId:guid}/export", async (Guid publicId, string? format, BibResource r, CancellationToken ct) =>
        {
            var file = await r.OpacExportAsync(publicId, format, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        // Từ điển MARC21 dùng chung — cán bộ nào dùng phân hệ Biên mục cũng đọc được (màn biên mục, biểu mẫu).
        api.MapGet("/marc21/fields", () => Marc21Definitions.Fields).WithTags("Marc21");

        // Nội bộ: service khác lấy trạng thái biểu ghi khi bản sao của nó chưa có biểu ghi này (không qua gateway, chỉ service token).
        app.MapGet("/internal/tenants/{tenantId:long}/bibs/{mfn:long}", async (long tenantId, long mfn, ITenantContext tenant, BibResource r, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await r.CurrentStateAsync(mfn, ct) is { } state ? Results.Ok(state) : Results.NotFound();
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        app.MapGet("/internal/tenants/{tenantId:long}/bibs", async (long tenantId, long? after, int? take, ITenantContext tenant, BibResource r, CancellationToken ct) =>
        {
            var size = Math.Clamp(take ?? 500, 1, BibResource.MaxStatePage);
            using (tenant.Use(tenantId))
            {
                var items = await r.StatesAsync(after ?? 0, size, ct);
                return new StatePage<BibChanged>(items, items.Count == size ? items[^1].Mfn : null);
            }
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        return app;
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length is 0 or > BibResource.MaxMarcFileBytes)
            throw new BusinessRuleException("IMPORT_FILE_SIZE", $"File rỗng hoặc lớn hơn {BibResource.MaxMarcFileBytes / 1024 / 1024} MB.");
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
