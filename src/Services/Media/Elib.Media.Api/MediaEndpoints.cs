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
        var files = app.MapGroup("/api/files").WithTags("Files").RequireAuthorization(new RequireStaffAttribute());
        files.MapPost("/uploads", (UploadRequest request, MediaService s, CancellationToken ct) => s.RequestUploadAsync(request, ct))
            .AddEndpointFilter(RejectImpersonation);
        files.MapPost("/{id:guid}/complete", (Guid id, MediaService s, CancellationToken ct) => s.CompleteAsync(id, ct))
            .AddEndpointFilter(RejectImpersonation);
        files.MapPost("/import", (ImportRequest request, MediaService s, CancellationToken ct) => s.ImportAsync(request, ct))
            .AddEndpointFilter(RejectImpersonation);
        files.MapGet("/", (string? purpose, int? page, int? pageSize, MediaService s, CancellationToken ct) => s.ListAsync(purpose, page ?? 1, pageSize ?? 24, ct));
        files.MapGet("/{id:guid}", (Guid id, MediaService s, CancellationToken ct) => s.GetAsync(id, ct));
        files.MapGet("/{id:guid}/download", (Guid id, MediaService s, CancellationToken ct) => s.DownloadAsync(id, ct));
        files.MapPost("/urls", (FileUrlsRequest request, MediaService s, CancellationToken ct) => s.UrlsAsync(request, ct));

        var system = app.MapGroup("/api/system/files").WithTags("SystemFiles").RequireAuthorization(new RequireSystemContextAttribute());
        system.MapPost("/uploads", (SystemUploadRequest request, MediaService s, CancellationToken ct) => s.RequestSystemUploadAsync(request, ct));
        system.MapPost("/{tenantId:long}/{id:guid}/complete", (long tenantId, Guid id, MediaService s, CancellationToken ct)
            => s.CompleteSystemAsync(tenantId, id, ct));
        // Nội bộ: service khác đọc nội dung ảnh bạn đọc (nhận diện khuôn mặt) — chỉ service token, không qua gateway.
        app.MapGet("/internal/tenants/{tenantId:long}/files/{id:guid}/content", async (long tenantId, Guid id, ITenantContext tenant, MediaService s, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
            {
                var content = await s.ReadReaderPhotoAsync(id, ct);
                return Results.File(content.Bytes, content.ContentType);
            }
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        // Nội bộ: digital xin URL có hạn của file tài liệu số sau khi đã TỰ kiểm quyền truy cập (mượn/chính sách) — chỉ purpose
        // ebook-file, không phải URL chung cho mọi loại file (tránh digital xin được URL của ảnh thẻ/tệp đính kèm service khác).
        app.MapGet("/internal/tenants/{tenantId:long}/files/{id:guid}/download", async (long tenantId, Guid id, ITenantContext tenant, MediaService s, CancellationToken ct) =>
        {
            using (tenant.Use(tenantId))
                return await s.DownloadEbookFileAsync(id, ct);
        }).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());

        // Nội bộ: digital xin vé upload hộ bạn đọc đã tự xác thực (nộp tài liệu qua OPAC) — chỉ mục đích ServiceRequestable
        // (MediaService tự kiểm), và hoàn tất bằng chính CompleteSystemAsync đã có (chuyển ngữ cảnh đơn vị rồi complete như
        // thường, không cần thêm một bản sao logic complete riêng).
        app.MapPost("/internal/tenants/{tenantId:long}/files/uploads", (long tenantId, UploadRequest request, MediaService s, CancellationToken ct) =>
            s.RequestServiceUploadAsync(tenantId, request, ct)).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        app.MapPost("/internal/tenants/{tenantId:long}/files/{id:guid}/complete", (long tenantId, Guid id, MediaService s, CancellationToken ct) =>
            s.CompleteSystemAsync(tenantId, id, ct)).WithTags("Internal").RequireAuthorization(new RequireServiceCallerAttribute());
        return app;
    }

    /// <summary>Quản trị nền tảng đang "vào xem đơn vị" chỉ được đọc — các endpoint không dùng [Permission] phải tự chặn ghi.</summary>
    private static ValueTask<object?> RejectImpersonation(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.User.HasClaim(c => c.Type == ElibClaimTypes.Impersonation)
            ? throw new BusinessRuleException(ElibErrorCodes.ImpersonationReadOnly, "Đang xem đơn vị ở chế độ chỉ đọc, không upload được file.", 403)
            : next(context);
}
