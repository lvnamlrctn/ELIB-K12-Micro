using System.Security.Claims;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Tenancy;
using Elib.Identity.Application;

namespace Elib.Identity.Api;

/// <summary>API quản trị tài khoản/vai trò. Gateway công bố dưới /api/admin/identity/**.</summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapIdentityAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (ClaimsPrincipal principal, ICurrentActor actor, ITenantContext tenant, SignInService signIn,
            ImpersonationService impersonation, PermissionQueries permissions, CancellationToken ct) =>
        {
            var tenantId = tenant.IsSystem ? null : tenant.TenantId;
            if (ElibPrincipals.ImpersonationExpiry(principal) is { } expiresAt)
            {
                // Đóng vai đơn vị: quyền "xem" mọi chức năng ("*:view"), không đổi được mật khẩu, không ghi gì.
                return actor.Id is { } adminId && tenantId is { } viewed && await impersonation.ReloadAsync(viewed, adminId, ct) is { } admin
                    ? Results.Ok(new MeDto(admin.UserId, viewed, admin.UserName, admin.FullName, false, ["*:view"], ReadOnly: true, ExpiresAt: expiresAt))
                    : Results.Unauthorized();
            }
            if (actor.Id is not { } userId || await signIn.ReloadAsync(tenantId, userId, ct) is not { } who) return Results.Unauthorized();
            var granted = await permissions.GetEffectiveAsync(tenantId, userId, ct);
            return Results.Ok(new MeDto(who.UserId, who.TenantId, who.UserName, who.FullName, who.MustChangePassword, granted.Order(StringComparer.Ordinal).ToList()));
        }).RequireAuthorization().WithTags("Me");

        app.MapPost("/api/me/password", async (ChangePasswordRequest request, ClaimsPrincipal principal, ICurrentActor actor, ITenantContext tenant,
            SignInService signIn, CancellationToken ct) =>
        {
            if (actor.Id is not { } userId || actor.Kind != ElibSubjectTypes.Staff || principal.FindFirst(ElibClaimTypes.Impersonation) is not null)
                return Results.Forbid();
            await signIn.ChangePasswordAsync(tenant.IsSystem ? null : tenant.TenantId, userId, request.CurrentPassword, request.NewPassword, ct);
            return Results.NoContent();
        }).RequireAuthorization().WithTags("Me");

        var users = app.MapGroup("/api/users").WithTags("Users");
        users.MapGet("/", [Permission("USER", "view")] (UserAdminService s, string? search, int? page, int? pageSize, CancellationToken ct)
            => s.ListAsync(search, page ?? 1, pageSize ?? 20, ct));
        users.MapGet("/{publicId:guid}", [Permission("USER", "view")] (Guid publicId, UserAdminService s, CancellationToken ct) => s.GetAsync(publicId, ct));
        users.MapPost("/", [Permission("USER", "add")] async (CreateUserRequest request, UserAdminService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(request, ct);
            return Results.Created($"/api/users/{created.PublicId}", created);
        });
        users.MapPut("/{publicId:guid}", [Permission("USER", "edit")] (Guid publicId, UpdateUserRequest request, UserAdminService s, CancellationToken ct)
            => s.UpdateAsync(publicId, request, ct));
        users.MapPut("/{publicId:guid}/roles", [Permission("USER", "edit")] (Guid publicId, SetUserRolesRequest request, UserAdminService s, CancellationToken ct)
            => s.SetRolesAsync(publicId, request, ct));
        users.MapPost("/{publicId:guid}/reset-password", [Permission("USER", "edit")] async (Guid publicId, ResetPasswordRequest request, UserAdminService s, CancellationToken ct) =>
        {
            await s.ResetPasswordAsync(publicId, request, ct);
            return Results.NoContent();
        });
        users.MapPost("/{publicId:guid}/unlock", [Permission("USER", "edit")] async (Guid publicId, UserAdminService s, CancellationToken ct) =>
        {
            await s.UnlockAsync(publicId, ct);
            return Results.NoContent();
        });

        var roles = app.MapGroup("/api/roles").WithTags("Roles");
        roles.MapGet("/", [Permission("ROLE", "view")] (RoleAdminService s, CancellationToken ct) => s.ListAsync(ct));
        app.MapGet("/api/permission-catalog", [Permission("ROLE", "view")] (RoleAdminService s, CancellationToken ct) => s.CatalogAsync(ct))
            .WithTags("Roles");
        roles.MapPost("/", [Permission("ROLE", "add")] async (RoleRequest request, RoleAdminService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(request, ct);
            return Results.Created($"/api/roles/{created.PublicId}", created);
        });
        roles.MapPut("/{publicId:guid}", [Permission("ROLE", "edit")] (Guid publicId, RoleRequest request, RoleAdminService s, CancellationToken ct)
            => s.UpdateAsync(publicId, request, ct));
        roles.MapDelete("/{publicId:guid}", [Permission("ROLE", "delete")] async (Guid publicId, RoleAdminService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(publicId, ct);
            return Results.NoContent();
        });

        app.MapPost("/api/system/impersonation", (StartImpersonationRequest request, ImpersonationService s, CancellationToken ct)
            => s.StartAsync(request, ct)).RequireAuthorization(new RequireSystemContextAttribute()).WithTags("System");

        app.MapPost("/api/system/tenants/{tenantId:long}/admins", async (long tenantId, CreateTenantAdminRequest request, TenantBootstrapService s, CancellationToken ct) =>
        {
            var created = await s.CreateTenantAdminAsync(tenantId, request, ct);
            return Results.Created($"/api/users/{created.PublicId}", created);
        }).RequireAuthorization(new RequireSystemContextAttribute()).WithTags("System");

        // Nguồn quyền cho HttpPermissionSource của các service khác — chỉ token service.
        app.MapGet("/internal/permissions", async (long? tenantId, long userId, PermissionQueries q, CancellationToken ct)
                => (await q.GetEffectiveAsync(tenantId, userId, ct)).Order(StringComparer.Ordinal).ToArray())
            .RequireAuthorization(new RequireServiceCallerAttribute()).WithTags("Internal");

        return app;
    }
}
