using System.Globalization;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Patron.Application;

namespace Elib.Patron.Infrastructure;

/// <summary>
/// GET /internal/tenants/{id}/reader-types/{readerTypeId}/usage của service circulation bằng service token của patron. Không hỏi được thì
/// không cho xoá (xoá nhầm loại đang dùng để lại điểm lưu thông/chính sách trỏ vào loại không còn).
/// </summary>
public sealed class HttpReaderTypeUsage(IHttpClientFactory clients, ITenantContext tenant) : IReaderTypeUsage
{
    public const string HttpClientName = "elib-circulation";

    public async Task<CirculationReaderTypeUsage> CirculationAsync(long readerTypeId, CancellationToken ct)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenant.RequireTenantId()}/reader-types/{readerTypeId}/usage");
        try
        {
            return await clients.CreateClient(HttpClientName).GetFromJsonAsync<CirculationReaderTypeUsage>(new Uri(path, UriKind.Relative), ct)
                ?? new CirculationReaderTypeUsage([], 0);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("CIRCULATION_UNAVAILABLE",
                "Không kiểm tra được loại bạn đọc có đang dùng ở lưu thông hay không — thử lại sau.", 503);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new BusinessRuleException("CIRCULATION_UNAVAILABLE",
                "Không kiểm tra được loại bạn đọc có đang dùng ở lưu thông hay không — thử lại sau.", 503);
        }
    }
}
