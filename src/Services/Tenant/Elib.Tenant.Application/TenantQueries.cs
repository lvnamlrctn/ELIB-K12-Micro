using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.Tenant.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Tenant.Application;

public sealed class TenantQueries(ITenantDb db, ITenantContext tenantContext, TimeProvider clock)
{
    public async Task<PagedResult<TenantSummaryDto>> ListAsync(string? search, TenantState? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // EF Core dịch ToLower() sang SQL lower(); string.Equals(..., StringComparison) không dịch được.
            query = query.Where(t => t.Code.ToLower().Contains(term) || t.Name.ToLower().Contains(term) || t.Subdomain.Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }
        if (status is not null) query = query.Where(t => t.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(t => t.Code).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new TenantSummaryDto(t.PublicId, t.Id, t.Code, t.Name, t.Subdomain, t.Status))
            .ToListAsync(ct);
        return new PagedResult<TenantSummaryDto>(items, total, page, pageSize);
    }

    public async Task<TenantDto> GetAsync(Guid publicId, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().Include(t => t.ProvisioningSteps).FirstOrDefaultAsync(t => t.PublicId == publicId, ct)
            ?? throw new NotFoundException("đơn vị", publicId);
        return ToDto(tenant, await LicensesAsync(tenant.Id, ct));
    }

    public async Task<IReadOnlyList<ModuleDto>> ModulesAsync(CancellationToken ct) =>
        (await db.Modules.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync(ct))
        .Select(m => new ModuleDto(m.Code, m.Package, m.Name, m.Service, m.DependsOn))
        .ToList();

    /// <summary>Module của đơn vị hiện tại (lấy từ token hoặc host qua gateway). Đơn vị bị khoá/chưa sẵn sàng → danh sách rỗng.</summary>
    public async Task<FeaturesDto> FeaturesAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.RequireTenantId();
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("đơn vị", tenantId);
        var modules = tenant.Status == TenantState.Active ? await EffectiveModulesAsync(tenant, ct) : [];
        return new FeaturesDto(tenant.Id, tenant.Code, tenant.Name, tenant.Status, modules, tenant.LogoUrl, tenant.LogoText);
    }

    /// <summary>
    /// Web App Manifest của đơn vị (monolith: PublicTenant/Manifest.json, PWA) — OPAC gắn &lt;link rel="manifest"&gt; vào đây.
    /// Đơn vị lấy từ host; icon là logo đơn vị (sizes "any": logo không được cắt theo kích thước chuẩn).
    /// </summary>
    public async Task<object> ManifestAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.RequireTenantId();
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("đơn vị", tenantId);
        object[] icons = tenant.LogoUrl is null
            ? []
            : [new { src = tenant.LogoUrl, sizes = "any", type = LogoType(tenant.LogoUrl), purpose = "any" }];
        return new
        {
            name = tenant.Name,
            short_name = tenant.LogoText ?? tenant.Name,
            description = $"Cổng tra cứu và đọc tài liệu số — {tenant.Name}",
            start_url = "/",
            scope = "/",
            display = "standalone",
            background_color = "#ffffff",
            theme_color = "#2563eb",
            lang = "vi",
            icons,
        };
    }

    private static string LogoType(string url) => Path.GetExtension(url) switch
    {
        ".jpg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "image/png",
    };

    /// <summary>Ánh xạ host → đơn vị cho gateway. Chấp nhận "truong-a" hoặc "truong-a.thuvientn.vn".</summary>
    public async Task<TenantHostDto> ByHostAsync(string host, CancellationToken ct)
    {
        var label = (host ?? "").Trim().ToLowerInvariant().Split(':')[0].Split('.')[0];
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Subdomain == label, ct)
            ?? throw new NotFoundException("đơn vị theo tên miền", label);
        var modules = tenant.Status == TenantState.Active ? await EffectiveModulesAsync(tenant, ct) : [];
        return new TenantHostDto(tenant.Id, tenant.Code, tenant.Subdomain, tenant.Status, modules);
    }

    /// <summary>Đơn vị có license hiệu lực hôm nay (theo múi giờ của đơn vị) cho module?</summary>
    public async Task<bool> IsLicensedAsync(long tenantId, string moduleCode, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || tenant.Status != TenantState.Active) return false;
        return (await EffectiveModulesAsync(tenant, ct)).Contains(moduleCode.Trim().ToUpperInvariant());
    }

    private async Task<IReadOnlyList<string>> EffectiveModulesAsync(Domain.Tenant tenant, CancellationToken ct)
    {
        var today = TodayIn(tenant.TimeZone);
        return (await LicensesAsync(tenant.Id, ct)).Where(l => l.IsEffectiveOn(today)).Select(l => l.ModuleCode).Order().ToList();
    }

    private Task<List<TenantModuleLicense>> LicensesAsync(long tenantId, CancellationToken ct) =>
        db.Licenses.AsNoTracking().IgnoreQueryFilters([ElibQueryFilters.Tenant]).Where(l => l.TenantId == tenantId).ToListAsync(ct);

    private DateOnly TodayIn(string timeZone)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var tz) ? tz : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    }

    internal static TenantDto ToDto(Domain.Tenant t, IEnumerable<TenantModuleLicense> licenses)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new TenantDto(
            t.PublicId, t.Id, t.Code, t.Name, t.Subdomain, t.TimeZone, t.ParentOrgId, t.Status, t.Version,
            t.ProvisioningStartedAt, t.ProvisioningError,
            t.ProvisioningSteps.Select(s => new ProvisioningStepDto(s.Service, s.Status, s.Error, s.UpdatedAt)).ToList(),
            licenses.OrderBy(l => l.ModuleCode, StringComparer.Ordinal)
                .Select(l => new LicenseDto(l.ModuleCode, ModuleCatalog.Get(l.ModuleCode).Name, l.Status, l.ValidFrom, l.ValidTo, l.IsEffectiveOn(today)))
                .ToList(),
            t.LogoUrl, t.LogoText);
    }
}
