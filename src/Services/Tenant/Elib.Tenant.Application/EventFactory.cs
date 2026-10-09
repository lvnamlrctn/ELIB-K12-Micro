using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using Elib.Tenant.Domain;

namespace Elib.Tenant.Application;

/// <summary>Dựng integration event từ trạng thái domain — một chỗ duy nhất để giữ contract nhất quán.</summary>
internal static class EventFactory
{
    public static EventActor Actor(ICurrentActor actor) => new(actor.Id, actor.Kind);

    public static IReadOnlyList<ModuleLicense> Licenses(IEnumerable<TenantModuleLicense> licenses) =>
        licenses.OrderBy(l => l.ModuleCode, StringComparer.Ordinal)
            .Select(l => new ModuleLicense(l.ModuleCode, l.Status.ToString(), l.ValidFrom, l.ValidTo))
            .ToList();

    public static TenantProvisioned Provisioned(Domain.Tenant t, IEnumerable<TenantModuleLicense> licenses, EventActor actor) => new()
    {
        TenantId = t.Id,
        Actor = actor,
        Code = t.Code,
        Name = t.Name,
        Subdomain = t.Subdomain,
        TimeZone = t.TimeZone,
        ParentOrgId = t.ParentOrgId,
        Modules = Licenses(licenses),
    };

    public static TenantUpdated Updated(Domain.Tenant t, EventActor actor) => new()
    {
        TenantId = t.Id,
        Actor = actor,
        Code = t.Code,
        Name = t.Name,
        Subdomain = t.Subdomain,
        TimeZone = t.TimeZone,
        ParentOrgId = t.ParentOrgId,
        Status = Map(t.Status),
        SourceVersion = t.Version,
    };

    public static ModuleLicenseChanged LicenseChanged(Domain.Tenant t, IEnumerable<TenantModuleLicense> licenses, EventActor actor) => new()
    {
        TenantId = t.Id,
        Actor = actor,
        Modules = Licenses(licenses),
        SourceVersion = t.Version,
    };

    private static TenantStatus Map(TenantState state) => state switch
    {
        TenantState.Provisioning => TenantStatus.Provisioning,
        TenantState.Active => TenantStatus.Active,
        TenantState.Suspended => TenantStatus.Suspended,
        TenantState.ProvisioningFailed => TenantStatus.ProvisioningFailed,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };
}
