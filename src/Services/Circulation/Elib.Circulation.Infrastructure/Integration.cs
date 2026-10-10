using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Messaging;
using Elib.Circulation.Application;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Patron;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Elib.Circulation.Infrastructure;

/// <summary>Bản sao bạn đọc theo event của patron (chỉ đơn vị có license CIRCULATION).</summary>
public sealed class ReaderChangedConsumer(Replicas replicas, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<ReaderChangedConsumer> logger)
    : ElibConsumer<ReaderChanged>(licenses, logger)
{
    protected override string? RequiredModule => "CIRCULATION";

    protected override async Task HandleAsync(ReaderChanged message, ConsumeContext<ReaderChanged> context)
    {
        await replicas.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Bản sao bản sách theo event của holdings.</summary>
public sealed class ItemChangedConsumer(Replicas replicas, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<ItemChangedConsumer> logger)
    : ElibConsumer<ItemChanged>(licenses, logger)
{
    protected override string? RequiredModule => "CIRCULATION";

    protected override async Task HandleAsync(ItemChanged message, ConsumeContext<ItemChanged> context)
    {
        await replicas.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Bản sao biểu ghi theo event của catalog.</summary>
public sealed class BibChangedConsumer(Replicas replicas, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<BibChangedConsumer> logger)
    : ElibConsumer<BibChanged>(licenses, logger)
{
    protected override string? RequiredModule => "CIRCULATION";

    protected override async Task HandleAsync(BibChanged message, ConsumeContext<BibChanged> context)
    {
        await replicas.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>
/// Hỏi patron/holdings/catalog qua /internal (service token) khi bản sao chưa có. Service gốc không trả lời được thì báo lỗi rõ
/// ("chưa đồng bộ") thay vì coi như không tồn tại.
/// </summary>
public sealed class HttpReplicaSources(IHttpClientFactory clients) : IReplicaSources
{
    public const string Patron = "elib-patron";
    public const string Holdings = "elib-holdings";
    public const string Catalog = "elib-catalog";

    public Task<ReaderChanged?> ReaderAsync(long tenantId, string cardNo, CancellationToken ct) =>
        GetAsync<ReaderChanged>(Patron, $"internal/tenants/{tenantId}/readers/by-card/{Uri.EscapeDataString(cardNo)}", "bạn đọc", ct);

    public Task<ItemChanged?> ItemAsync(long tenantId, string barcode, CancellationToken ct) =>
        GetAsync<ItemChanged>(Holdings, $"internal/tenants/{tenantId}/items/by-barcode/{Uri.EscapeDataString(barcode)}", "bản sách", ct);

    public Task<BibChanged?> BibAsync(long tenantId, long mfn, CancellationToken ct) =>
        GetAsync<BibChanged>(Catalog, string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/bibs/{mfn}"), "biểu ghi", ct);

    private async Task<T?> GetAsync<T>(string client, string path, string what, CancellationToken ct) where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient(client).GetAsync(new Uri(path, UriKind.Relative), ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("REPLICA_UNAVAILABLE", $"Chưa có dữ liệu {what} ở lưu thông và không kết nối được service gốc — thử lại sau.", 503);
        }
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new BusinessRuleException("REPLICA_UNAVAILABLE", $"Chưa có dữ liệu {what} ở lưu thông và service gốc báo lỗi {(int)response.StatusCode} — thử lại sau.", 503);
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
    }
}
