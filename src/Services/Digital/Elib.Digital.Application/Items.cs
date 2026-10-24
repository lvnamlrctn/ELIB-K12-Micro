using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Digital.Application;

/// <summary>Cấu hình riêng của digital — tiền tố công khai của media (ảnh bìa), đúng khuôn Catalog's CatalogOptions.</summary>
public sealed class DigitalOptions
{
    public const string SectionName = "Digital";

    public string MediaPublicPrefix { get; set; } = "/s3/media-public";
}

/// <summary>Một biểu ghi sách in tìm được ở catalog — ô chọn liên kết của màn thêm/sửa tài liệu số (quyết định #2).</summary>
public sealed record BibLinkOption(Guid BibPublicId, string Title, string? Author, string? Publisher, string? PublishYear);

/// <summary>Tìm biểu ghi sách in ở catalog để liên kết — chỉ tra cứu hiển thị, không giữ bản sao (quyết định #2 của plan).</summary>
public interface ICatalogBibLookup
{
    Task<IReadOnlyList<BibLinkOption>> SearchAsync(string? keyword, CancellationToken ct);
}

public sealed class EbookItemSearch : CrudSearch
{
    public long? CollectionId { get; set; }
    public long? SubjectId { get; set; }
    public long? TopicId { get; set; }
    public long? TypeId { get; set; }

    /// <summary>Lọc theo nhan đề / tác giả / nhà xuất bản (chứa chuỗi, không phân biệt hoa thường).</summary>
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYearFrom { get; set; }
    public int? PublishYearTo { get; set; }

    /// <summary>Ngày nộp (ngày lịch Việt Nam, gồm cả hai đầu).</summary>
    public DateOnly? SubmittedFrom { get; set; }
    public DateOnly? SubmittedTo { get; set; }
    public bool? Free { get; set; }
}

public sealed record EbookItemDto(
    long Id, Guid PublicId, int Status, long? CollectionId, string? CollectionName, long? SubjectId, long? TopicId, long? TypeId, string Title,
    string? Author, string? Publisher, string? PublishDate, string? Keyword, string? OtherTitle, string? Page, string? OldAuthor, string? Xml,
    string? Language, bool Free, bool AllowDownload, int? PrintCopies, int? OfflineDays, int? Show, int? IndexContent, int? Share, int? FileVersion,
    DateTimeOffset? IndexedAt, string? ErrorMessage, int TotalView, int TotalDownload, DateTimeOffset? Submited, DateTimeOffset? LastUpdate,
    string? CoverUrl, Guid? LinkedBibPublicId, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string? Abstract, string? Description, string? JournalName,
    int? PublishDay, int? PublishMonth, int? PublishYear, IReadOnlyList<EbookPerson> Authors, IReadOnlyList<EbookPerson> Advisors,
    IReadOnlyList<EbookIdentifier> Identifiers, int TotalFile, bool IsReplica, bool HiddenByTenant);

