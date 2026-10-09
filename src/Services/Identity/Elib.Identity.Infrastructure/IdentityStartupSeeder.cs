using System.Data.Common;
using Elib.BuildingBlocks.Tenancy;
using Elib.Identity.Application;
using Elib.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Elib.Identity.Infrastructure;

/// <summary>Seed client OIDC từ cấu hình và tài khoản quản trị hệ thống đầu tiên. Idempotent — chạy ở mỗi lần khởi động.</summary>
public sealed partial class IdentityStartupSeeder(
    IOpenIddictApplicationManager applications, IIdentityDb db, ITenantContext tenant, IPasswordHasher hasher,
    IOptions<IdentityServerOptions> options, ILogger<IdentityStartupSeeder> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var (clientId, client) in options.Value.Clients) await UpsertClientAsync(clientId, client, ct);
        await EnsureBootstrapAdminAsync(ct);
    }

    private async Task UpsertClientAsync(string clientId, ClientDefinition client, CancellationToken ct)
    {
        var confidential = !string.IsNullOrEmpty(client.ClientSecret);
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = confidential ? client.ClientSecret : null,
            ClientType = confidential ? ClientTypes.Confidential : ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit, // client của chính nền tảng — không hỏi đồng ý
            DisplayName = string.IsNullOrEmpty(client.DisplayName) ? clientId : client.DisplayName,
        };
        foreach (var uri in client.RedirectUris) descriptor.RedirectUris.Add(new Uri(uri));
        foreach (var uri in client.PostLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(new Uri(uri));

        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + ElibScopes.Api);
        foreach (var grant in client.GrantTypes)
        {
            switch (grant)
            {
                case GrantTypes.AuthorizationCode:
                    descriptor.Permissions.UnionWith([
                        Permissions.Endpoints.Authorization, Permissions.Endpoints.EndSession,
                        Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code, Permissions.Scopes.Profile]);
                    if (!confidential) descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
                    break;
                case GrantTypes.RefreshToken:
                    descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                    break;
                case GrantTypes.ClientCredentials:
                    if (!confidential) throw new InvalidOperationException($"Client '{clientId}' dùng client_credentials phải có ClientSecret.");
                    descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
                    break;
                default:
                    throw new InvalidOperationException($"Grant type '{grant}' của client '{clientId}' không được hỗ trợ.");
            }
        }

        var existing = await applications.FindByClientIdAsync(clientId, ct);
        if (existing is null) await applications.CreateAsync(descriptor, ct);
        else await applications.UpdateAsync(existing, descriptor, ct);
    }

    private async Task EnsureBootstrapAdminAsync(CancellationToken ct)
    {
        using var system = tenant.UseSystem();
        var bootstrap = options.Value.BootstrapAdmin;
        if (await db.SystemUsers.AnyAsync(ct))
        {
            // Bổ sung email (nhận OTP) cho tài khoản bootstrap đã tạo từ trước — chỉ khi tài khoản chưa có email.
            if (!string.IsNullOrWhiteSpace(bootstrap.Email) && !string.IsNullOrWhiteSpace(bootstrap.UserName))
            {
                var name = AccountRules.NormalizeUserName(bootstrap.UserName);
                var existing = await db.SystemUsers.FirstOrDefaultAsync(u => u.UserName == name && u.Email == null, ct);
                if (existing is not null)
                {
                    existing.SetEmail(bootstrap.Email);
                    await db.SaveChangesAsync(ct);
                }
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.UserName) || string.IsNullOrEmpty(bootstrap.Password))
        {
            LogNoBootstrap(logger);
            return;
        }

        AccountRules.EnsureStrongPassword(bootstrap.Password);
        db.SystemUsers.Add(SystemUser.Create(bootstrap.UserName, bootstrap.FullName, bootstrap.Email, hasher.Hash(bootstrap.Password), mustChangePassword: true));
        await db.SaveChangesAsync(ct);
        LogBootstrapCreated(logger, bootstrap.UserName);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chưa có tài khoản quản trị hệ thống và chưa cấu hình Identity:BootstrapAdmin — không ai đăng nhập quản trị được")]
    private static partial void LogNoBootstrap(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đã tạo tài khoản quản trị hệ thống đầu tiên '{UserName}' (bắt buộc đổi mật khẩu)")]
    private static partial void LogBootstrapCreated(ILogger logger, string userName);
}

/// <summary>Chạy seeder khi khởi động; DB có thể chưa sẵn sàng (pod khởi động cùng lúc) nên thử lại.</summary>
public sealed partial class IdentityStartupSeederService(IServiceScopeFactory scopes, ILogger<IdentityStartupSeederService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 30 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IdentityStartupSeeder>().RunAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is DbException or DbUpdateException or InvalidOperationException { InnerException: DbException })
            {
                LogRetry(logger, ex, attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed identity thất bại (lần {Attempt}), thử lại")]
    private static partial void LogRetry(ILogger logger, Exception exception, int attempt);
}
