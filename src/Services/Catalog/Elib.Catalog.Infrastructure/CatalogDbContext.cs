using System.Text.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;
using Elib.Contracts.Events.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elib.Catalog.Infrastructure;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trường MARC chỉ sống trong cột JSON — không phải entity.
        modelBuilder.Ignore<MarcField>();
        modelBuilder.Ignore<MarcSubfield>();

        modelBuilder.Entity<BibType>(e =>
        {
            e.ToTable("bib_types");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.RecordType).HasMaxLength(1);
            e.Property(x => x.BibLevel).HasMaxLength(1);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<Worksheet>(e =>
        {
            e.ToTable("worksheets");
            e.Property(x => x.Name).HasMaxLength(250);
            e.MarcJson(x => x.Fields);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("is_deleted = false");
            e.HasOne<BibType>().WithMany().HasForeignKey(x => x.BibTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bib>(e =>
        {
            e.ToTable("bibs");
            e.Ignore(x => x.IsbnList);
            e.Property(x => x.Leader).HasMaxLength(24);
            e.MarcJson(x => x.Fields);
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.Publisher).HasMaxLength(500);
            e.Property(x => x.PublishYear).HasMaxLength(4);
            e.Property(x => x.Isbns).HasMaxLength(1000);
            e.Property(x => x.Ddc).HasMaxLength(50);
            e.Property(x => x.Keywords).HasMaxLength(2000);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.SearchText).HasMaxLength(4000);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.TenantId, x.BibTypeId });
            e.HasIndex(x => new { x.TenantId, x.Ddc });
            e.HasOne<BibType>().WithMany().HasForeignKey(x => x.BibTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Worksheet>().WithMany().HasForeignKey(x => x.WorksheetId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

internal static class MarcJsonMapping
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Danh sách trường MARC lưu nguyên một cột JSON (jsonb trên PostgreSQL) — đọc/ghi cả biểu ghi một lần, giữ thứ tự trường
    /// và trường con. Tìm kiếm không đọc cột này (dùng cột tóm tắt; tra cứu OPAC ở service search).
    /// </summary>
    public static void MarcJson<T>(this EntityTypeBuilder<T> e, System.Linq.Expressions.Expression<Func<T, List<MarcField>>> property) where T : class =>
        e.Property(property)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<MarcField>>(v, Json) ?? new List<MarcField>(),
                new ValueComparer<List<MarcField>>(
                    (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                    v => JsonSerializer.Serialize(v, Json).GetHashCode(StringComparison.Ordinal),
                    v => JsonSerializer.Deserialize<List<MarcField>>(JsonSerializer.Serialize(v, Json), Json)!));
}

/// <summary>Đơn vị mới có phân hệ Biên mục: tạo loại biểu ghi và biểu mẫu biên mục mặc định (saga khởi tạo chờ catalog báo TenantSeeded).</summary>
public sealed class CatalogTenantSeeder(BibTypeResource bibTypes, WorksheetResource worksheets) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        await bibTypes.AddMissingDefaultsAsync(cancellationToken);
        await worksheets.AddMissingDefaultsAsync(cancellationToken);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class CatalogDbContextDesignFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_catalog")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
