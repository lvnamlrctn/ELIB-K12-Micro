using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Elib.BuildingBlocks.Tests.Persistence;

public sealed class TenantIsolationTests : IDisposable
{
    private readonly DbHarness _h = new();

    public TenantIsolationTests()
    {
        _h.SeedBooks(1, "A1", "A2");
        _h.SeedBooks(2, "B1");
    }

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Query_returns_only_current_tenant_rows()
    {
        using var _ = _h.Tenant.Use(1);
        using var db = _h.Db();

        Assert.Equal(["A1", "A2"], db.Books.OrderBy(b => b.Title).Select(b => b.Title).ToArray());
    }

    [Fact]
    public void Unresolved_context_sees_nothing()
    {
        using var db = _h.Db();

        Assert.Empty(db.Books.ToList());
    }

    [Fact]
    public void System_context_sees_nothing_until_tenant_filter_is_ignored_explicitly()
    {
        using var _ = _h.Tenant.UseSystem();
        using var db = _h.Db();

        Assert.Empty(db.Books.ToList());
        Assert.Equal(3, db.Books.IgnoreQueryFilters([ElibQueryFilters.Tenant]).Count());
    }

    [Fact]
    public void ReadAcross_returns_rows_of_allowed_scope()
    {
        _h.Tenant.Initialize(1, isSystem: false, allowedReadScope: [2]);
        using var _ = _h.Tenant.ReadAcross([1, 2]);
        using var db = _h.Db();

        Assert.Equal(3, db.Books.Count());
    }

    [Fact]
    public void ReadAcross_outside_allowed_scope_is_denied()
    {
        _h.Tenant.Initialize(1, isSystem: false, allowedReadScope: [2]);

        Assert.Throws<TenantAccessDeniedException>(() => _h.Tenant.ReadAcross([2, 3]));
    }

    [Fact]
    public void Writes_are_rejected_while_reading_across()
    {
        _h.Tenant.Initialize(1, isSystem: false, allowedReadScope: [2]);
        using var _ = _h.Tenant.ReadAcross([1, 2]);
        using var db = _h.Db();
        db.Books.Add(new Book { Title = "X" });

        Assert.Throws<TenantAccessDeniedException>(() => db.SaveChanges());
    }

    [Fact]
    public void Use_restores_previous_context_on_dispose()
    {
        using (_h.Tenant.Use(1))
        {
            using (_h.Tenant.Use(2)) Assert.Equal(2, _h.Tenant.TenantId);
            Assert.Equal(1, _h.Tenant.TenantId);
        }
        Assert.Null(_h.Tenant.TenantId);
    }
}
