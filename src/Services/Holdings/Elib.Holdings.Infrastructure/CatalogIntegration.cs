using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Circulation;
using Elib.Holdings.Application;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Elib.Holdings.Infrastructure;

/// <summary>Bản sao biểu ghi theo event của catalog (chỉ đơn vị có license HOLDINGS).</summary>
public sealed class BibChangedConsumer(BibSnapshots snapshots, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<BibChangedConsumer> logger)
    : ElibConsumer<BibChanged>(licenses, logger)
{
    protected override string? RequiredModule => "HOLDINGS";

    protected override async Task HandleAsync(BibChanged message, ConsumeContext<BibChanged> context)
    {
        await snapshots.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Lượt mượn từ circulation: hiển thị "đang mượn", lượt đóng vì mất tài liệu → bản sách sang "Mất" (chỉ đơn vị có license HOLDINGS).</summary>
public sealed class LoanChangedConsumer(ItemResource items, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<LoanChangedConsumer> logger)
    : ElibConsumer<LoanChanged>(licenses, logger)
{
    protected override string? RequiredModule => "HOLDINGS";

    protected override async Task HandleAsync(LoanChanged message, ConsumeContext<LoanChanged> context)
    {
        await items.ApplyLoanAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>GET catalog /internal/tenants/{tenantId}/bibs/{mfn} bằng service token của holdings.</summary>
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
}
