using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Platform;
using Elib.Notification.Application;
using Elib.Notification.Domain;
using Elib.Notification.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Notification.Tests.Api;

public sealed class NotificationApiTests : IClassFixture<NotificationApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 1000;
    private static long _nextUserId = 5000;

    private readonly NotificationApiFactory _factory;

    public NotificationApiTests(NotificationApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    /// <summary>Đơn vị mới qua đúng luồng saga: TenantProvisioned → bản sao + seed mẫu → TenantSeeded.</summary>
    private async Task<long> ProvisionAsync(string name = "Trường thử")
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        await _factory.PublishAsync(new TenantProvisioned
        {
            TenantId = tenantId, Code = "T" + tenantId, Name = name, Subdomain = "t" + tenantId, Modules = [],
        });
        await NotificationApiFactory.WaitForAsync(
            () => Task.FromResult(_factory.Published.OfType<TenantSeeded>().FirstOrDefault(e => e.TenantId == tenantId)), "TenantSeeded");
        return tenantId;
    }

    private async Task<NotificationLog> WaitLogAsync(long tenantId, string recipient)
    {
        return await NotificationApiFactory.WaitForAsync(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            return await db.NotificationLogs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Recipient == recipient);
        }, "nhật ký gửi " + recipient);
    }

    private static NotificationRequested Otp(long tenantId, string email, string otp = "123456", string? dedup = null) => new()
    {
        TenantId = tenantId,
        TemplateCode = DefaultTemplates.LoginOtp,
        Recipient = new NotificationRecipient(null, 7, email, null, "Nguyễn <b>An</b>"),
        Data = new Dictionary<string, string> { ["otp"] = otp, ["minutes"] = "5" },
        DeduplicationKey = dedup,
    };

    [Fact]
    public async Task Provisioned_tenant_gets_default_templates()
    {
        var tenantId = await ProvisionAsync();
        var seeded = _factory.Published.OfType<TenantSeeded>().Single(e => e.TenantId == tenantId);
        Assert.Equal("notification", seeded.Service);
        Assert.True(seeded.Succeeded);

        var templates = await Read<IReadOnlyList<EmailTemplateDto>>(
            await Staff(tenantId).PostAsJsonAsync("/api/email-templates/SearchAll", new CrudSearch(), Json));
        Assert.Equal([DefaultTemplates.LoginOtp, DefaultTemplates.PrintDueSoon, DefaultTemplates.PrintHoldExpired, DefaultTemplates.PrintHoldReady,
            DefaultTemplates.PrintOverdue, DefaultTemplates.TestEmail], templates.Select(t => t.Code));
        Assert.All(templates, t => Assert.True(t.IsBuiltIn));

        var restore = await Staff(tenantId).PostAsync(new Uri("/api/email-templates/RestoreDefaults", UriKind.Relative), null);
        Assert.Equal(0, JsonDocument.Parse(await restore.Content.ReadAsStringAsync()).RootElement.GetProperty("added").GetInt32());
    }

    [Fact]
    public async Task Requested_email_is_rendered_and_sent_via_platform_smtp()
    {
        var tenantId = await ProvisionAsync("Trường Tiểu học Hoa Sen");
        await _factory.PublishAsync(Otp(tenantId, "an@example.com"));

        var log = await WaitLogAsync(tenantId, "an@example.com");
        Assert.Equal(NotificationStatus.Sent, log.Status);
        Assert.True(log.ViaPlatform);

        var (endpoint, email) = Assert.Single(_factory.Smtp.To("an@example.com"));
        Assert.Equal(NotificationApiFactory.PlatformHost, endpoint.Host);
        Assert.Equal("Mã xác thực đăng nhập Trường Tiểu học Hoa Sen", email.Subject);
        Assert.Contains("123456", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Nguyễn &lt;b&gt;An&lt;/b&gt;", email.HtmlBody, StringComparison.Ordinal); // dữ liệu được mã hoá HTML
        Assert.Equal(log.Subject, email.Subject);
    }

    [Fact]
    public async Task System_notification_uses_platform_smtp_and_default_template()
    {
        await _factory.PublishAsync(new SystemNotificationRequested
        {
            TemplateCode = DefaultTemplates.LoginOtp,
            Recipient = new NotificationRecipient(null, 1, "sysadmin@platform.test", null, "Quản trị hệ thống"),
            Data = new Dictionary<string, string> { ["otp"] = "424242", ["minutes"] = "5" },
        });
        var email = await NotificationApiFactory.WaitForAsync(
            () => Task.FromResult(_factory.Smtp.To("sysadmin@platform.test").Select(x => x.Email).FirstOrDefault()), "thư nền tảng");
        var endpoint = _factory.Smtp.To("sysadmin@platform.test").Single().Endpoint;
        Assert.Equal(NotificationApiFactory.PlatformHost, endpoint.Host);
        Assert.Contains("424242", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Quản trị hệ thống", email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_deduplication_key_is_sent_once()
    {
        var tenantId = await ProvisionAsync();
        await _factory.PublishAsync(Otp(tenantId, "dedup@example.com", dedup: "otp:7:1"));
        await WaitLogAsync(tenantId, "dedup@example.com");
        await _factory.PublishAsync(Otp(tenantId, "dedup@example.com", dedup: "otp:7:1"));
        await _factory.PublishAsync(Otp(tenantId, "dedup2@example.com"));
        await WaitLogAsync(tenantId, "dedup2@example.com");

        Assert.Single(_factory.Smtp.To("dedup@example.com"));
    }

    [Fact]
    public async Task Smtp_failure_and_missing_template_are_logged_not_thrown()
    {
        var tenantId = await ProvisionAsync();
        await _factory.PublishAsync(Otp(tenantId, "fail@example.com"));
        var failed = await WaitLogAsync(tenantId, "fail@example.com");
        Assert.Equal(NotificationStatus.Failed, failed.Status);
        Assert.Contains("550", failed.Error, StringComparison.Ordinal);

        await _factory.PublishAsync(Otp(tenantId, "nope@example.com") with { TemplateCode = "UNKNOWN_CODE" });
        var missing = await WaitLogAsync(tenantId, "nope@example.com");
        Assert.Equal(NotificationStatus.Failed, missing.Status);
        Assert.Empty(_factory.Smtp.To("nope@example.com"));
    }

    [Fact]
    public async Task Tenant_template_override_and_disable()
    {
        var tenantId = await ProvisionAsync();
        var staff = Staff(tenantId);
        var all = await Read<IReadOnlyList<EmailTemplateDto>>(await staff.PostAsJsonAsync("/api/email-templates/SearchAll", new CrudSearch(), Json));
        var otp = all.Single(t => t.Code == DefaultTemplates.LoginOtp);

        await Read<EmailTemplateDto>(await staff.PutAsJsonAsync($"/api/email-templates/Update/{otp.PublicId}",
            new EmailTemplateRequest(otp.Code, otp.Name, "OTP của bạn", "<p>Mã: {{otp}}</p>", 2), Json));
        await _factory.PublishAsync(Otp(tenantId, "own@example.com", otp: "999111"));
        await WaitLogAsync(tenantId, "own@example.com");
        var (_, email) = Assert.Single(_factory.Smtp.To("own@example.com"));
        Assert.Equal("OTP của bạn", email.Subject);
        Assert.Equal("<p>Mã: 999111</p>", email.HtmlBody);

        // Đổi mã mẫu bị chặn.
        var rename = await staff.PutAsJsonAsync($"/api/email-templates/Update/{otp.PublicId}",
            new EmailTemplateRequest("OTHER_CODE", otp.Name, "x", "y", 2), Json);
        Assert.Equal("TEMPLATE_CODE_IMMUTABLE", await ErrorCode(rename));

        // Tắt mẫu = không gửi.
        Assert.Equal(HttpStatusCode.NoContent,
            (await staff.PutAsJsonAsync("/api/email-templates/ChangeStatus", new ChangeStatusRequest(otp.PublicId, 1), Json)).StatusCode);
        await _factory.PublishAsync(Otp(tenantId, "off@example.com"));
        Assert.Equal(NotificationStatus.Failed, (await WaitLogAsync(tenantId, "off@example.com")).Status);
        Assert.Empty(_factory.Smtp.To("off@example.com"));
    }

    [Fact]
    public async Task Tenant_smtp_overrides_platform_and_password_never_leaves()
    {
        var tenantId = await ProvisionAsync();
        var staff = Staff(tenantId);

        var empty = await Read<EmailSettingsDto>(await staff.GetAsync(new Uri("/api/email-settings", UriKind.Relative)));
        Assert.False(empty.Configured);
        Assert.True(empty.PlatformAvailable);

        var saved = await Read<EmailSettingsDto>(await staff.PutAsJsonAsync("/api/email-settings",
            new EmailSettingsRequest("8.8.8.8", 587, SmtpSecurity.StartTls, "thuvien", "s3cret-pass", false, "thuvien@truong.edu.vn", "Thư viện", 2), Json));
        Assert.True(saved.Configured);
        Assert.True(saved.HasPassword);
        var raw = await (await staff.GetAsync(new Uri("/api/email-settings", UriKind.Relative))).Content.ReadAsStringAsync();
        Assert.DoesNotContain("s3cret-pass", raw, StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var row = await db.EmailSettings.IgnoreQueryFilters().SingleAsync(s => s.TenantId == tenantId);
            Assert.NotNull(row.ProtectedPassword);
            Assert.DoesNotContain("s3cret-pass", row.ProtectedPassword, StringComparison.Ordinal);
        }

        // Lưu lại không gửi mật khẩu = giữ mật khẩu cũ; gửi thử dùng SMTP của đơn vị.
        await Read<EmailSettingsDto>(await staff.PutAsJsonAsync("/api/email-settings",
            new EmailSettingsRequest("8.8.8.8", 465, SmtpSecurity.SslOnConnect, "thuvien", null, false, "thuvien@truong.edu.vn", "Thư viện", 2), Json));
        var test = await Read<NotificationLogDto>(await staff.PostAsJsonAsync("/api/email-settings/test", new TestEmailRequest("admin@truong.edu.vn"), Json));
        Assert.Equal(NotificationStatus.Sent, test.Status);
        Assert.False(test.ViaPlatform);
        var (endpoint, _) = Assert.Single(_factory.Smtp.To("admin@truong.edu.vn"));
        Assert.Equal(("8.8.8.8", 465, SmtpSecurity.SslOnConnect, "s3cret-pass", "thuvien@truong.edu.vn"),
            (endpoint.Host, endpoint.Port, endpoint.Security, endpoint.Password, endpoint.FromAddress));

        // Bỏ cấu hình riêng → quay về SMTP nền tảng.
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(new Uri("/api/email-settings", UriKind.Relative))).StatusCode);
        Assert.False((await Read<EmailSettingsDto>(await staff.GetAsync(new Uri("/api/email-settings", UriKind.Relative)))).Configured);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.20.0.3")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    public async Task Smtp_host_in_private_network_is_rejected(string host)
    {
        var tenantId = await ProvisionAsync();
        var response = await Staff(tenantId).PutAsJsonAsync("/api/email-settings",
            new EmailSettingsRequest(host, 25, SmtpSecurity.None, null, null, false, "a@b.vn", null, 2), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("SMTP_HOST_NOT_ALLOWED", await ErrorCode(response));
    }

    [Fact]
    public async Task Logs_and_settings_do_not_leak_between_tenants_and_need_permission()
    {
        var a = await ProvisionAsync();
        var b = await ProvisionAsync();
        await _factory.PublishAsync(Otp(a, "only-a@example.com"));
        await WaitLogAsync(a, "only-a@example.com");
        await Read<EmailSettingsDto>(await Staff(a).PutAsJsonAsync("/api/email-settings",
            new EmailSettingsRequest("8.8.4.4", 587, SmtpSecurity.StartTls, null, null, false, "a@a.vn", null, 2), Json));

        var logsA = await Read<CrudPage<NotificationLogDto>>(await Staff(a).PostAsJsonAsync("/api/notification-logs/Search", new NotificationLogSearch(), Json));
        Assert.Contains(logsA.Items, l => l.Recipient == "only-a@example.com");
        var logsB = await Read<CrudPage<NotificationLogDto>>(await Staff(b).PostAsJsonAsync("/api/notification-logs/Search", new NotificationLogSearch(), Json));
        Assert.DoesNotContain(logsB.Items, l => l.Recipient == "only-a@example.com");
        Assert.False((await Read<EmailSettingsDto>(await Staff(b).GetAsync(new Uri("/api/email-settings", UriKind.Relative)))).Configured);

        var noGrant = Staff(a, "ORGS:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await noGrant.GetAsync(new Uri("/api/email-settings", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noGrant.PostAsJsonAsync("/api/notification-logs/Search", new NotificationLogSearch(), Json)).StatusCode);
        var viewOnly = Staff(a, "NOTIFICATION_CONFIG:view");
        Assert.Equal(HttpStatusCode.OK, (await viewOnly.GetAsync(new Uri("/api/email-settings", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsJsonAsync("/api/email-settings/test", new TestEmailRequest("x@y.vn"), Json)).StatusCode);
    }
}
