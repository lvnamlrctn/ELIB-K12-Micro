using Elib.Audit.Application;
using Elib.BuildingBlocks.Authorization;

namespace Elib.Audit.Api;

/// <summary>
/// Nhật ký hệ thống — chỉ đọc. Đơn vị: gateway /api/admin/audit/** → /api/** (quyền SYSTEM_LOG:view như monolith UserLog).
/// Nền tảng: gateway /api/system/audit/** → /api/system/** (chỉ quản trị nền tảng).
/// </summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var logs = app.MapGroup("/api/audit-logs").WithTags("AuditLogs");
        logs.MapPost("/Search", [Permission("SYSTEM_LOG", "view")] (AuditLogSearch search, AuditQueries q, CancellationToken ct) => q.SearchAsync(search, ct));
        logs.MapGet("/actions", [Permission("SYSTEM_LOG", "view")] (AuditQueries q, CancellationToken ct) => q.ActionsAsync(ct));

        var platform = app.MapGroup("/api/system/audit-logs").WithTags("PlatformAuditLogs").RequireAuthorization(new RequireSystemContextAttribute());
        platform.MapPost("/Search", (PlatformAuditLogSearch search, AuditQueries q, CancellationToken ct) => q.SearchPlatformAsync(search, ct));
        platform.MapGet("/actions", (AuditQueries q, CancellationToken ct) => q.PlatformActionsAsync(ct));
        return app;
    }
}
