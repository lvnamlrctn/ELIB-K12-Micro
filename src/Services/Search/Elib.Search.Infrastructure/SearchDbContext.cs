using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Search.Application;
using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace Elib.Search.Infrastructure;

public sealed class SearchDbContext(DbContextOptions<SearchDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ISearchDb
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchBib>(e =>
        {
            e.ToTable("search_bibs");
            e.Property(x => x.MaterialType).HasMaxLength(250);
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.OtherAuthors).HasMaxLength(2000);
            e.Property(x => x.Publisher).HasMaxLength(500);
            e.Property(x => x.PublishPlace).HasMaxLength(250);
            e.Property(x => x.PublishYear).HasMaxLength(4);
            e.Property(x => x.Isbns).HasMaxLength(500);
            e.Property(x => x.Ddc).HasMaxLength(50);
            e.Property(x => x.Cutter).HasMaxLength(50);
            e.Property(x => x.Keywords).HasMaxLength(2000);
            e.Property(x => x.Language).HasMaxLength(20);
            e.Property(x => x.Summary).HasMaxLength(4000);
            e.Property(x => x.Edition).HasMaxLength(250);
            e.Property(x => x.PhysicalDescription).HasMaxLength(250);
            e.Property(x => x.Series).HasMaxLength(500);
            e.Property(x => x.CoverUrl).HasMaxLength(1000);
            e.Property(x => x.TitleFold).HasMaxLength(1000);
            e.Property(x => x.AuthorFold).HasMaxLength(2000);
            e.Property(x => x.PublisherFold).HasMaxLength(500);
            e.Property(x => x.KeywordFold).HasMaxLength(2000);
            e.Property(x => x.IsbnKey).HasMaxLength(500);
            e.Property(x => x.SearchText).HasMaxLength(8000);
            e.HasIndex(x => new { x.TenantId, x.BibPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Mfn });
            e.HasIndex(x => new { x.TenantId, x.Status, x.Deleted, x.Year });
            // Chỉ mục trigram (pg_trgm) cho search_text/title_fold/author_fold tạo bằng SQL trong migration — chỉ PostgreSQL.
        });

        modelBuilder.Entity<SearchItem>(e =>
        {
            e.ToTable("search_items");
            e.Property(x => x.Barcode).HasMaxLength(50);
            e.Property(x => x.BarcodeKey).HasMaxLength(50);
            e.Property(x => x.StoreName).HasMaxLength(250);
            e.Property(x => x.Status).HasMaxLength(1);
            e.HasIndex(x => new { x.TenantId, x.ItemPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Mfn });
            e.HasIndex(x => new { x.TenantId, x.BarcodeKey });
        });

        modelBuilder.Entity<SearchQuery>(e =>
        {
            e.ToTable("search_queries");
            e.Property(x => x.Text).HasMaxLength(SearchQuery.MaxTextLength);
            e.Property(x => x.TextFold).HasMaxLength(SearchQuery.MaxTextLength);
            e.HasIndex(x => new { x.TenantId, x.QueryId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.At });
        });

        modelBuilder.Entity<SearchSyncState>(e =>
        {
            e.ToTable("search_sync_states");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).ValueGeneratedNever();
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Error).HasMaxLength(1000);
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Đơn vị mới hoặc search vừa triển khai (seeder chạy khi dựng bản sao đơn vị): chỉ mục còn trống thì dựng từ catalog/holdings/
/// circulation. Lỗi không chặn khởi tạo đơn vị — quản trị bấm "Dựng lại chỉ mục" sau.
/// </summary>
public sealed partial class SearchTenantSeeder(SearchDbContext db, IndexRebuilder rebuilder, ILogger<SearchTenantSeeder> logger) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        if (await db.Set<SearchBib>().AnyAsync(cancellationToken)) return;
        try
        {
            var status = await rebuilder.RebuildAsync(cancellationToken);
            LogBuilt(logger, tenant.TenantId, status.Bibs, status.Items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, tenant.TenantId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: đã dựng chỉ mục tra cứu ({Bibs} biểu ghi, {Items} bản sách)")]
    private static partial void LogBuilt(ILogger logger, long tenantId, int bibs, int items);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đơn vị {TenantId}: chưa dựng được chỉ mục tra cứu — dùng \"Dựng lại chỉ mục\"")]
    private static partial void LogFailed(ILogger logger, Exception exception, long tenantId);
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class SearchDbContextDesignFactory : IDesignTimeDbContextFactory<SearchDbContext>
{
    public SearchDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SearchDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_search")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
