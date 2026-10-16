using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.Acquisition.Application;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elib.Acquisition.Infrastructure;

/// <summary>Bản sao biểu ghi theo event của catalog (chỉ đơn vị có license ACQUISITION).</summary>
public sealed class BibChangedConsumer(BibSnapshots snapshots, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<BibChangedConsumer> logger)
    : ElibConsumer<BibChanged>(licenses, logger)
{
    protected override string? RequiredModule => "ACQUISITION";

    protected override async Task HandleAsync(BibChanged message, ConsumeContext<BibChanged> context)
    {
        await snapshots.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Đơn vị vừa mua (hoặc đổi) phân hệ Bổ sung: bản sao biểu ghi còn trống thì chép từ catalog.</summary>
public sealed class AcquisitionLicenseConsumer(BibReplicaBackfill backfill) : IConsumer<ModuleLicenseChanged>
{
    public async Task Consume(ConsumeContext<ModuleLicenseChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Message.Modules.Any(m => m.ModuleCode == BibReplicaBackfill.Module))
            await backfill.RunIfEmptyAsync(context.Message.TenantId, context.CancellationToken);
    }
}

/// <summary>GET catalog /internal/tenants/{tenantId}/bibs[/{mfn}] bằng service token của acquisition.</summary>
public sealed class HttpCatalogBibs(IHttpClientFactory clients) : ICatalogBibs
{
    public const string HttpClientName = "elib-catalog";

    public async Task<BibChanged?> GetAsync(long tenantId, long mfn, CancellationToken ct)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/bibs/{mfn}");
        using var response = await clients.CreateClient(HttpClientName).GetAsync(new Uri(path, UriKind.Relative), ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BibChanged>(ct);
    }

    public async Task<StatePage<BibChanged>> PageAsync(long tenantId, long after, CancellationToken ct)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/bibs?after={after}&take=500");
        using var response = await clients.CreateClient(HttpClientName).GetAsync(new Uri(path, UriKind.Relative), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StatePage<BibChanged>>(ct) ?? new StatePage<BibChanged>([], null);
    }
}

/// <summary>
/// ĐKCB theo phiếu nhận ở holdings (/internal/tenants/{id}/receipt-items, service token). Lỗi nghiệp vụ 4xx của holdings (problem+json
/// có code) chuyển nguyên mã + thông báo cho người dùng; không kết nối được / 5xx → 503 HOLDINGS_UNAVAILABLE.
/// </summary>
public sealed class HttpHoldingsReceiptItems(IHttpClientFactory clients, IHttpContextAccessor http) : IHoldingsReceiptItems
{
    public const string HttpClientName = "elib-holdings";

    public async Task<HoldingsReportPage> ReportAsync(long tenantId, HoldingsReportRequest request, CancellationToken ct) =>
        await SendAsync<HoldingsReportPage>(HttpMethod.Post, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/receipt-items/Report"),
            JsonContent.Create(request), ct) ?? new HoldingsReportPage([], 0);

    public async Task<IReadOnlyList<HoldingsStoreCount>> StoreCountsAsync(long tenantId, HoldingsReportRequest request, CancellationToken ct) =>
        await SendAsync<List<HoldingsStoreCount>>(HttpMethod.Post, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/receipt-items/StoreCounts"),
            JsonContent.Create(request), ct) ?? [];

    public async Task<IReadOnlyList<HoldingsStore>> StoresAsync(long tenantId, CancellationToken ct) =>
        await SendAsync<List<HoldingsStore>>(HttpMethod.Get, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/receipt-items/Stores"),
            null, ct) ?? [];

    public async Task<IReadOnlyList<RegisteredItem>> RegisterAsync(long tenantId, HoldingsRegisterRequest request, CancellationToken ct) =>
        await SendAsync<List<RegisteredItem>>(HttpMethod.Post, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/receipt-items/Register"),
            JsonContent.Create(request), ct) ?? [];

    public async Task<IReadOnlyList<ReceiptItem>> ListAsync(long tenantId, Guid receiptPublicId, CancellationToken ct) =>
        await SendAsync<List<ReceiptItem>>(HttpMethod.Get, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/receipt-items/{receiptPublicId}"),
            null, ct) ?? [];

    public Task RemoveAsync(long tenantId, Guid receiptPublicId, Guid itemPublicId, CancellationToken ct) =>
        SendAsync<object>(HttpMethod.Delete, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/receipt-items/{receiptPublicId}/{itemPublicId}"),
            null, ct);

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct) where T : class
    {
        using var message = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = content };
        // Holdings ghi nhật ký / cột audit đúng người dùng thật thay vì tài khoản service của acquisition.
        var context = http.HttpContext;
        ActingAsHeaders.Apply(message, context?.RequestServices.GetService<ICurrentActor>(), context);
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient(HttpClientName).SendAsync(message, ct);
        }
        catch (HttpRequestException)
        {
            throw Unavailable(null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw Unavailable(null); // hết thời gian chờ
        }
        using (response)
        {
            if (response.IsSuccessStatusCode)
                return response.StatusCode == HttpStatusCode.NoContent ? null : await response.Content.ReadFromJsonAsync<T>(ct);
            var status = (int)response.StatusCode;
            if (status is >= 400 and < 500 && await ProblemAsync(response, ct) is { } problem)
                throw new BusinessRuleException(problem.Code, problem.Detail, status);
            throw Unavailable(status);
        }
    }

    private static async Task<(string Code, string Detail)?> ProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("code", out var code) && code.GetString() is { } c
                ? (c, root.TryGetProperty("detail", out var detail) && detail.GetString() is { } d ? d : $"Kho (holdings) từ chối: {c}.")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static BusinessRuleException Unavailable(int? status) => new("HOLDINGS_UNAVAILABLE",
        status is null
            ? "Không kết nối được phân hệ Kho (holdings) — đăng ký/xoá ĐKCB tạm thời không thực hiện được, thử lại sau."
            : $"Phân hệ Kho (holdings) báo lỗi {status} — thử lại sau.", 503);
}
