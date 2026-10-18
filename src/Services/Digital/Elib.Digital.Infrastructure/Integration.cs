using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Application;
using Elib.Contracts.Events.Catalog;
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

/// <summary>Tìm biểu ghi sách in ở catalog qua /internal (service token) — ô chọn liên kết, không giữ bản sao.</summary>
public sealed class HttpCatalogBibLookup(IHttpClientFactory clients, ITenantContext tenant) : ICatalogBibLookup
{
    public const string Catalog = "elib-catalog";

    public async Task<IReadOnlyList<BibLinkOption>> SearchAsync(string? keyword, CancellationToken ct)
    {
        var url = $"internal/tenants/{tenant.RequireTenantId()}/bibs/search?q={Uri.EscapeDataString(keyword ?? "")}";
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient(Catalog).GetAsync(new Uri(url, UriKind.Relative), ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("CATALOG_UNAVAILABLE", "Không kết nối được biên mục để tìm biểu ghi liên kết — thử lại sau.", 503);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new BusinessRuleException("CATALOG_UNAVAILABLE", $"Biên mục báo lỗi {(int)response.StatusCode} khi tìm biểu ghi — thử lại sau.", 503);
            var bibs = await response.Content.ReadFromJsonAsync<List<BibChanged>>(ct) ?? [];
            return [.. bibs.Select(b => new BibLinkOption(b.BibPublicId, b.Title, b.Author, b.Publisher, b.PublishYear))];
        }
    }
}

/// <summary>Xin URL có hạn của file tài liệu số ở media qua /internal (service token) — digital đã tự kiểm quyền trước khi gọi.</summary>
public sealed class HttpMediaFileAccess(IHttpClientFactory clients, ITenantContext tenant) : IMediaFileAccess
{
    public const string Media = "elib-media";

    private sealed record DownloadResponse(string Url, DateTimeOffset? ExpiresAt);

    public async Task<EbookFileUrl> SignedUrlAsync(Guid mediaFileId, CancellationToken ct)
    {
        var url = $"internal/tenants/{tenant.RequireTenantId()}/files/{mediaFileId}/download";
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient(Media).GetAsync(new Uri(url, UriKind.Relative), ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("MEDIA_UNAVAILABLE", "Không kết nối được kho file để lấy đường dẫn đọc — thử lại sau.", 503);
        }
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound) throw new NotFoundException("File", mediaFileId);
            if (!response.IsSuccessStatusCode)
                throw new BusinessRuleException("MEDIA_UNAVAILABLE", $"Kho file báo lỗi {(int)response.StatusCode} khi lấy đường dẫn đọc — thử lại sau.", 503);
            var dto = await response.Content.ReadFromJsonAsync<DownloadResponse>(ct)
                ?? throw new BusinessRuleException("MEDIA_UNAVAILABLE", "Kho file trả về dữ liệu không hợp lệ.", 503);
            return new EbookFileUrl(dto.Url, dto.ExpiresAt);
        }
    }
}
