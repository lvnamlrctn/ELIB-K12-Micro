using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Digital.Infrastructure;

public sealed class DigitalDbContext(DbContextOptions<DigitalDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PatronReplica>(e =>
        {
            e.ToTable("patron_replicas");
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.FullName).HasMaxLength(250);
            e.Property(x => x.ReaderTypeName).HasMaxLength(250);
            e.Property(x => x.Email).HasMaxLength(250);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CardNo });
        });

        modelBuilder.Entity<EbookCollection>(e =>
        {
            e.ToTable("ebook_collections");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.Link).HasMaxLength(500);
            e.Property(x => x.CoverUrl).HasMaxLength(EbookCover.MaxUrlLength);
            e.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        modelBuilder.Entity<EbookSubject>(e =>
        {
            e.ToTable("ebook_subjects");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Ddc).HasMaxLength(50);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.Link).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        modelBuilder.Entity<EbookTopic>(e =>
        {
            e.ToTable("ebook_topics");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Keyword).HasMaxLength(500);
            e.Property(x => x.PageTitle).HasMaxLength(250);
            e.Property(x => x.MetaDescription).HasMaxLength(500);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.Ddc).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        modelBuilder.Entity<EbookDigType>(e =>
        {
            e.ToTable("ebook_dig_types");
            e.Property(x => x.Code).HasMaxLength(EbookDigType.MaxCodeLength);
            e.Property(x => x.DescriptionVn).HasMaxLength(EbookDigType.MaxDescriptionLength);
            e.Property(x => x.DescriptionEn).HasMaxLength(EbookDigType.MaxDescriptionLength);
            e.Property(x => x.Language).HasMaxLength(10);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<PolicyDigital>(e =>
        {
            e.ToTable("policy_digitals");
            e.HasIndex(x => new { x.TenantId, x.ReaderTypeId }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<PolicyDigitalByCollection>(e =>
        {
            e.ToTable("policy_digital_by_collections");
            e.HasIndex(x => new { x.TenantId, x.CollectionId, x.ReaderTypeId }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<EbookItem>(e =>
        {
            e.ToTable("ebook_items");
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.Publisher).HasMaxLength(500);
            e.Property(x => x.PublishDate).HasMaxLength(50);
            e.Property(x => x.JournalName).HasMaxLength(500);
            e.Property(x => x.Abstract).HasMaxLength(20_000);
            e.Property(x => x.Description).HasMaxLength(20_000);
            e.OwnsMany(x => x.Authors, b => b.ToJson());
            e.OwnsMany(x => x.Advisors, b => b.ToJson());
            e.OwnsMany(x => x.Identifiers, b => b.ToJson());
            e.HasIndex(x => new { x.TenantId, x.PublishYear });
            e.Property(x => x.Keyword).HasMaxLength(1000);
            e.Property(x => x.OtherTitle).HasMaxLength(1000);
            e.Property(x => x.Page).HasMaxLength(50);
            e.Property(x => x.OldAuthor).HasMaxLength(500);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.ErrorMessage).HasMaxLength(2000);
            e.Property(x => x.CoverUrl).HasMaxLength(EbookCover.MaxUrlLength);
            e.HasIndex(x => new { x.TenantId, x.CollectionId });
            e.HasIndex(x => new { x.TenantId, x.SubjectId });
            e.HasIndex(x => new { x.TenantId, x.TopicId });
            e.HasIndex(x => new { x.TenantId, x.LinkedBibPublicId });
        });

        modelBuilder.Entity<EbookFile>(e =>
        {
            e.ToTable("ebook_files");
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.FileType).HasMaxLength(50);
            e.Property(x => x.FileExt).HasMaxLength(20);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Source).HasMaxLength(250);
            e.Property(x => x.CheckSumAlgorithm).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.EbookItemId });
        });

        modelBuilder.Entity<EbookItemLoan>(e =>
        {
            e.ToTable("ebook_item_loans");
            e.Property(x => x.RecallReason).HasMaxLength(EbookItemLoan.MaxReasonLength);
            e.HasIndex(x => new { x.TenantId, x.EbookItemId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.Status });
        });

        modelBuilder.Entity<EbookItemReservation>(e =>
        {
            e.ToTable("ebook_item_reservations");
            e.HasIndex(x => new { x.TenantId, x.EbookItemId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.Status });
        });

        modelBuilder.Entity<EbookReview>(e =>
        {
            e.ToTable("ebook_reviews");
            e.Property(x => x.DisplayName).HasMaxLength(EbookReview.MaxDisplayNameLength);
            e.Property(x => x.Content).HasMaxLength(EbookReview.MaxContentLength);
            e.Property(x => x.Email).HasMaxLength(EbookReview.MaxEmailLength);
            e.Property(x => x.RejectReason).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.EbookItemId, x.Status });
        });

        modelBuilder.Entity<EbookFavorite>(e =>
        {
            e.ToTable("ebook_favorites");
            e.HasIndex(x => new { x.TenantId, x.EbookItemId, x.ReaderPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId });
        });

        modelBuilder.Entity<DocumentSubmission>(e =>
        {
            e.ToTable("document_submissions");
            e.Property(x => x.Title).HasMaxLength(DocumentSubmission.MaxTitleLength);
            e.Property(x => x.Author).HasMaxLength(DocumentSubmission.MaxAuthorLength);
            e.Property(x => x.Abstract).HasMaxLength(DocumentSubmission.MaxAbstractLength);
            e.Property(x => x.FileName).HasMaxLength(DocumentSubmission.MaxFileNameLength);
            e.Property(x => x.FileExt).HasMaxLength(DocumentSubmission.MaxFileExtLength);
            e.Property(x => x.RejectReason).HasMaxLength(DocumentSubmission.MaxReasonLength);
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<EbookReadSession>(e =>
        {
            e.ToTable("ebook_read_sessions");
            e.Property(x => x.Device).HasMaxLength(40);
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.SessionKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.StartedAt });
            e.HasIndex(x => new { x.TenantId, x.EbookItemId, x.StartedAt });
        });

        modelBuilder.Entity<EbookReadProgress>(e =>
        {
            e.ToTable("ebook_read_progress");
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.EbookItemId }).IsUnique();
        });

        modelBuilder.Entity<EbookBookmark>(e =>
        {
            e.ToTable("ebook_bookmarks");
            e.Property(x => x.Note).HasMaxLength(EbookBookmark.MaxNoteLength);
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.EbookItemId, x.Page }).IsUnique();
        });

        modelBuilder.Entity<EbookAccessLog>(e =>
        {
            e.ToTable("ebook_access_logs");
            e.Property(x => x.Kind).HasMaxLength(10);
            e.HasIndex(x => new { x.TenantId, x.At });
        });

        modelBuilder.Entity<EbookContentState>(e =>
        {
            e.ToTable("ebook_content_states");
            e.Property(x => x.Status).HasMaxLength(10);
            e.Property(x => x.Error).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.EbookItemId }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.Status, x.RequestedAt });
        });

        modelBuilder.Entity<EbookChunk>(e =>
        {
            e.ToTable("ebook_chunks");
            e.Property(x => x.Text).HasMaxLength(EbookChunk.MaxTextLength);
            e.Property(x => x.SearchText).HasMaxLength(EbookChunk.MaxTextLength);
            e.HasIndex(x => new { x.TenantId, x.EbookItemId, x.Ordinal });
        });

        modelBuilder.Entity<MetadataSchema>(e =>
        {
            e.ToTable("metadata_schemas");
            e.Property(x => x.NameSpace).HasMaxLength(MetadataSchema.MaxLength);
            e.Property(x => x.ShortId).HasMaxLength(50);
            e.Property(x => x.Language).HasMaxLength(10);
            e.HasIndex(x => new { x.TenantId, x.ShortId }).IsUnique();
        });

        modelBuilder.Entity<MetadataField>(e =>
        {
            e.ToTable("metadata_fields");
            e.Property(x => x.Field).HasMaxLength(100);
            e.Property(x => x.Subfield).HasMaxLength(100);
            e.Property(x => x.DescriptionVn).HasMaxLength(300);
            e.Property(x => x.DescriptionEn).HasMaxLength(300);
            e.Property(x => x.Input).HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.SchemaId, x.Field, x.Subfield }).IsUnique();
        });

        modelBuilder.Entity<EbookMetadataValue>(e =>
        {
            e.ToTable("ebook_metadata_values");
            e.Property(x => x.Value).HasMaxLength(EbookMetadataValue.MaxValueLength);
            e.Property(x => x.Language).HasMaxLength(10);
            e.HasIndex(x => new { x.TenantId, x.EbookItemId, x.FieldId, x.SortOrder });
            e.HasIndex(x => new { x.TenantId, x.FieldId });
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Chưa có gì cần seed riêng cho digital (danh mục mặc định — nếu có — thêm khi port EbookDigType).</summary>
public sealed class DigitalTenantSeeder : ITenantSeeder
{
    public Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return Task.CompletedTask;
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class DigitalDbContextDesignFactory : IDesignTimeDbContextFactory<DigitalDbContext>
{
    public DigitalDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<DigitalDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_digital")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
