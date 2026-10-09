using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Elib.BuildingBlocks.Tests.Persistence;

public sealed class Book : TenantEntity
{
    public string Title { get; set; } = "";
}

/// <summary>Dữ liệu dùng chung, không thuộc đơn vị nào.</summary>
public sealed class MarcField : AuditableEntity
{
    public string Tag { get; set; } = "";
}

public sealed class TestDb(DbContextOptions<TestDb> options, ITenantContext tenant) : ElibDbContext(options, tenant)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<MarcField> MarcFields => Set<MarcField>();
}

/// <summary>SQLite in-memory dùng chung một connection để dữ liệu sống suốt test.</summary>
public sealed class DbHarness : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TenantContext Tenant { get; } = new();
    public CurrentActor Actor { get; } = new();

    public DbHarness()
    {
        _connection.Open();
        using var db = Db();
        db.Database.EnsureCreated();
    }

    public TestDb Db() => new(
        new DbContextOptionsBuilder<TestDb>()
            .UseSqlite(_connection)
            .AddInterceptors(new ElibSaveChangesInterceptor(Tenant, Actor, TimeProvider.System))
            .Options,
        Tenant);

    public void SeedBooks(long tenantId, params string[] titles)
    {
        using var scope = Tenant.Use(tenantId);
        using var db = Db();
        db.Books.AddRange(titles.Select(t => new Book { Title = t }));
        db.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();
}
