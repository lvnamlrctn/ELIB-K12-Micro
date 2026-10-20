using Elib.Space.Application;
using Elib.Space.Domain;

namespace Elib.Space.Api;

public sealed record VerifyRequest(string Uid, DateTimeOffset? ScannedAt = null);

public sealed record AckRequest(Guid Id);

/// <summary>
/// REST cho đầu đọc RFID (gateway: /api/device/space/** → /api/device/**, module SPACE không kiểm ở đây — thiết bị không mang
/// token bạn đọc/staff). Xác thực bằng header X-Device-Code/X-Device-Key, không qua JWT/[Authorize] — xem AccessControlService.
/// </summary>
public static class DeviceSpaceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceSpaceEndpoints(this IEndpointRouteBuilder app)
    {
        var device = app.MapGroup("/api/device").WithTags("AccessDeviceApi").AllowAnonymous();

        device.MapPost("/Verify", async (VerifyRequest request, HttpContext http, AccessControlService svc, TimeProvider clock, CancellationToken ct) =>
        {
            var auth = await AuthenticateAsync(http, svc, ct);
            if (auth is null) return Results.Unauthorized();
            // Phần cứng cửa có thể gửi giờ địa phương kèm offset khác 0 — ElibDbContext chỉ nhận UTC khi lưu.
            var scannedAt = (request.ScannedAt ?? clock.GetUtcNow()).ToUniversalTime();
            return Results.Ok(await svc.VerifyAsync(auth, request.Uid, scannedAt, ct));
        });

        device.MapGet("/Commands", async (HttpContext http, AccessControlService svc, CancellationToken ct) =>
        {
            var auth = await AuthenticateAsync(http, svc, ct);
            return auth is null ? Results.Unauthorized() : Results.Ok(await svc.CommandsAsync(auth, ct));
        });

        device.MapPost("/Commands/Ack", async (AckRequest request, HttpContext http, AccessControlService svc, CancellationToken ct) =>
        {
            var auth = await AuthenticateAsync(http, svc, ct);
            if (auth is null) return Results.Unauthorized();
            await svc.AckAsync(auth, request.Id, ct);
            return Results.NoContent();
        });

        return app;
    }

    private static Task<AccessDevice?> AuthenticateAsync(HttpContext http, AccessControlService svc, CancellationToken ct) =>
        svc.AuthenticateAsync(http.Request.Headers["X-Device-Code"], http.Request.Headers["X-Device-Key"], ct);
}
