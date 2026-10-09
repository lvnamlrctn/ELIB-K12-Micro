using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Notification.Application;

namespace Elib.Notification.Api;

/// <summary>
/// Quản trị gửi tin của đơn vị (gateway: /api/admin/notification/** → /api/**). Gửi tin thật đi qua event NotificationRequested,
/// không có API "gửi thư bất kỳ" — chỉ có thư kiểm tra theo mẫu TEST_EMAIL.
/// </summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/email-settings").WithTags("EmailSettings");
        settings.MapGet("", [Permission("NOTIFICATION_CONFIG", "view")] (EmailSettingsService s, CancellationToken ct) => s.GetAsync(ct));
        settings.MapPut("", [Permission("NOTIFICATION_CONFIG", "edit")] (EmailSettingsRequest request, EmailSettingsService s, CancellationToken ct)
            => s.SaveAsync(request, ct));
        settings.MapDelete("", [Permission("NOTIFICATION_CONFIG", "delete")] async (EmailSettingsService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(ct);
            return Results.NoContent();
        });
        settings.MapPost("/test", [Permission("NOTIFICATION_CONFIG", "edit")] (TestEmailRequest request, EmailSettingsService s, CancellationToken ct)
            => s.SendTestAsync(request, ct));

        var templates = app.MapCrud<EmailTemplateResource>("/api/email-templates", "NOTIFICATION_TEMPLATES").WithTags("EmailTemplates");
        templates.MapPost("/RestoreDefaults", [Permission("NOTIFICATION_TEMPLATES", "add")] async (EmailTemplateResource r, CancellationToken ct)
            => Results.Ok(new { added = await r.AddMissingDefaultsAsync(ct) }));

        app.MapPost("/api/notification-logs/Search", [Permission("NOTIFICATION_LOGS", "view")] (NotificationLogSearch search, NotificationLogQueries q, CancellationToken ct)
            => q.SearchAsync(search, ct)).WithTags("NotificationLogs");

        return app;
    }
}
