using System.Globalization;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Patron.Application;

namespace Elib.Patron.Infrastructure;

/// <summary>GET /internal/tenants/{id}/reference/{kind} của service tenant bằng service token của patron.</summary>
public sealed class HttpTenantReferences(IHttpClientFactory clients, ITenantContext tenant) : ITenantReferences
{
    public const string HttpClientName = "elib-tenant";

    public async Task<IReadOnlyList<ReferenceItem>> ListAsync(string kind, CancellationToken ct)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenant.RequireTenantId()}/reference/{Uri.EscapeDataString(kind)}");
        try
        {
            return await clients.CreateClient(HttpClientName).GetFromJsonAsync<List<ReferenceItem>>(new Uri(path, UriKind.Relative), ct) ?? [];
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("TENANT_REFERENCES_UNAVAILABLE",
                "Không đọc được danh mục đơn vị/dân tộc/trình độ ở service tenant — thử lại sau.", 503);
        }
    }
}
