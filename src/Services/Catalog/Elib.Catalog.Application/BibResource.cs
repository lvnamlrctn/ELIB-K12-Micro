using System.Globalization;
using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Catalog.Domain;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Catalog;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Catalog.Application;

/// <summary>Tìm biểu ghi ở màn quản trị (monolith: BibSearchRequest). Keyword không phân biệt dấu: nhan đề, tác giả, NXB, từ khoá, ISBN, DDC.</summary>
public sealed class BibSearch : CrudSearch
{
    public long? BibTypeId { get; set; }
    public string? Isbn { get; set; }
    public string? Ddc { get; set; }
}

/// <summary>
/// Thêm/sửa biểu ghi (monolith: CatalogueBook/Save). <see cref="Fields"/>: các trường MARC (không cần 001/005 — hệ thống tự quản);
/// <see cref="Leader"/> bỏ trống thì sinh theo loại biểu ghi.
/// </summary>
public sealed record BibRequest(long? BibTypeId, IReadOnlyList<MarcField> Fields, string? Leader = null, long? WorksheetId = null, int? Status = null);

/// <summary>Biểu ghi kèm MARC đầy đủ; <see cref="Fields"/> có sẵn 001 (MFN) và 005 (thời điểm sửa) như bản ghi MARC21 chuẩn.</summary>
public sealed record BibDto(
    long Id, long Mfn, Guid PublicId, long? BibTypeId, long? WorksheetId, string Leader, IReadOnlyList<MarcField> Fields,
    string Title, string? Author, string? Publisher, string? PublishYear, IReadOnlyList<string> Isbns, string? Ddc, string? Keywords,
    int Status, long Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record IsbnMatch(Guid PublicId, long Mfn, string Title);

/// <summary>
/// Biểu ghi thư mục (monolith: CatalogueBookController, quyền CATALOG_BIBS). Ngoài 8 endpoint chuẩn: GetByMfn, CheckIsbn.
/// Mọi thay đổi phát <see cref="BibChanged"/> (outbox, cùng transaction) cho bản sao ở holdings/circulation/search.
/// </summary>
public sealed partial class BibResource(ICrudDbContext db, IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor, TimeProvider clock)
    : CrudResource<BibResource, Bib, BibSearch, BibRequest, BibDto>(db)
{
    protected override string EntityName => "Biểu ghi";

    protected override string? Describe(Bib entity) => entity.Id > 0 ? $"MFN {entity.Id} — {entity.Title}" : entity.Title;

    protected override Expression<Func<Bib, BibDto>> Projection => x => new BibDto(
        x.Id, x.Id, x.PublicId, x.BibTypeId, x.WorksheetId, x.Leader, WithSystemFields(x.Fields, x.Id, x.UpdatedAt ?? x.CreatedAt),
        x.Title, x.Author, x.Publisher, x.PublishYear, SplitIsbns(x.Isbns), x.Ddc, x.Keywords, x.Status, x.Version, x.CreatedAt, x.UpdatedAt);

    protected override Bib Create(BibRequest request) =>
        throw new NotSupportedException("Biểu ghi tạo qua CreateAsync (cần tra loại biểu ghi).");

    protected override async Task<Bib> CreateAsync(BibRequest request, CancellationToken ct)
    {
        var bib = Bib.Create(await TypeAsync(request.BibTypeId, ct), request.WorksheetId, request.Leader, request.Fields, Today(), await AgencyCodeAsync(ct));
        if (request.Status is { } status && status != IHasStatus.Active) bib.ChangeStatus(status);
        return bib;
    }

    protected override void Update(Bib entity, BibRequest request) =>
        throw new NotSupportedException("Biểu ghi cập nhật qua UpdateAsync (cần tra loại biểu ghi).");

    protected override async Task UpdateAsync(Bib entity, BibRequest request, CancellationToken ct)
    {
        entity.Update(await TypeAsync(request.BibTypeId, ct), request.Leader, request.Fields, Today(), await AgencyCodeAsync(ct));
        if (request.Status is { } status && status != entity.Status) entity.ChangeStatus(status);
    }

    protected override IQueryable<Bib> Filter(IQueryable<Bib> query, BibSearch s)
    {
        if (s.BibTypeId is { } type) query = query.Where(x => x.BibTypeId == type);
        if (MarcRecord.NormalizeIsbn(s.Isbn) is { } isbn)
        {
            var key = "|" + isbn + "|";
            query = query.Where(x => x.Isbns.Contains(key));
        }
        if (!string.IsNullOrWhiteSpace(s.Ddc))
        {
            var ddc = s.Ddc.Trim();
            query = query.Where(x => x.Ddc != null && x.Ddc.StartsWith(ddc));
        }
        if (MarcRecord.Fold(s.Keyword) is { Length: > 0 } term) query = query.Where(x => x.SearchText.Contains(term));
        return query;
    }

    protected override IOrderedQueryable<Bib> Order(IQueryable<Bib> query) => query.OrderByDescending(x => x.Id);

    protected override async Task ValidateAsync(Bib entity, CancellationToken ct)
    {
        if (entity.BibTypeId is { } type && !await Db.Set<BibType>().AnyAsync(t => t.Id == type, ct))
            throw new BusinessRuleException("BIB_TYPE_NOT_FOUND", "Loại biểu ghi đã chọn không còn.");
    }

    protected override Task OnSavingAsync(Bib entity, CrudChange change, CancellationToken ct) => PublishAsync(entity, change == CrudChange.Deleted, ct);

    /// <summary>
    /// Thêm biểu ghi: lưu trước để có MFN (= id), rồi phát <see cref="BibChanged"/> + nhật ký trong CÙNG transaction —
    /// bản sao ở service khác nhận được MFN ngay từ event đầu tiên.
    /// </summary>
    public override async Task<BibDto> AddAsync(BibRequest request, CancellationToken ct)
    {
        var entity = await CreateAsync(request, ct);
        await ValidateAsync(entity, ct);
        var strategy = Db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await Db.Database.BeginTransactionAsync(token);
            if (Set.Entry(entity).State == EntityState.Detached) Set.Add(entity);
            await Db.SaveChangesAsync(token);
            await PublishAsync(entity, deleted: false, token);
            await AuditAsync(entity, CrudChange.Added, token);
            await Db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }, ct);
        return ToDto(entity);
    }

    /// <summary>Biểu ghi theo MFN (monolith: GetByMfn) — MFN chính là id.</summary>
    public Task<BibDto> GetByMfnAsync(long mfn, CancellationToken ct) => GetAsync(mfn, ct);

    /// <summary>Biểu ghi khác đã có cùng ISBN (monolith: CheckIsbn) — cảnh báo trùng khi biên mục.</summary>
    public async Task<IReadOnlyList<IsbnMatch>> CheckIsbnAsync(string? isbn, Guid? excludePublicId, CancellationToken ct)
    {
        if (MarcRecord.NormalizeIsbn(isbn) is not { } normalized) return [];
        var key = "|" + normalized + "|";
        return await Set.AsNoTracking().Where(x => x.Isbns.Contains(key) && x.PublicId != excludePublicId)
            .OrderBy(x => x.Id).Select(x => new IsbnMatch(x.PublicId, x.Id, x.Title)).Take(20).ToListAsync(ct);
    }

    /// <summary>Thêm 001 (MFN) và 005 (yyyyMMddHHmmss.f) vào danh sách trường lưu trữ — hai trường này không lưu trong JSON.</summary>
    public static IReadOnlyList<MarcField> WithSystemFields(IReadOnlyList<MarcField> fields, long mfn, DateTimeOffset changedAt) =>
    [
        new MarcField("001", Value: mfn.ToString(CultureInfo.InvariantCulture)),
        .. fields.Where(f => string.CompareOrdinal(f.Tag, "005") < 0),
        new MarcField("005", Value: changedAt.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".0"),
        .. fields.Where(f => string.CompareOrdinal(f.Tag, "005") > 0),
    ];

    private static string[] SplitIsbns(string isbns) => isbns.Split('|', StringSplitOptions.RemoveEmptyEntries);

    private async Task<BibType?> TypeAsync(long? id, CancellationToken ct) => id is { } value
        ? await Db.Set<BibType>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == value, ct)
          ?? throw new BusinessRuleException("BIB_TYPE_NOT_FOUND", "Loại biểu ghi đã chọn không còn.")
        : null;

    /// <summary>Mã đơn vị làm 003 (mã cơ quan tạo biểu ghi), như monolith.</summary>
    private async Task<string?> AgencyCodeAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        return await Db.Set<TenantReplicaRecord>().AsNoTracking().Where(t => t.TenantId == tenantId).Select(t => t.Code).FirstOrDefaultAsync(ct);
    }

    private Task PublishAsync(Bib bib, bool deleted, CancellationToken ct)
    {
        if (bib.PublicId == Guid.Empty) bib.PublicId = Guid.CreateVersion7(); // biểu ghi mới: interceptor chỉ gán khi còn trống
        return publisher.Publish(new BibChanged
        {
            TenantId = tenant.RequireTenantId(),
            Actor = new EventActor(actor.Id, actor.Kind),
            BibPublicId = bib.PublicId,
            Mfn = bib.Id,
            BibTypeId = bib.BibTypeId,
            Title = bib.Title,
            Author = bib.Author,
            Publisher = bib.Publisher,
            PublishYear = bib.PublishYear,
            Isbns = bib.IsbnList,
            Ddc = bib.Ddc,
            Status = bib.Status,
            Deleted = deleted,
            Version = bib.Version + (deleted ? 1 : 0),
        }, ct);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7)); // giờ Việt Nam
}