/// <summary>Tài liệu số (LRC: EbookItemController, quyền DIGITAL_ITEMS) — trang quản trị bespoke (có file đính kèm + liên kết sách in).</summary>
public sealed partial class EbookItemResource(
    ICrudDbContext db, ITenantContext tenant, IOptions<DigitalOptions> options, TimeProvider clock, EbookStatePublisher states, IMediaFileStore store)
    : CrudResource<EbookItemResource, EbookItem, EbookItemSearch, EbookItemFields, EbookItemDto>(db)
{
    private readonly DigitalOptions _options = options.Value;

    protected override string EntityName => "Tài liệu số";

    protected override string? Describe(EbookItem entity) => entity.Title;

    protected override Expression<Func<EbookItem, EbookItemDto>> Projection => x => new EbookItemDto(
        x.Id, x.PublicId, x.Status, x.CollectionId, Db.Set<EbookCollection>().Where(c => c.Id == x.CollectionId).Select(c => c.Name).FirstOrDefault(),
        x.SubjectId, x.TopicId, x.TypeId, x.Title, x.Author, x.Publisher, x.PublishDate, x.Keyword, x.OtherTitle, x.Page, x.OldAuthor, x.Xml,
        x.Language, x.Free, x.AllowDownload, x.PrintCopies, x.OfflineDays, x.Show, x.IndexContent, x.Share, x.FileVersion, x.IndexedAt, x.ErrorMessage,
        x.TotalView, x.TotalDownload, x.Submited, x.LastUpdate, x.CoverUrl, x.LinkedBibPublicId, x.CreatedAt, x.UpdatedAt, x.Abstract, x.Description, x.JournalName,
        x.PublishDay, x.PublishMonth, x.PublishYear, x.Authors, x.Advisors, x.Identifiers, Db.Set<EbookFile>().Count(f => f.EbookItemId == x.Id), x.IsReplica, x.HiddenByTenant);

    protected override EbookItem Create(EbookItemFields request) =>
        EbookItem.Create(request, _options.MediaPublicPrefix, tenant.RequireTenantId(), clock.GetUtcNow());

    protected override void Update(EbookItem entity, EbookItemFields request) =>
        entity.Update(request, _options.MediaPublicPrefix, tenant.RequireTenantId(), clock.GetUtcNow());

    protected override IQueryable<EbookItem> Filter(IQueryable<EbookItem> query, EbookItemSearch search)
    {
        if (search.CollectionId is { } collection) query = query.Where(x => x.CollectionId == collection);
        if (search.SubjectId is { } subject) query = query.Where(x => x.SubjectId == subject);
        if (search.TopicId is { } topic) query = query.Where(x => x.TopicId == topic);
        if (search.TypeId is { } type) query = query.Where(x => x.TypeId == type);
        if (search.Free is { } free) query = query.Where(x => x.Free == free);
        if (search.PublishYearFrom is { } from) query = query.Where(x => x.PublishYear >= from);
        if (search.PublishYearTo is { } to) query = query.Where(x => x.PublishYear <= to);
        // Ngày nộp là ngày lịch Việt Nam: đầu ngày "từ" đến trước đầu ngày sau "đến" (gồm cả ngày "đến").
        if (search.SubmittedFrom is { } submittedFrom) { var start = VietnamDayStart(submittedFrom); query = query.Where(x => x.Submited >= start); }
        if (search.SubmittedTo is { } submittedTo) { var end = VietnamDayStart(submittedTo.AddDays(1)); query = query.Where(x => x.Submited < end); }
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        if (Fold(search.Title) is { } title) query = query.Where(x => x.Title.ToLower().Contains(title));
        if (Fold(search.Author) is { } author) query = query.Where(x => x.Author != null && x.Author.ToLower().Contains(author));
        if (Fold(search.Publisher) is { } publisher) query = query.Where(x => x.Publisher != null && x.Publisher.ToLower().Contains(publisher));
        if (search.Term is not { } term) return query;
        return query.Where(x => x.Title.ToLower().Contains(term) || (x.Author != null && x.Author.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
    }

    private static string? Fold(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    /// <summary>00:00 của ngày lịch Việt Nam (UTC+7) tại UTC — ghi tham số DateTimeOffset vào Postgres phải ở UTC.</summary>
    internal static DateTimeOffset VietnamDayStart(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();

    protected override async Task ValidateAsync(EbookItem entity, CancellationToken ct)
    {
        if (entity.CollectionId is { } collection && !await Db.Set<EbookCollection>().AnyAsync(x => x.Id == collection, ct))
            throw new BusinessRuleException("COLLECTION_NOT_FOUND", "Bộ sưu tập đã chọn không còn.");
        if (entity.SubjectId is { } subject && !await Db.Set<EbookSubject>().AnyAsync(x => x.Id == subject, ct))
            throw new BusinessRuleException("SUBJECT_NOT_FOUND", "Môn loại đã chọn không còn.");
        if (entity.TopicId is { } topic && !await Db.Set<EbookTopic>().AnyAsync(x => x.Id == topic, ct))
            throw new BusinessRuleException("TOPIC_NOT_FOUND", "Chủ đề đã chọn không còn.");
        if (entity.TypeId is { } type && !await Db.Set<EbookDigType>().AnyAsync(x => x.Id == type, ct))
            throw new BusinessRuleException("DIG_TYPE_NOT_FOUND", "Loại tài liệu số đã chọn không còn.");
    }

    // Mọi thay đổi phát EbookChanged (outbox, cùng transaction) cho search.
    protected override Task OnSavingAsync(EbookItem entity, CrudChange change, CancellationToken ct) =>
        change == CrudChange.Added ? Task.CompletedTask : states.PublishAsync(entity, change == CrudChange.Deleted, ct);

    /// <summary>Thêm tài liệu: lưu trước để có Id, rồi phát <see cref="Elib.Contracts.Events.Digital.EbookChanged"/> + nhật ký trong CÙNG transaction
    /// (khuôn BibResource.AddAsync) — search nhận được Id ngay từ event đầu tiên.</summary>
    public override async Task<EbookItemDto> AddAsync(EbookItemFields request, CancellationToken ct)
    {
        var entity = await CreateAsync(request, ct);
        await ValidateAsync(entity, ct);
        await AddCoreAsync(entity, ct);
        return ToDto(entity);
    }

    private async Task AddCoreAsync(EbookItem entity, CancellationToken ct)
    {
        var strategy = Db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await Db.Database.BeginTransactionAsync(token);
            if (Set.Entry(entity).State == EntityState.Detached) Set.Add(entity);
            await Db.SaveChangesAsync(token);
            await states.PublishAsync(entity, deleted: false, token);
            await AuditAsync(entity, CrudChange.Added, token);
            await Db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }, ct);
    }

    protected override async Task EnsureDeletableAsync(EbookItem entity, CancellationToken ct)
    {
        if (entity.IsReplica) throw EbookItem.ReadOnly(entity.Title);
        // Kiểm tra file TRƯỚC khi đánh dấu xoá metadata: bị chặn thì không để lại thay đổi nào trong ngữ cảnh (xoá hàng loạt dùng chung một lần lưu).
        if (await Db.Set<EbookFile>().AnyAsync(f => f.EbookItemId == entity.Id, ct))
            throw new ConflictException("EBOOK_ITEM_HAS_FILES", $"Tài liệu số '{entity.Title}' còn file đính kèm — xoá file trước.");
        // Metadata là con của tài liệu: xoá cùng lần lưu (không chặn xoá).
        Db.Set<EbookMetadataValue>().RemoveRange(await Db.Set<EbookMetadataValue>().Where(v => v.EbookItemId == entity.Id).ToListAsync(ct));
    }
}

public sealed record EbookFileRequest(
    Guid EbookItemPublicId, Guid MediaFileId, string? Type = null, bool IsConvert = false, string? FileType = null, double? FileSize = null,
    string? FileExt = null, string? Description = null, string? Source = null, int? FormatId = null, string? CheckSumAlgorithm = null, int SortOrder = 0);

public sealed record EbookFileDto(
    long Id, Guid PublicId, Guid EbookItemPublicId, Guid MediaFileId, string? Type, bool IsConvert, string? FileType, double? FileSize, string? FileExt,
    string? Description, string? Source, int? FormatId, string? CheckSumAlgorithm, int SortOrder, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed class EbookFileSearch : CrudSearch
{
    public Guid? EbookItemPublicId { get; set; }
}

/// <summary>
/// File đính kèm của tài liệu số (LRC: EbookFileController, cùng quyền DIGITAL_ITEMS — file là con của tài liệu số, không có quyền riêng).
/// Nội dung đã upload trực tiếp lên media từ trình quản trị (purpose <c>ebook-file</c>) trước khi gọi Add — chỉ lưu tham chiếu ở đây.
/// </summary>
public sealed class EbookFileResource(ICrudDbContext db, EbookStatePublisher states, TimeProvider clock)
    : CrudResource<EbookFileResource, EbookFile, EbookFileSearch, EbookFileRequest, EbookFileDto>(db)
{
    // Thêm/xoá file đổi "có file để đọc" của tài liệu → tăng phiên bản và phát lại trạng thái cho search.
    protected override async Task OnSavingAsync(EbookFile entity, CrudChange change, CancellationToken ct)
    {
        if (change is not (CrudChange.Added or CrudChange.Deleted)) return;
        var item = await Db.Set<EbookItem>().FirstOrDefaultAsync(i => i.Id == entity.EbookItemId, ct);
        if (item is null) return;
        var hasFile = change == CrudChange.Added || await Db.Set<EbookFile>().AnyAsync(f => f.EbookItemId == item.Id && f.Id != entity.Id, ct);
        item.Touch(clock.GetUtcNow());
        await states.PublishAsync(item, false, ct, hasFile: hasFile);
        await RefreshContentAsync(item, entity, change, ct);
    }

    /// <summary>
    /// Đồng bộ nội dung đã chạy cho tài liệu này thì xếp lại hàng đợi khi thêm/xoá file. Xoá file PDF cuối cùng: gỡ ngay các đoạn đã lập chỉ mục
    /// (không để OPAC còn trích dẫn từ file cán bộ đã gỡ, có thể là file nhầm hoặc mật) rồi để việc nền ghi trạng thái.
    /// </summary>
    private async Task RefreshContentAsync(EbookItem item, EbookFile file, CrudChange change, CancellationToken ct)
    {
        var state = await Db.Set<EbookContentState>().FirstOrDefaultAsync(s => s.EbookItemId == item.Id, ct);
        if (state is null) return;
        if (change == CrudChange.Deleted
            && !await Db.Set<EbookFile>().Where(f => f.EbookItemId == item.Id && f.Id != file.Id).Where(PdfFiles.Predicate).AnyAsync(ct))
            await Db.Set<EbookChunk>().Where(c => c.EbookItemId == item.Id).ExecuteDeleteAsync(ct);
        state.Requeue(clock.GetUtcNow());
    }

    protected override string EntityName => "File tài liệu số";

    protected override Task EnsureDeletableAsync(EbookFile entity, CancellationToken ct) =>
        entity.IsReplica ? throw EbookItem.ReadOnly(entity.Description ?? "file") : Task.CompletedTask;

    protected override string? Describe(EbookFile entity) => entity.FileExt is { } ext ? $"file #{entity.Id}.{ext}" : $"file #{entity.Id}";

    protected override Expression<Func<EbookFile, EbookFileDto>> Projection => x => new EbookFileDto(
        x.Id, x.PublicId, Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.PublicId).FirstOrDefault(), x.MediaFileId, x.Type,
        x.IsConvert, x.FileType, x.FileSize, x.FileExt, x.Description, x.Source, x.FormatId, x.CheckSumAlgorithm, x.SortOrder, x.CreatedAt, x.UpdatedAt);

    // Thêm cần tra tài liệu số theo PublicId trong request — override CreateAsync, Create đồng bộ không được gọi. Sửa không đổi
    // tài liệu số/file đã chọn (chọn sai thì xoá thêm lại), chỉ áp các trường mô tả.
    protected override EbookFile Create(EbookFileRequest request) => throw new NotSupportedException();

    protected override void Update(EbookFile entity, EbookFileRequest request) => entity.Update(ToFields(request));

    protected override async Task<EbookFile> CreateAsync(EbookFileRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await Db.Set<EbookItem>().AsNoTracking().Where(i => i.PublicId == request.EbookItemPublicId).Select(i => new { i.Id, i.Title, i.IsReplica }).FirstOrDefaultAsync(ct);
        if (item is null) throw new NotFoundException("Tài liệu số", request.EbookItemPublicId);
        if (item.IsReplica) throw EbookItem.ReadOnly(item.Title);
        var itemId = item.Id;
        return EbookFile.Create(itemId, request.MediaFileId, ToFields(request));
    }

    protected override IQueryable<EbookFile> Filter(IQueryable<EbookFile> query, EbookFileSearch search)
    {
        if (search.EbookItemPublicId is not { } publicId) return query;
        var itemId = Db.Set<EbookItem>().Where(i => i.PublicId == publicId).Select(i => i.Id).FirstOrDefault();
        return query.Where(x => x.EbookItemId == itemId);
    }

    protected override IOrderedQueryable<EbookFile> Order(IQueryable<EbookFile> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);

    private static EbookFileFields ToFields(EbookFileRequest r) =>
        new(r.Type, r.IsConvert, r.FileType, r.FileSize, r.FileExt, r.Description, r.Source, r.FormatId, r.CheckSumAlgorithm, r.SortOrder);
}
