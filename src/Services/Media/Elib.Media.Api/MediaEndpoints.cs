using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Media.Application;

namespace Elib.Media.Api;

/// <summary>
/// Đơn vị: gateway /api/admin/media/** → /api/** (cán bộ đã đăng nhập). Nền tảng: /api/system/media/** → /api/system/** (upload hộ đơn vị).
/// Nội dung file không đi qua service: trình duyệt PUT thẳng vào uploadUrl (/s3/** ở gateway → MinIO).
/// </summary>
public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var files = app.MapGroup("/api/files").WithTags("Files").RequireAuthorization();
        files.MapPost("/uploads", (UploadRequest request, MediaService s, CancellationToken ct) => s.RequestUploadAsync(request, ct))
            .AddEndpointFilter(RejectImpersonation);
        files.MapPost("/{id:guid}/complete", (Guid id, MediaService s, CancellationToken ct) => s.CompleteAsync(id, ct))
            .AddEndpointFilter(RejectImpersonation);
        files.MapGet("/{id:guid}", (Guid id, MediaService s, CancellationToken ct) => s.GetAsync(id, ct));
        files.MapGet("/{id:guid}/download", (Guid id, MediaService s, CancellationToken ct) => s.DownloadAsync(id, ct));

        var system = app.MapGroup("/api/system/files").WithTags("SystemFiles").RequireAuthorization(new RequireSystemContextAttribute());
        system.MapPost("/uploads", (SystemUploadRequest request, MediaService s, CancellationToken ct) => s.RequestSystemUploadAsync(request, ct));
        system.MapPost("/{tenantId:long}/{id:guid}/complete", (long tenantId, Guid id, MediaService s, CancellationToken ct)
            => s.CompleteSystemAsync(tenantId, id, ct));
        return app;
    }

    /// <summary>Quản trị nền tảng đang "vào xem đơn vị" chỉ được đọc — các endpoint không dùng [Permission] phải tự chặn ghi.</summary>
    private static ValueTask<object?> RejectImpersonation(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.User.HasClaim(c => c.Type == ElibClaimTypes.Impersonation)
            ? throw new BusinessRuleException(ElibErrorCodes.ImpersonationReadOnly, "Đang xem đơn vị ở chế độ chỉ đọc, không upload được file.", 403)
            : next(context);
}
