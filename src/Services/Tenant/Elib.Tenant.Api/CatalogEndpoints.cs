using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Tenant.Application;

namespace Elib.Tenant.Api;

/// <summary>
/// Danh mục của đơn vị (gateway: /api/admin/tenant/**), tham số công khai cho OPAC (gateway: /api/opac/tenant/parameters)
/// và tham số cho service khác (/internal). Mã quyền giữ như monolith để vai trò cũ dùng lại được.
/// </summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCrud<SystemParameterResource>("/api/system-parameters", "SYSTEM_PARAMS").WithTags("SystemParameters");
        app.MapCrud<CurrencyResource>("/api/currencies", "CURRENCIES").WithTags("Currencies");
        app.MapCrud<NationalityResource>("/api/nationalities", "NATIONALITIES").WithTags("Nationalities");
        app.MapCrud<EthnicityResource>("/api/ethnicities", "ETHNICS").WithTags("Ethnicities");
        app.MapCrud<AcademicTitleResource>("/api/academic-titles", "PROFS").WithTags("AcademicTitles");
        app.MapCrud<DegreeResource>("/api/degrees", "DEGREES").WithTags("Degrees");
        app.MapCrud<PositionResource>("/api/positions", "POSITIONS").WithTags("Positions");

        var orgs = app.MapCrud<OrgResource>("/api/orgs", "ORGS").WithTags("Orgs");
        orgs.MapPost("/GetTree", [Permission("ORGS", "view")] (OrgSearch search, OrgResource r, CancellationToken ct) => r.GetTreeAsync(search, ct));
        orgs.MapPut("/UpdateOrder", [Permission("ORGS", "edit")] async (UpdateOrderRequest request, OrgResource r, CancellationToken ct) =>
        {
            await r.UpdateOrderAsync(request, ct);
            return Results.NoContent();
        });
        orgs.MapPut("/Move/{publicId:guid}", [Permission("ORGS", "edit")] (Guid publicId, MoveOrgRequest request, OrgResource r, CancellationToken ct)
            => r.MoveAsync(publicId, request, ct));
        orgs.MapDelete("/DeleteWithChildren/{publicId:guid}", [Permission("ORGS", "delete")] async (Guid publicId, OrgResource r, CancellationToken ct) =>
        {
            await r.DeleteWithChildrenAsync(publicId, ct);
            return Results.NoContent();
        });

        // OPAC gọi trước khi đăng nhập — đơn vị lấy từ host (header gateway có chữ ký). Monolith: PublicSystemParameter/GetSystemPara.
        app.MapGet("/api/public/parameters", (string? codes, ParameterQueries q, CancellationToken ct)
            => q.PublicAsync((codes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries), ct))
            .WithTags("Public").AllowAnonymous();
        app.MapGet("/api/public/parameters/{code}", async (string code, ParameterQueries q, CancellationToken ct) =>
            (await q.PublicAsync([code], ct)) is [var found] ? Results.Ok(found) : Results.NotFound())
            .WithTags("Public").AllowAnonymous();

        app.MapGet("/internal/tenants/{tenantId:long}/parameters", (long tenantId, string? service, ParameterQueries q, CancellationToken ct)
            => q.EffectiveAsync(tenantId, service, ct))
            .WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());

        app.MapPost("/api/tenants/{publicId:guid}/defaults", async (Guid publicId, TenantAdminService s, CancellationToken ct)
            => Results.Ok(new { added = await s.SeedDefaultsAsync(publicId, ct) }))
            .WithTags("Tenants").RequireAuthorization(new RequireSystemContextAttribute());

        return app;
    }
}
