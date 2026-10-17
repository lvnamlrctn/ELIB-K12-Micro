using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Messaging;
using Elib.Digital.Application;
using Elib.Contracts.Events.Patron;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Elib.Digital.Infrastructure;

/// <summary>Bản sao bạn đọc theo event của patron (chỉ đơn vị có license DIGITAL).</summary>
public sealed class ReaderChangedConsumer(Replicas replicas, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<ReaderChangedConsumer> logger)
    : ElibConsumer<ReaderChanged>(licenses, logger)
{
    protected override string? RequiredModule => "DIGITAL";

    protected override async Task HandleAsync(ReaderChanged message, ConsumeContext<ReaderChanged> context)
    {
        await replicas.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Hỏi patron qua /internal (service token) khi bản sao chưa có.</summary>
public sealed class HttpReplicaSources(IHttpClientFactory clients) : IReplicaSources
{
    public const string Patron = "elib-patron";

    public Task<ReaderChanged?> ReaderAsync(long tenantId, string cardNo, CancellationToken ct) =>
        GetAsync<ReaderChanged>(Patron, $"internal/tenants/{tenantId}/readers/by-card/{Uri.EscapeDataString(cardNo)}", "bạn đọc", ct);

    private async Task<T?> GetAsync<T>(string client, string path, string what, CancellationToken ct) where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient(client).GetAsync(new Uri(path, UriKind.Relative), ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("REPLICA_UNAVAILABLE", $"Chưa có dữ liệu {what} ở tài liệu số và không kết nối được service gốc — thử lại sau.", 503);
        }
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new BusinessRuleException("REPLICA_UNAVAILABLE", $"Chưa có dữ liệu {what} ở tài liệu số và service gốc báo lỗi {(int)response.StatusCode} — thử lại sau.", 503);
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
    }
}
