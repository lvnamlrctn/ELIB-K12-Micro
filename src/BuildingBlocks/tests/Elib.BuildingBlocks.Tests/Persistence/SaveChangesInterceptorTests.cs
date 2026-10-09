using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Elib.BuildingBlocks.Tests.Persistence;

public sealed class SaveChangesInterceptorTests : IDisposable
{
    private readonly DbHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Insert_assigns_tenant_public_id_and_audit_fields()
    {
        _h.Actor.Set(42, ElibSubjectTypes.Staff);
        using var _ = _h.Tenant.Use(7);
        using var db = _h.Db();
        var book = new Book { Title = "T" };
        db.Books.Add(book);
        db.SaveChanges();

        Assert.Equal(7, book.TenantId);
        Assert.NotEqual(Guid.Empty, book.PublicId);
        Assert.Equal(7, book.PublicId.Version);
        Assert.Equal(42, book.CreatedBy);
        Assert.NotEqual(default, book.CreatedAt);
    }

    [Fact]
    public void Insert_without_tenant_context_is_rejected()
    {
        using var db = _h.Db();
        db.Books.Add(new Book { Title = "T" });

        Assert.Throws<TenantRequiredException>(() => db.SaveChanges());
    }

    [Fact]
    public void Insert_for_another_tenant_is_rejected()
    {
        using var _ = _h.Tenant.Use(1);
        using var db = _h.Db();
        db.Books.Add(new Book { Title = "T", TenantId = 2 });

        Assert.Throws<TenantAccessDeniedException>(() => db.SaveChanges());
    }

    [Fact]
    public void System_context_may_seed_explicit_tenant_but_not_implicit()
    {
        using var _ = _h.Tenant.UseSystem();
        using (var db = _h.Db())
        {
            db.Books.Add(new Book { Title = "seed", TenantId = 9 });
            db.SaveChanges();
        }

        using var db2 = _h.Db();
        db2.Books.Add(new Book { Title = "no tenant" });
        Assert.Throws<TenantRequiredException>(() => db2.SaveChanges());
    }

    [Fact]
    public void Changing_tenant_id_is_rejected()
    {
        _h.SeedBooks(1, "T");
        using var _ = _h.Tenant.Use(1);
        using var db = _h.Db();
        var book = db.Books.Single();
        book.TenantId = 2;

        Assert.Throws<TenantAccessDeniedException>(() => db.SaveChanges());
    }

    [Fact]
    public void Update_sets_updated_fields_and_keeps_created_fields()
    {
        _h.Actor.Set(1, ElibSubjectTypes.Staff);
        _h.SeedBooks(1, "T");
        _h.Actor.Set(2, ElibSubjectTypes.Staff);
        using var _ = _h.Tenant.Use(1);
        using (var db = _h.Db())
        {
            var book = db.Books.Single();
            book.Title = "T2";
            book.CreatedBy = 999;
            db.SaveChanges();
        }

        using var check = _h.Db();
        var saved = check.Books.Single();
        Assert.Equal(1, saved.CreatedBy);
        Assert.Equal(2, saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Fact]
    public void Delete_becomes_soft_delete_and_is_hidden()
    {
        _h.Actor.Set(5, ElibSubjectTypes.Staff);
        _h.SeedBooks(1, "T");
        using var _ = _h.Tenant.Use(1);
        using (var db = _h.Db())
        {
            db.Books.Remove(db.Books.Single());
            db.SaveChanges();
        }

        using var check = _h.Db();
        Assert.Empty(check.Books.ToList());
        var deleted = check.Books.IgnoreQueryFilters([ElibQueryFilters.SoftDelete]).Single();
        Assert.True(deleted.IsDeleted);
        Assert.Equal(5, deleted.DeletedBy);
        Assert.NotNull(deleted.DeletedAt);
    }

    [Fact]
    public void Shared_entities_are_not_tenant_filtered()
    {
        using (_h.Tenant.UseSystem())
        using (var db = _h.Db())
        {
            db.MarcFields.Add(new MarcField { Tag = "245" });
            db.SaveChanges();
        }

        using var _ = _h.Tenant.Use(3);
        using var check = _h.Db();
        Assert.Single(check.MarcFields.ToList());
    }
}
