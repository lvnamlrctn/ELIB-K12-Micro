using Elib.BuildingBlocks.Domain;
using Elib.Tenant.Domain;

namespace Elib.Tenant.Tests.Domain;

public sealed class TenantDomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 8, 1, 0, 0, TimeSpan.Zero);

    private static Elib.Tenant.Domain.Tenant NewTenant() => Elib.Tenant.Domain.Tenant.Create("th-abc", "Trường ABC", "Truong-ABC", null, null);

    [Fact]
    public void Create_normalizes_code_and_subdomain()
    {
        var t = NewTenant();

        Assert.Equal("TH-ABC", t.Code);
        Assert.Equal("truong-abc", t.Subdomain);
        Assert.Equal("Asia/Ho_Chi_Minh", t.TimeZone);
        Assert.Equal(TenantState.Provisioning, t.Status);
    }

    [Theory]
    [InlineData("-abc", "TENANT_SUBDOMAIN_INVALID")]
    [InlineData("a", "TENANT_SUBDOMAIN_INVALID")]
    [InlineData("truong_abc", "TENANT_SUBDOMAIN_INVALID")]
    [InlineData("admin", "TENANT_SUBDOMAIN_RESERVED")]
    public void Invalid_subdomain_is_rejected(string subdomain, string code)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Elib.Tenant.Domain.Tenant.Create("TH1", "X", subdomain, null, null));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void Invalid_time_zone_is_rejected()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Elib.Tenant.Domain.Tenant.Create("TH1", "X", "th1", "Mars/Olympus", null));
        Assert.Equal("TENANT_TIMEZONE_INVALID", ex.Code);
    }

    [Fact]
    public void No_expected_services_activates_immediately()
    {
        var t = NewTenant();
        t.StartProvisioning([], T0);

        Assert.Equal(TenantState.Active, t.Status);
    }

    [Fact]
    public void All_services_seeded_activates()
    {
        var t = NewTenant();
        t.StartProvisioning(["identity", "patron"], T0);

        Assert.False(t.RecordSeeded("identity", true, null, T0));
        Assert.Equal(TenantState.Provisioning, t.Status);
        Assert.True(t.RecordSeeded("PATRON", true, null, T0));
        Assert.Equal(TenantState.Active, t.Status);
    }

    [Fact]
    public void Any_failure_marks_provisioning_failed_with_reason()
    {
        var t = NewTenant();
        t.StartProvisioning(["identity", "patron"], T0);

        Assert.True(t.RecordSeeded("patron", false, "thiếu loại bạn đọc mặc định", T0));
        Assert.Equal(TenantState.ProvisioningFailed, t.Status);
        Assert.Contains("patron: thiếu loại bạn đọc mặc định", t.ProvisioningError, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_or_late_responses_are_ignored()
    {
        var t = NewTenant();
        t.StartProvisioning(["identity"], T0);

        Assert.False(t.RecordSeeded("catalog", true, null, T0));
        Assert.True(t.TimeOutProvisioning(T0.AddMinutes(16), TimeSpan.FromMinutes(15)));
        Assert.False(t.RecordSeeded("identity", true, null, T0.AddMinutes(17)));
        Assert.Equal(TenantState.ProvisioningFailed, t.Status);
        Assert.Contains("identity", t.ProvisioningError, StringComparison.Ordinal);
    }

    [Fact]
    public void Timeout_does_not_fire_early()
    {
        var t = NewTenant();
        t.StartProvisioning(["identity"], T0);

        Assert.False(t.TimeOutProvisioning(T0.AddMinutes(14), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Restart_reuses_steps_without_duplicates()
    {
        var t = NewTenant();
        t.StartProvisioning(["identity", "patron"], T0);
        t.RecordSeeded("identity", true, null, T0);
        t.RecordSeeded("patron", false, "lỗi", T0);

        t.StartProvisioning(["identity", "patron", "patron"], T0.AddMinutes(1));

        Assert.Equal(TenantState.Provisioning, t.Status);
        Assert.Equal(2, t.ProvisioningSteps.Count);
        Assert.All(t.ProvisioningSteps, s => Assert.Equal(StepStatus.Pending, s.Status));
        Assert.Null(t.ProvisioningError);
    }

    [Fact]
    public void Cannot_restart_after_active()
    {
        var t = NewTenant();
        t.StartProvisioning([], T0);

        Assert.Throws<ConflictException>(() => t.StartProvisioning(["identity"], T0));
    }

    [Fact]
    public void Suspend_and_activate_follow_lifecycle_and_bump_version()
    {
        var t = NewTenant();
        Assert.Throws<ConflictException>(t.Suspend);

        t.StartProvisioning([], T0);
        var v = t.Version;
        t.Suspend();
        t.Activate();

        Assert.Equal(TenantState.Active, t.Status);
        Assert.Equal(v + 2, t.Version);
        Assert.Throws<ConflictException>(t.Activate);
    }

    [Fact]
    public void Module_dependencies_are_enforced()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => ModuleCatalog.ValidateSelection(["circulation", "CATALOG"]));
        Assert.Equal("MODULE_DEPENDENCY_MISSING", ex.Code);
        Assert.Contains("CIRCULATION cần HOLDINGS", ex.Message, StringComparison.Ordinal);

        Assert.Equal(3, ModuleCatalog.ValidateSelection(["CIRCULATION", "CATALOG", "HOLDINGS"]).Count);
        Assert.Throws<BusinessRuleException>(() => ModuleCatalog.ValidateSelection(["NOPE"]));
    }

    [Theory]
    [InlineData(LicenseStatus.Active, null, null, true)]
    [InlineData(LicenseStatus.Trial, "2026-10-01", "2026-10-31", true)]
    [InlineData(LicenseStatus.Active, "2026-11-01", null, false)]
    [InlineData(LicenseStatus.Active, null, "2026-10-07", false)]
    [InlineData(LicenseStatus.Suspended, null, null, false)]
    [InlineData(LicenseStatus.Expired, null, null, false)]
    public void License_effectiveness(LicenseStatus status, string? from, string? to, bool expected)
    {
        var license = new TenantModuleLicense(1, "AI", status,
            from is null ? null : DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture),
            to is null ? null : DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expected, license.IsEffectiveOn(new DateOnly(2026, 10, 8)));
    }

    [Fact]
    public void License_period_must_be_ordered()
    {
        Assert.Throws<BusinessRuleException>(() =>
            new TenantModuleLicense(1, "AI", LicenseStatus.Active, new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1)));
    }
}
