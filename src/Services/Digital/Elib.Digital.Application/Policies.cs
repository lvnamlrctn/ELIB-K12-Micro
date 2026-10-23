using System.Globalization;
using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed record PolicyDigitalRequest(long? ReaderTypeId = null, int? MaxPage = null, long? MaxSize = null, int? MaxDocument = null, int? ReservationExpireHours = null);

public sealed record PolicyDigitalDto(
    long Id, Guid PublicId, long? ReaderTypeId, int? MaxPage, long? MaxSize, int? MaxDocument, int? ReservationExpireHours, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Chính sách tài liệu số theo loại bạn đọc (LRC: PolicyDigitalController, quyền DIGITAL_POLICIES).</summary>
public sealed class PolicyDigitalResource(ICrudDbContext db)
    : CrudResource<PolicyDigitalResource, PolicyDigital, CrudSearch, PolicyDigitalRequest, PolicyDigitalDto>(db)
{
    protected override string EntityName => "Chính sách tài liệu số";

    protected override string? Describe(PolicyDigital entity) =>
        $"loại bạn đọc {entity.ReaderTypeId?.ToString(CultureInfo.InvariantCulture) ?? "mọi loại"}";

    protected override Expression<Func<PolicyDigital, PolicyDigitalDto>> Projection => x => new PolicyDigitalDto(
        x.Id, x.PublicId, x.ReaderTypeId, x.MaxPage, x.MaxSize, x.MaxDocument, x.ReservationExpireHours, x.CreatedAt, x.UpdatedAt);

    protected override PolicyDigital Create(PolicyDigitalRequest r) =>
        PolicyDigital.Create(r.ReaderTypeId, r.MaxPage, r.MaxSize, r.MaxDocument, r.ReservationExpireHours);

    protected override void Update(PolicyDigital entity, PolicyDigitalRequest r) =>
        entity.Update(r.ReaderTypeId, r.MaxPage, r.MaxSize, r.MaxDocument, r.ReservationExpireHours);

    protected override IOrderedQueryable<PolicyDigital> Order(IQueryable<PolicyDigital> query) => query.OrderBy(x => x.ReaderTypeId).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(PolicyDigital entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.ReaderTypeId == entity.ReaderTypeId && x.Id != entity.Id, ct))
            throw new ConflictException("POLICY_EXISTS", "Đã có chính sách cho loại bạn đọc này — sửa chính sách đó.");
    }

    /// <summary>Chính sách áp cho bạn đọc — cụ thể nhất thắng (theo loại); không có thì null (gọi nơi dùng tự quyết định mặc định).</summary>
    public static PolicyDigital? Resolve(IEnumerable<PolicyDigital> policies, long? readerTypeId) =>
        policies.Where(p => p.ReaderTypeId is null || p.ReaderTypeId == readerTypeId)
            .OrderByDescending(p => p.Specificity).ThenBy(p => p.Id).FirstOrDefault();
}

public sealed record PolicyDigitalByCollectionRequest(
    long? ReaderTypeId, long CollectionId, int? MaxPage = null, long? MaxSize = null, int? MaxDocument = null, bool Read = true,
    bool Comment = false, bool Download = false, int? OfflineDays = null, int? ReservationExpireHours = null);

