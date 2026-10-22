using System.Globalization;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Payment;
using Elib.Contracts.Events.Platform;
using Elib.Payment.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Payment.Infrastructure;

/// <summary>Đọc khoản đang nợ theo trang từ /internal của circulation (service token của payment).</summary>
public sealed class HttpChargeSources(IHttpClientFactory clients) : IChargeSources
{
    public const string Circulation = "elib-circulation";

    /// <summary>Mỗi nguồn một con trỏ riêng: phiếu phạt và phiếu sao chụp của circulation đọc từ hai danh sách khác nhau.</summary>
    public string[] Services { get; } = ["circulation:FINE_TICKET", "circulation:PHOTOCOPY"];

    public async Task<StatePage<ChargeIssued>> OpenChargesAsync(string service, long tenantId, long after, CancellationToken cancellationToken)
    {
        var parts = service.Split(':', 2);
        if (parts[0] != "circulation") return new StatePage<ChargeIssued>([], null);
        var path = string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/charges/open?kind={(parts.Length > 1 ? parts[1] : "FINE_TICKET")}&after={after}&take=1000");
        using var response = await clients.CreateClient(Circulation).GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StatePage<ChargeIssued>>(cancellationToken) ?? new StatePage<ChargeIssued>([], null);
    }
}

/// <summary>
/// Mỗi 5 phút (và ngay sau khi khởi động) dựng khoản thu cho đơn vị có phân hệ PAYMENT mà chưa đồng bộ với nguồn — đơn vị vừa mua phân hệ hoặc service
/// vừa được triển khai. Đồng bộ rồi thì bỏ qua (cán bộ có thể bấm "Đồng bộ lại"). Nguồn đang tắt thì thử lại ở vòng sau.
/// </summary>
public sealed partial class ChargeBackfillJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ChargeBackfillJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), clock, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), clock);
        do
        {
            try
            {
                await RunAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunAllAsync(CancellationToken ct)
    {
        List<long> tenants;
        using (var scope = scopes.CreateScope())
        {
            using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
            tenants = await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Set<TenantReplicaRecord>().AsNoTracking()
                .Where(t => t.Status == "Active").OrderBy(t => t.TenantId).Select(t => t.TenantId).ToListAsync(ct);
        }
        foreach (var tenantId in tenants)
        {
            using var scope = scopes.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<IModuleLicenseSource>().IsLicensedAsync(tenantId, PaymentExpiryJob.Module, ct)) continue;
            using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            try
            {
                var backfill = scope.ServiceProvider.GetRequiredService<ChargeBackfill>();
                if (await backfill.IsSyncedAsync(ct)) continue;
                var n = await backfill.RunAsync(tenantId, force: false, ct);
                LogRan(logger, tenantId, n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTenantFailed(logger, tenantId, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: đã dựng {Count} khoản thu từ dữ liệu có sẵn")]
    private static partial void LogRan(ILogger logger, long tenantId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đơn vị {TenantId}: chưa dựng được khoản thu từ dữ liệu có sẵn (nguồn chưa sẵn sàng?) — thử lại sau")]
    private static partial void LogTenantFailed(ILogger logger, long tenantId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job dựng khoản thu thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}

public static class ChargeSourceRegistration
{
    public static IServiceCollection AddChargeSources(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var url = configuration["Circulation:Url"];
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("Thiếu Circulation:Url (địa chỉ nội bộ của service circulation).");
        services.AddElibServiceTokens();
        services.AddHttpClient(HttpChargeSources.Circulation, c =>
            {
                c.BaseAddress = new Uri(url.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddScoped<IChargeSources, HttpChargeSources>();
        services.AddSingleton<ChargeBackfillJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ChargeBackfillJob>());
        return services;
    }
}
