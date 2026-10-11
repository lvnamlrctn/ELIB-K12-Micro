using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Messaging;
using Elib.Contracts.Events.Patron;
using Elib.Identity.Application;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Elib.Identity.Infrastructure;

/// <summary>Hồ sơ tài khoản bạn đọc theo event của patron (mọi đơn vị — bạn đọc không thuộc phân hệ bán riêng).</summary>
public sealed class ReaderChangedConsumer(ReaderAccountService accounts, IIdentityDb db, IModuleLicenseSource licenses, ILogger<ReaderChangedConsumer> logger)
    : ElibConsumer<ReaderChanged>(licenses, logger)
{
    protected override async Task HandleAsync(ReaderChanged message, ConsumeContext<ReaderChanged> context)
    {
        await accounts.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>GET /internal/tenants/{id}/readers/{publicId} của patron bằng service token của identity.</summary>
public sealed class HttpReaderSource(IHttpClientFactory clients) : IReaderSource
{
    public const string HttpClientName = "elib-patron";

    public async Task<ReaderChanged?> ReaderAsync(long tenantId, Guid readerPublicId, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient(HttpClientName).GetAsync(
                new Uri(string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/readers/{readerPublicId}"), UriKind.Relative), ct);
        }
        catch (HttpRequestException)
        {
            throw Unavailable("không kết nối được");
        }
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) throw Unavailable($"báo lỗi {(int)response.StatusCode}");
            return await response.Content.ReadFromJsonAsync<ReaderChanged>(ct);
        }
    }

    private static BusinessRuleException Unavailable(string why) =>
        new("REPLICA_UNAVAILABLE", $"Chưa có dữ liệu bạn đọc ở identity và service bạn đọc {why} — thử lại sau.", 503);
}