public sealed record PolicyDigitalByCollectionDto(
    long Id, Guid PublicId, long? ReaderTypeId, long CollectionId, string? CollectionName, int? MaxPage, long? MaxSize, int? MaxDocument, bool Read,
    bool Comment, bool Download, int? OfflineDays, int? ReservationExpireHours, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Chính sách tài liệu số theo loại bạn đọc × bộ sưu tập (LRC: PolicyDigitalByCollectionController, quyền DIGITAL_POLICIES_BY_COLLECTION).</summary>
/// <summary>Quyền của một loại bạn đọc trên một bộ sưu tập (hộp "Phân quyền" ở cây bộ sưu tập, LRC: GetPermissions/SavePermissions).</summary>
public sealed record CollectionPermissionRow(long ReaderTypeId, bool Read, bool Download, int? MaxDocument = null, int? OfflineDays = null, int? ReservationExpireHours = null);

public sealed class PolicyDigitalByCollectionResource(ICrudDbContext db)
    : CrudResource<PolicyDigitalByCollectionResource, PolicyDigitalByCollection, CrudSearch, PolicyDigitalByCollectionRequest, PolicyDigitalByCollectionDto>(db)
{
    protected override string EntityName => "Chính sách theo bộ sưu tập";

    protected override string? Describe(PolicyDigitalByCollection entity) =>
        $"bộ sưu tập #{entity.CollectionId}, loại bạn đọc {entity.ReaderTypeId?.ToString(CultureInfo.InvariantCulture) ?? "mọi loại"}";

    protected override Expression<Func<PolicyDigitalByCollection, PolicyDigitalByCollectionDto>> Projection => x => new PolicyDigitalByCollectionDto(
        x.Id, x.PublicId, x.ReaderTypeId, x.CollectionId, Db.Set<EbookCollection>().Where(c => c.Id == x.CollectionId).Select(c => c.Name).FirstOrDefault(),
        x.MaxPage, x.MaxSize, x.MaxDocument, x.Read, x.Comment, x.Download, x.OfflineDays, x.ReservationExpireHours, x.CreatedAt, x.UpdatedAt);

    protected override PolicyDigitalByCollection Create(PolicyDigitalByCollectionRequest r) => PolicyDigitalByCollection.Create(
        r.ReaderTypeId, r.CollectionId, r.MaxPage, r.MaxSize, r.MaxDocument, r.Read, r.Comment, r.Download, r.OfflineDays, r.ReservationExpireHours);

    protected override void Update(PolicyDigitalByCollection entity, PolicyDigitalByCollectionRequest r) => entity.Update(
        r.ReaderTypeId, r.CollectionId, r.MaxPage, r.MaxSize, r.MaxDocument, r.Read, r.Comment, r.Download, r.OfflineDays, r.ReservationExpireHours);

    protected override IOrderedQueryable<PolicyDigitalByCollection> Order(IQueryable<PolicyDigitalByCollection> query) =>
        query.OrderBy(x => x.CollectionId).ThenBy(x => x.ReaderTypeId).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(PolicyDigitalByCollection entity, CancellationToken ct)
    {
        if (!await Db.Set<EbookCollection>().AnyAsync(c => c.Id == entity.CollectionId, ct))
            throw new BusinessRuleException("COLLECTION_NOT_FOUND", "Bộ sưu tập đã chọn không còn.");
        if (await Set.AnyAsync(x => x.CollectionId == entity.CollectionId && x.ReaderTypeId == entity.ReaderTypeId && x.Id != entity.Id, ct))
            throw new ConflictException("POLICY_EXISTS", "Đã có chính sách cho loại bạn đọc và bộ sưu tập này — sửa chính sách đó.");
    }

    public async Task<IReadOnlyList<CollectionPermissionRow>> PermissionsAsync(Guid collectionPublicId, CancellationToken ct)
    {
        var collectionId = await CollectionIdAsync(collectionPublicId, ct);
        return await Set.AsNoTracking().Where(x => x.CollectionId == collectionId && x.ReaderTypeId != null).OrderBy(x => x.ReaderTypeId)
            .Select(x => new CollectionPermissionRow(x.ReaderTypeId!.Value, x.Read, x.Download, x.MaxDocument, x.OfflineDays, x.ReservationExpireHours)).ToListAsync(ct);
    }

    /// <summary>
    /// Đặt lại quyền theo loại bạn đọc của một bộ sưu tập: loại có trong danh sách được thêm/sửa, loại có chính sách cũ mà không còn trong danh sách thì
    /// xoá. Chỉ đụng các dòng có loại bạn đọc cụ thể — dòng "mọi loại" (ReaderTypeId rỗng) giữ nguyên, và các trường hộp này không hiện
    /// (số trang/dung lượng tối đa, bình luận) của dòng đã có được giữ nguyên. Bật Tải thì tự bật Đọc.
    /// </summary>
    public async Task<IReadOnlyList<CollectionPermissionRow>> SavePermissionsAsync(Guid collectionPublicId, IReadOnlyList<CollectionPermissionRow> rows, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var collectionId = await CollectionIdAsync(collectionPublicId, ct);
        if (rows.Select(r => r.ReaderTypeId).Distinct().Count() != rows.Count)
            throw new BusinessRuleException("PERMISSION_DUPLICATE", "Mỗi loại bạn đọc chỉ xuất hiện một lần.");
        var existing = await Set.Where(x => x.CollectionId == collectionId && x.ReaderTypeId != null).ToListAsync(ct);
        foreach (var row in rows)
        {
            var read = row.Read || row.Download;
            var current = existing.FirstOrDefault(x => x.ReaderTypeId == row.ReaderTypeId);
            if (current is null)
            {
                var created = PolicyDigitalByCollection.Create(row.ReaderTypeId, collectionId, null, null, row.MaxDocument, read, false, row.Download, row.OfflineDays, row.ReservationExpireHours);
                Set.Add(created);
                await AuditAsync(created, CrudChange.Added, ct);
            }
            else
            {
                current.Update(row.ReaderTypeId, collectionId, current.MaxPage, current.MaxSize, row.MaxDocument, read, current.Comment, row.Download, row.OfflineDays, row.ReservationExpireHours);
                await AuditAsync(current, CrudChange.Updated, ct);
            }
        }
        var kept = rows.Select(r => r.ReaderTypeId).ToHashSet();
        foreach (var gone in existing.Where(x => !kept.Contains(x.ReaderTypeId!.Value)))
        {
            Set.Remove(gone); // interceptor chuyển thành xoá mềm
            await AuditAsync(gone, CrudChange.Deleted, ct);
        }
        await Db.SaveChangesAsync(ct);
        return await PermissionsAsync(collectionPublicId, ct);
    }

    private async Task<long> CollectionIdAsync(Guid publicId, CancellationToken ct) =>
        await Db.Set<EbookCollection>().AsNoTracking().Where(c => c.PublicId == publicId).Select(c => (long?)c.Id).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Bộ sưu tập", publicId);

    /// <summary>Chính sách áp cho bạn đọc trong một bộ sưu tập — cụ thể nhất thắng; không có thì null.</summary>
    public static PolicyDigitalByCollection? Resolve(IEnumerable<PolicyDigitalByCollection> policies, long? readerTypeId, long collectionId) =>
        policies.Where(p => p.CollectionId == collectionId && (p.ReaderTypeId is null || p.ReaderTypeId == readerTypeId))
            .OrderByDescending(p => p.Specificity).ThenBy(p => p.Id).FirstOrDefault();
}
