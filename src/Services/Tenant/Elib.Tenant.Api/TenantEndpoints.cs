using Elib.BuildingBlocks.Authorization;
using Elib.Tenant.Application;
using Elib.Tenant.Domain;

namespace Elib.Tenant.Api;

/// <summary>
/// Route trong service. Gateway thêm tiền tố: /api/admin/tenant/** (quản trị), /api/opac/tenant/features (OPAC).
/// /internal/** không được gateway công bố — chỉ gateway/service gọi trong cluster.
/// </summary>
public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/tenants").WithTags("Tenants").RequireAuthorization(new RequireSystemContextAttribute());

        admin.MapGet("/", (TenantQueries q, string? search, TenantState? status, int? page, int? pageSize, CancellationToken ct)
            => q.ListAsync(search, status, page ?? 1, pageSize ?? 20, ct));

        admin.MapGet("/{publicId:guid}", (Guid publicId, TenantQueries q, CancellationToken ct) => q.GetAsync(publicId, ct));

        admin.MapPost("/", async (CreateTenantRequest request, TenantAdminService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(request, ct);
            return Results.Created($"/api/tenants/{created.PublicId}", created);
        });

        admin.MapPut("/{publicId:guid}", (Guid publicId, UpdateTenantRequest request, TenantAdminService s, CancellationToken ct)
            => s.UpdateAsync(publicId, request, ct));

        admin.MapPut("/{publicId:guid}/branding", (Guid publicId, SetBrandingRequest request, TenantAdminService s, CancellationToken ct)
            => s.SetBrandingAsync(publicId, request, ct));

        admin.MapPost("/{publicId:guid}/suspend", (Guid publicId, SuspendRequest? request, TenantAdminService s, CancellationToken ct)
            => s.SuspendAsync(publicId, request?.Reason, ct));

        admin.MapPost("/{publicId:guid}/activate", (Guid publicId, TenantAdminService s, CancellationToken ct)
            => s.ActivateAsync(publicId, ct));

        admin.MapPut("/{publicId:guid}/licenses", (Guid publicId, SetLicensesRequest request, TenantAdminService s, CancellationToken ct)
            => s.SetLicensesAsync(publicId, request, ct));

        admin.MapPost("/{publicId:guid}/provisioning/retry", (Guid publicId, TenantAdminService s, CancellationToken ct)
            => s.RetryProvisioningAsync(publicId, ct));

        admin.MapPost("/{publicId:guid}/resync", (Guid publicId, TenantAdminService s, CancellationToken ct)
            => s.ResyncAsync(publicId, ct));

        app.MapGet("/api/modules", (TenantQueries q, CancellationToken ct) => q.ModulesAsync(ct))
            .WithTags("Modules").RequireAuthorization(new RequireSystemContextAttribute());

        // Ẩn danh được: OPAC gọi trước khi đăng nhập, đơn vị lấy từ host (header gateway có chữ ký).
        app.MapGet("/api/features", (TenantQueries q, CancellationToken ct) => q.FeaturesAsync(ct))
            .WithTags("Features").AllowAnonymous();

        // Gateway: /api/opac/tenant/manifest.json (ẩn danh, đơn vị theo host).
        app.MapGet("/api/manifest.json", async (TenantQueries q, CancellationToken ct) =>
                Results.Json(await q.ManifestAsync(ct), contentType: "application/manifest+json"))
            .WithTags("Features").AllowAnonymous();

        app.MapGet("/internal/tenants/by-host/{host}", (string host, TenantQueries q, CancellationToken ct) => q.ByHostAsync(host, ct))
            .WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());

        return app;
    }
}

public sealed record SuspendRequest(string? Reason);
