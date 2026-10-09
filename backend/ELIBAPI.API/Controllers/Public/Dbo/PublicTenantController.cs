using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicTenantController : PublicBaseController
{
    private readonly IPublicTenantRepository _repo;
    private readonly IMinioService _minio;
    private readonly IStringLocalizer<SharedResource> _loc;

    public PublicTenantController(
        IPublicTenantRepository repo,
        IMinioService minio,
        IStringLocalizer<SharedResource> loc)
    {
        _repo  = repo;
        _minio = minio;
        _loc   = loc;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicTenantSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicTenantResponse>>.Ok(new PagedResult<PublicTenantResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicTenantSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicTenantResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    [HttpGet("ResolveByHost")]
    public async Task<IActionResult> ResolveByHost([FromQuery] string host)
    {
        var tenant = await _repo.GetByHostAsync(host);
        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found", 404));

        return Ok(ApiResponse<ResolveByHostResponse>.Ok(new ResolveByHostResponse
        {
            TenantId = tenant.PublicId,
            Code     = tenant.Code,
            Name     = tenant.Name,
            LogoText = tenant.LogoText,
            LogoUrl  = ResolveImageUrl(tenant.LogoUrl),
        }));
    }

    /// <summary>Web App Manifest theo từng đơn vị (Đợt 22.6 — PWA). ELIB đa tenant dùng 1 domain/subdomain
    /// khác nhau nên không thể có 1 file <c>manifest.webmanifest</c> tĩnh như ELIB-LRC (đơn tenant) — FE gắn
    /// động <c>&lt;link rel="manifest"&gt;</c> trỏ vào endpoint này (xem opac/services/tenant.service.ts).
    /// <paramref name="tenantId"/> là fallback khi host không khớp tenant nào (localhost/dev) — trên domain
    /// thật, <see cref="ELIBAPI.API.Filters.PublicHostTenantFilter"/> đã tự ghi đè đúng tenant theo Host.</summary>
    [HttpGet("Manifest.json")]
    public async Task<IActionResult> Manifest([FromQuery] Guid? tenantId)
    {
        var tenant = tenantId.HasValue ? await _repo.GetByPublicIdAsync(tenantId.Value) : null;
        var name = tenant?.Name ?? "Thư viện số";
        var logoUrl = ResolveImageUrl(tenant?.LogoUrl);

        var manifest = new
        {
            name,
            short_name = tenant?.LogoText ?? name,
            description = $"Cổng tra cứu và đọc tài liệu số — {name}",
            start_url = "/",
            display = "standalone",
            scope = "/",
            background_color = "#ffffff",
            theme_color = "#2563eb",
            icons = string.IsNullOrEmpty(logoUrl)
                ? Array.Empty<object>()
                : new object[]
                {
                    new { src = logoUrl, sizes = "192x192", type = "image/png", purpose = "any" },
                    new { src = logoUrl, sizes = "512x512", type = "image/png", purpose = "any" },
                },
        };
        return new JsonResult(manifest) { ContentType = "application/manifest+json" };
    }

    private static PublicTenantResponse ToResponse(Tenant x) => new()
    {
        PublicId = x.PublicId,
        Code     = x.Code,
        Name     = x.Name,
        PortalId = x.PortalId,
        Language = x.Language,
    };

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";
}
