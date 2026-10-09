using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Elib.Tenant.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Tenant.Application;

/// <summary>
/// Quản trị đơn vị — chỉ gọi trong ngữ cảnh hệ thống (endpoint [RequireSystemContext]).
/// Event được publish trước SaveChanges: với bus outbox, event nằm trong cùng transaction với dữ liệu.
/// </summary>
public sealed class TenantAdminService(
    ITenantDb db, IPublishEndpoint publisher, ICurrentActor actor, TimeProvider clock, IOptions<ProvisioningOptions> options,
    TenantDefaults defaults, PlatformAudit audit, IOptions<BrandingOptions> branding)
{
    private readonly ProvisioningOptions _options = options.Value;

    public Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct) =>
        db.InTransactionAsync(token => CreateCoreAsync(request, token), ct);

    private async Task<TenantDto> CreateCoreAsync(CreateTenantRequest request, CancellationToken ct)
    {
        var tenant = Domain.Tenant.Create(request.Code, request.Name, request.Subdomain, request.TimeZone, request.ParentOrgId);
        await EnsureUniqueAsync(tenant.Code, tenant.Subdomain, excludeId: null, ct);
        var selected = ModuleCatalog.ValidateSelection(request.Licenses.Select(l => l.ModuleCode));

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct); // cần Id thật cho license và event — cùng transaction với lần lưu dưới

        var licenses = request.Licenses
            .Where(l => selected.Contains(l.ModuleCode.Trim().ToUpperInvariant()))
            .Select(l => new TenantModuleLicense(tenant.Id, l.ModuleCode.Trim().ToUpperInvariant(), l.Status, l.ValidFrom, l.ValidTo))
            .ToList();
        db.Licenses.AddRange(licenses);
        await defaults.SeedAsync(tenant.Id, ct); // danh mục của chính service tenant — không cần chờ saga

        tenant.StartProvisioning(ExpectedServices(licenses), clock.GetUtcNow());
        var eventActor = EventFactory.Actor(actor);
        await publisher.Publish(EventFactory.Provisioned(tenant, licenses, eventActor), ct);
        if (tenant.Status == TenantState.Active) await publisher.Publish(EventFactory.Updated(tenant, eventActor), ct);
        await audit.RecordAsync(tenant, "TENANT_CREATE", $"Tạo đơn vị {tenant.Name} ({tenant.Subdomain}), phân hệ: {Modules(licenses)}", ct);
        await db.SaveChangesAsync(ct);

        return TenantQueries.ToDto(tenant, licenses);
    }

    public async Task<TenantDto> UpdateAsync(Guid publicId, UpdateTenantRequest request, CancellationToken ct)
    {
        var tenant = await LoadAsync(publicId, ct);
        var subdomain = Domain.Tenant.NormalizeSubdomain(request.Subdomain);
        await EnsureUniqueAsync(code: null, subdomain, excludeId: tenant.Id, ct);

        tenant.Update(request.Name, subdomain, request.TimeZone, request.ParentOrgId);
        await publisher.Publish(EventFactory.Updated(tenant, EventFactory.Actor(actor)), ct);
        await audit.RecordAsync(tenant, "TENANT_UPDATE", $"Sửa thông tin đơn vị: {tenant.Name}, tên miền {tenant.Subdomain}", ct);
        await db.SaveChangesAsync(ct);
        return TenantQueries.ToDto(tenant, await LicensesOfAsync(tenant.Id, ct));
    }

    /// <summary>Logo + tên ngắn (file logo upload trước qua media, purpose tenant-logo).</summary>
    public async Task<TenantDto> SetBrandingAsync(Guid publicId, SetBrandingRequest request, CancellationToken ct)
    {
        var tenant = await LoadAsync(publicId, ct);
        tenant.SetBranding(request.LogoText, request.LogoUrl, branding.Value.MediaPublicPrefix);
        await publisher.Publish(EventFactory.Updated(tenant, EventFactory.Actor(actor)), ct);
        await audit.RecordAsync(tenant, "TENANT_BRANDING",
            (tenant.LogoUrl is null ? "Bỏ logo" : "Đổi logo") + $", tên ngắn: {tenant.LogoText ?? "(theo tên đơn vị)"}", ct);
        await db.SaveChangesAsync(ct);
        return TenantQueries.ToDto(tenant, await LicensesOfAsync(tenant.Id, ct));
    }

    public async Task<TenantDto> SuspendAsync(Guid publicId, string? reason, CancellationToken ct)
    {
        var tenant = await LoadAsync(publicId, ct);
        tenant.Suspend();
        var eventActor = EventFactory.Actor(actor);
        await publisher.Publish(new TenantSuspended { TenantId = tenant.Id, Actor = eventActor, Reason = reason }, ct);
        await publisher.Publish(EventFactory.Updated(tenant, eventActor), ct);
        await audit.RecordAsync(tenant, "TENANT_SUSPEND", "Tạm ngưng đơn vị" + (string.IsNullOrWhiteSpace(reason) ? "" : $". Lý do: {reason.Trim()}"), ct);
        await db.SaveChangesAsync(ct);
        return TenantQueries.ToDto(tenant, await LicensesOfAsync(tenant.Id, ct));
    }

    public async Task<TenantDto> ActivateAsync(Guid publicId, CancellationToken ct)
    {
        var tenant = await LoadAsync(publicId, ct);
        tenant.Activate();
        await publisher.Publish(EventFactory.Updated(tenant, EventFactory.Actor(actor)), ct);
        await audit.RecordAsync(tenant, "TENANT_ACTIVATE", "Kích hoạt lại đơn vị", ct);
        await db.SaveChangesAsync(ct);
        return TenantQueries.ToDto(tenant, await LicensesOfAsync(tenant.Id, ct));
    }

    /// <summary>Thay toàn bộ tập license (bán thêm, gia hạn, ngừng). Module bỏ khỏi danh sách bị xoá mềm.</summary>
    public async Task<TenantDto> SetLicensesAsync(Guid publicId, SetLicensesRequest request, CancellationToken ct)
    {
        var tenant = await LoadAsync(publicId, ct);
        var selected = ModuleCatalog.ValidateSelection(request.Licenses.Select(l => l.ModuleCode));
        var current = await LicensesOfAsync(tenant.Id, ct);

        foreach (var license in current.Where(l => !selected.Contains(l.ModuleCode))) db.Licenses.Remove(license);
        var result = new List<TenantModuleLicense>();
        foreach (var input in request.Licenses)
        {
            var code = input.ModuleCode.Trim().ToUpperInvariant();
            var existing = current.FirstOrDefault(l => l.ModuleCode == code);
            if (existing is null)
            {
                existing = new TenantModuleLicense(tenant.Id, code, input.Status, input.ValidFrom, input.ValidTo);
                db.Licenses.Add(existing);
            }
            else
            {
                existing.Set(input.Status, input.ValidFrom, input.ValidTo);
            }
            result.Add(existing);
        }

        tenant.BumpVersion();
        await publisher.Publish(EventFactory.LicenseChanged(tenant, result, EventFactory.Actor(actor)), ct);
        await audit.RecordAsync(tenant, "TENANT_LICENSES", $"Cập nhật phân hệ: {Modules(result)}", ct);
        await db.SaveChangesAsync(ct);
        return TenantQueries.ToDto(tenant, result);
    }

    /// <summary>Chạy lại khởi tạo sau khi lỗi/timeout — seed ở các service phải idempotent.</summary>
    public async Task<TenantDto> RetryProvisioningAsync(Guid publicId, CancellationToken ct)
    {
        var tenant = await LoadAsync(publicId, ct);
        var licenses = await LicensesOfAsync(tenant.Id, ct);
        tenant.StartProvisioning(ExpectedServices(licenses), clock.GetUtcNow());

        var eventActor = EventFactory.Actor(actor);
        await publisher.Publish(EventFactory.Provisioned(tenant, licenses, eventActor), ct);
        await publisher.Publish(EventFactory.Updated(tenant, eventActor), ct);
        await audit.RecordAsync(tenant, "TENANT_PROVISION_RETRY", "Chạy lại khởi tạo đơn vị", ct);
        await db.SaveChangesAsync(ct);
        return TenantQueries.ToDto(tenant, licenses);
    }

    /// <summary>
    /// Phát lại trạng thái hiện tại của đơn vị (TenantUpdated + ModuleLicenseChanged, đúng version đang có) để service dựng
    /// lại bản sao — cho service mới thêm vào nền tảng sau khi đơn vị đã tạo, hoặc bản sao bị lệch. Bản sao đã khớp bỏ qua (cùng version).
    /// </summary>
    public async Task<TenantDto> ResyncAsync(Guid publicId, CancellationToken ct)
    {
        var tenant = await LoadAsync(publicId, ct);
        var licenses = await LicensesOfAsync(tenant.Id, ct);
        var eventActor = EventFactory.Actor(actor);
        await publisher.Publish(EventFactory.Updated(tenant, eventActor), ct);
        await publisher.Publish(EventFactory.LicenseChanged(tenant, licenses, eventActor), ct);
        await audit.RecordAsync(tenant, "TENANT_RESYNC", "Đồng bộ lại thông tin đơn vị sang các service", ct);
        await db.SaveChangesAsync(ct); // đẩy outbox
        return TenantQueries.ToDto(tenant, licenses);
    }

    /// <summary>
    /// Chép bổ sung danh mục mặc định còn thiếu — cho đơn vị tạo trước khi có danh mục, hoặc khi
    /// <see cref="ParameterCatalog"/> có tham số mới. Trả về số mục đã thêm.
    /// </summary>
    public async Task<int> SeedDefaultsAsync(Guid publicId, CancellationToken ct)
    {
        var tenant = await LoadAsync(publicId, ct);
        var added = await defaults.SeedAsync(tenant.Id, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }

    private static string Modules(IEnumerable<TenantModuleLicense> licenses)
    {
        var codes = licenses.Select(l => l.Status == LicenseStatus.Active ? l.ModuleCode : $"{l.ModuleCode} ({l.Status})").Order(StringComparer.Ordinal).ToList();
        return codes.Count == 0 ? "(không có)" : string.Join(", ", codes);
    }

    /// <summary>(Service nền tảng ∪ service của module được mua) ∩ service đang triển khai.</summary>
    internal IEnumerable<string> ExpectedServices(IEnumerable<TenantModuleLicense> licenses)
    {
        var deployed = new HashSet<string>(_options.DeployedServices.Select(s => s.Trim().ToLowerInvariant()));
        return ModuleCatalog.PlatformServices
            .Concat(licenses.Select(l => ModuleCatalog.Get(l.ModuleCode).Service))
            .Where(deployed.Contains);
    }

    private async Task<Domain.Tenant> LoadAsync(Guid publicId, CancellationToken ct) =>
        await db.Tenants.Include(t => t.ProvisioningSteps).FirstOrDefaultAsync(t => t.PublicId == publicId, ct)
        ?? throw new NotFoundException("đơn vị", publicId);

    private Task<List<TenantModuleLicense>> LicensesOfAsync(long tenantId, CancellationToken ct) =>
        db.Licenses.IgnoreQueryFilters([ElibQueryFilters.Tenant]).Where(l => l.TenantId == tenantId).ToListAsync(ct);

    private async Task EnsureUniqueAsync(string? code, string subdomain, long? excludeId, CancellationToken ct)
    {
        var others = db.Tenants.IgnoreQueryFilters([ElibQueryFilters.SoftDelete]).Where(t => excludeId == null || t.Id != excludeId);
        if (code is not null && await others.AnyAsync(t => t.Code == code, ct))
            throw new ConflictException("TENANT_CODE_EXISTS", $"Mã đơn vị '{code}' đã tồn tại.");
        if (await others.AnyAsync(t => t.Subdomain == subdomain, ct))
            throw new ConflictException("TENANT_SUBDOMAIN_EXISTS", $"Tên miền con '{subdomain}' đã được dùng.");
    }
}
