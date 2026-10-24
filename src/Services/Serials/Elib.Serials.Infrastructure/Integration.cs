using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Platform;
using Elib.Serials.Application;
using Elib.Serials.Domain;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elib.Serials.Infrastructure;

/// <summary>Bản sao biểu ghi theo event của catalog (chỉ đơn vị có license SERIALS); nhan đề đổi thì đăng ký gắn biểu ghi đó đổi theo khi chưa duyệt.</summary>
public sealed class BibChangedConsumer(BibSnapshots snapshots, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<BibChangedConsumer> logger)
    : ElibConsumer<BibChanged>(licenses, logger)
{
    protected override string? RequiredModule => "SERIALS";

    protected override async Task HandleAsync(BibChanged message, ConsumeContext<BibChanged> context)
    {
        await snapshots.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>
/// Đơn vị vừa mua (hoặc đổi) phân hệ Báo - Tạp chí: bổ sung danh mục mặc định còn thiếu (đơn vị đã hoạt động trước khi mua phân hệ thì không đi qua
/// seed lúc khởi tạo) và chép bản sao biểu ghi còn trống từ catalog. Idempotent.
/// </summary>
public sealed class SerialsLicenseConsumer(PublicationTypeResource types, FrequencyResource frequencies, PatternResource patterns, BibReplicaBackfill backfill)
    : IConsumer<ModuleLicenseChanged>
{
    public async Task Consume(ConsumeContext<ModuleLicenseChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Message.Modules.Any(m => m.ModuleCode == BibReplicaBackfill.Module)) return;
        var ct = context.CancellationToken;
        await types.AddMissingDefaultsAsync(ct);
        await frequencies.AddMissingDefaultsAsync(ct);
        await patterns.AddMissingDefaultsAsync(ct);
        await backfill.RunIfEmptyAsync(context.Message.TenantId, ct);
    }
}

/// <summary>
/// Đơn vị mới: loại ấn phẩm, tần suất, mẫu đánh số mặc định (hard-code trong mã, không đọc DB của monolith); đơn vị có phân hệ Báo - Tạp chí thì chép bản
/// sao biểu ghi từ catalog.
/// </summary>
public sealed class SerialsTenantSeeder(PublicationTypeResource types, FrequencyResource frequencies, PatternResource patterns, BibReplicaBackfill backfill) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        await types.AddMissingDefaultsAsync(cancellationToken);
        await frequencies.AddMissingDefaultsAsync(cancellationToken);
        await patterns.AddMissingDefaultsAsync(cancellationToken);
        if (tenant.Modules.Any(m => m.ModuleCode == BibReplicaBackfill.Module)) await backfill.RunIfEmptyAsync(tenant.TenantId, cancellationToken);
    }
}

/// <summary>Chép bản sao biểu ghi của đơn vị hiện tại từ catalog khi còn trống. Catalog chưa trả lời được thì bỏ qua: biểu ghi vẫn tới qua event và tra được theo MFN.</summary>
public sealed partial class BibReplicaBackfill(BibSnapshots bibs, ICrudDbContext db, ILogger<BibReplicaBackfill> logger)
{
    public const string Module = "SERIALS";

    public async Task RunIfEmptyAsync(long tenantId, CancellationToken ct)
    {
        if (await db.Set<BibSnapshot>().AnyAsync(ct)) return;
        try
        {
            var count = await bibs.BackfillAsync(ct);
            LogBackfilled(logger, tenantId, count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogBackfillFailed(logger, ex, tenantId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: đã chép {Count} biểu ghi từ catalog vào bản sao của serials")]
    private static partial void LogBackfilled(ILogger logger, long tenantId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đơn vị {TenantId}: chưa chép được biểu ghi từ catalog — biểu ghi tới dần qua event / tra theo MFN")]
    private static partial void LogBackfillFailed(ILogger logger, Exception exception, long tenantId);
}

/// <summary>GET catalog /internal/tenants/{tenantId}/bibs[/{mfn}] bằng service token của serials.</summary>
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
/// ĐKCB của tập đóng ở holdings (/internal/tenants/{id}/serial-items, service token). Lỗi nghiệp vụ 4xx của holdings (problem+json có code) chuyển nguyên mã +
/// thông báo cho người dùng; không kết nối được / 5xx → 503 HOLDINGS_UNAVAILABLE.
/// </summary>
public sealed class HttpHoldingsSerialItems(IHttpClientFactory clients, IHttpContextAccessor http) : IHoldingsSerialItems
{
    public const string HttpClientName = "elib-holdings";

    public async Task<SerialItemRegistered> RegisterAsync(long tenantId, SerialItemRegisterRequest request, CancellationToken ct) =>
        await SendAsync<SerialItemRegistered>(HttpMethod.Post, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/serial-items/Register"),
            JsonContent.Create(request), ct) ?? throw Unavailable(null);

    public Task RemoveAsync(long tenantId, Guid bindingPublicId, CancellationToken ct) =>
        SendAsync<object>(HttpMethod.Delete, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/serial-items/{bindingPublicId}"), null, ct);

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct) where T : class
    {
        using var message = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = content };
        // Holdings ghi nhật ký / cột audit đúng người dùng thật thay vì tài khoản service của serials.
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
            ? "Không kết nối được phân hệ Kho (holdings) — đóng tập/gỡ ĐKCB tạm thời không thực hiện được, thử lại sau."
            : $"Phân hệ Kho (holdings) báo lỗi {status} — thử lại sau.", 503);
}

/// <summary>
/// Nhà cung cấp của acquisition (GET /internal/tenants/{id}/suppliers). Tuỳ chọn: đơn vị không có phân hệ Bổ sung hoặc acquisition không trả lời thì
/// trả danh sách rỗng và ghi cảnh báo — màn đăng ký chỉ ẩn ô nhà cung cấp.
/// </summary>
public sealed partial class HttpAcquisitionSuppliers(IHttpClientFactory clients, ILogger<HttpAcquisitionSuppliers> logger) : ISupplierSource
{
    public const string HttpClientName = "elib-acquisition";

    public async Task<IReadOnlyList<SupplierOption>> ListAsync(long tenantId, CancellationToken ct)
    {
        try
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/suppliers");
            using var response = await clients.CreateClient(HttpClientName).GetAsync(new Uri(path, UriKind.Relative), ct);
            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(logger, tenantId, (int)response.StatusCode);
                return [];
            }
            return await response.Content.ReadFromJsonAsync<List<SupplierOption>>(ct) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            LogFailed(logger, ex, tenantId);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đơn vị {TenantId}: acquisition trả {Status} khi lấy nhà cung cấp — dùng danh sách rỗng")]
    private static partial void LogUnavailable(ILogger logger, long tenantId, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đơn vị {TenantId}: không lấy được nhà cung cấp từ acquisition — dùng danh sách rỗng")]
    private static partial void LogFailed(ILogger logger, Exception exception, long tenantId);
}
