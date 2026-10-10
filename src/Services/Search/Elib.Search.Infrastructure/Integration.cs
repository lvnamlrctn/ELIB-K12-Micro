using System.Globalization;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Messaging;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Platform;
using Elib.Search.Application;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Elib.Search.Infrastructure;

// Chỉ mục ghi cho mọi đơn vị (không đòi license SEARCH): read model nhỏ, đơn vị mua thêm Tra cứu dùng được ngay không phải dựng lại.

/// <summary>Biểu ghi từ catalog.</summary>
public sealed class BibChangedConsumer(SearchIndex index, ISearchDb db, IModuleLicenseSource licenses, ILogger<BibChangedConsumer> logger)
    : ElibConsumer<BibChanged>(licenses, logger)
{
    protected override async Task HandleAsync(BibChanged message, ConsumeContext<BibChanged> context)
    {
        await index.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Bản sách từ holdings.</summary>
public sealed class ItemChangedConsumer(SearchIndex index, ISearchDb db, IModuleLicenseSource licenses, ILogger<ItemChangedConsumer> logger)
    : ElibConsumer<ItemChanged>(licenses, logger)
{
    protected override async Task HandleAsync(ItemChanged message, ConsumeContext<ItemChanged> context)
    {
        await index.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Lượt mượn từ circulation — "đang mượn" / còn bản sẵn sàng.</summary>
public sealed class LoanChangedConsumer(SearchIndex index, ISearchDb db, IModuleLicenseSource licenses, ILogger<LoanChangedConsumer> logger)
    : ElibConsumer<LoanChanged>(licenses, logger)
{
    protected override async Task HandleAsync(LoanChanged message, ConsumeContext<LoanChanged> context)
    {
        await index.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Đọc trạng thái theo trang từ /internal của catalog, holdings, circulation (service token của search).</summary>
public sealed class HttpSearchSources(IHttpClientFactory clients) : ISearchSources
{
    public const string Catalog = "elib-catalog";
    public const string Holdings = "elib-holdings";
    public const string Circulation = "elib-circulation";

    public Task<StatePage<BibChanged>> BibsAsync(long tenantId, long after, CancellationToken ct) =>
        GetAsync<BibChanged>(Catalog, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/bibs?after={after}&take=500"), ct);

    public Task<StatePage<ItemChanged>> ItemsAsync(long tenantId, long after, CancellationToken ct) =>
        GetAsync<ItemChanged>(Holdings, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/items?after={after}&take=1000"), ct);

    public Task<StatePage<LoanChanged>> OpenLoansAsync(long tenantId, long after, CancellationToken ct) =>
        GetAsync<LoanChanged>(Circulation, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/loans/open?after={after}&take=1000"), ct);

    private async Task<StatePage<T>> GetAsync<T>(string client, string path, CancellationToken ct)
    {
        using var response = await clients.CreateClient(client).GetAsync(new Uri(path, UriKind.Relative), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StatePage<T>>(ct) ?? new StatePage<T>([], null);
    }
}
