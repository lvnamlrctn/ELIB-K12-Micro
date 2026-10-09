using Elib.BuildingBlocks.Domain;
using Elib.Identity.Domain;

namespace Elib.Identity.Tests.Domain;

public sealed class AccountDomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 8, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Five_failures_lock_for_fifteen_minutes()
    {
        var c = new Credentials();
        for (var i = 0; i < Credentials.MaxFailedAttempts - 1; i++) c.RecordFailure(T0);
        Assert.False(c.IsLockedOut(T0));

        c.RecordFailure(T0);
        Assert.True(c.IsLockedOut(T0.AddMinutes(14)));
        Assert.False(c.IsLockedOut(T0.AddMinutes(16)));
    }

    [Fact]
    public void Success_resets_failures()
    {
        var c = new Credentials();
        for (var i = 0; i < 4; i++) c.RecordFailure(T0);
        c.RecordSuccess(T0);
        c.RecordFailure(T0);

        Assert.False(c.IsLockedOut(T0));
        Assert.Equal(1, c.AccessFailedCount);
        Assert.Equal(T0, c.LastLoginAt);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void Weak_passwords_are_rejected(string password)
    {
        Assert.Equal("PASSWORD_WEAK", Assert.Throws<BusinessRuleException>(() => AccountRules.EnsureStrongPassword(password)).Code);
    }

    [Fact]
    public void Username_is_normalized_and_validated()
    {
        Assert.Equal("thuthu.01", AccountRules.NormalizeUserName("  ThuThu.01 "));
        Assert.Throws<BusinessRuleException>(() => AccountRules.NormalizeUserName("ab"));
        Assert.Throws<BusinessRuleException>(() => AccountRules.NormalizeUserName("có dấu"));
    }

    [Fact]
    public void Role_permissions_are_normalized_and_validated()
    {
        var role = Role.Create("Thủ thư", null, ["circulation:Add", "CIRCULATION:add", "news:view", "*"]);
        Assert.Equal(["*", "CIRCULATION:add", "NEWS:view"], role.Permissions);

        Assert.Equal("PERMISSION_CODE_INVALID",
            Assert.Throws<BusinessRuleException>(() => Role.Create("X", null, ["khong-hop-le"])).Code);
    }

    [Fact]
    public void Changing_roles_or_status_rotates_permission_stamp()
    {
        var user = StaffUser.Create("thuthu", "Thủ thư", null, null, "hash", mustChangePassword: false);
        var s0 = user.PermissionStamp;

        user.SetRoles([Role.Create("A", null, ["NEWS:view"])]);
        var s1 = user.PermissionStamp;
        user.SetActive(false);

        Assert.NotEqual(s0, s1);
        Assert.NotEqual(s1, user.PermissionStamp);
        Assert.Equal(["NEWS:view"], user.EffectivePermissions());
    }

    [Fact]
    public void Built_in_role_cannot_be_deleted()
    {
        Assert.Throws<ConflictException>(() => Role.Create(Role.AdminRoleName, null, ["*"], isBuiltIn: true).EnsureDeletable());
    }

    [Fact]
    public void Permission_catalog_filters_by_license_and_validates_actions()
    {
        IReadOnlyList<PermissionModule> catalog =
        [
            new("ORGS", "Phòng ban", "Hệ thống", ["view", "add"]),
            new("LOANS", "Mượn trả", "Lưu thông", ["view", "add"], License: "CIRCULATION"),
        ];
        Assert.Equal(["ORGS"], PermissionCatalog.Available([], catalog).Select(m => m.Code));
        var licensed = PermissionCatalog.Available(["CIRCULATION"], catalog);
        Assert.Equal(["ORGS", "LOANS"], licensed.Select(m => m.Code));

        PermissionCatalog.Validate(["LOANS:add", "ORGS:view"], licensed);
        Assert.Equal("PERMISSION_UNKNOWN", Assert.Throws<BusinessRuleException>(() =>
            PermissionCatalog.Validate(["LOANS:add"], PermissionCatalog.Available([], catalog))).Code);
        Assert.Equal("PERMISSION_UNKNOWN", Assert.Throws<BusinessRuleException>(() => PermissionCatalog.Validate(["ORGS:delete"], licensed)).Code);
        Assert.All(PermissionCatalog.All, m => Assert.Matches("^[A-Z0-9_]{2,40}$", m.Code));
        Assert.Equal(PermissionCatalog.All.Count, PermissionCatalog.All.Select(m => m.Code).Distinct().Count());
    }
}
