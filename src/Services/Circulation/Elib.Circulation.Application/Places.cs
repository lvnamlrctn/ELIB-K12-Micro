using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Circulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

public sealed record CircPlaceRequest(string Code, string Name, IReadOnlyList<long>? StoreIds = null);

public sealed record CircPlaceDto(long Id, Guid PublicId, string Code, string Name, IReadOnlyList<long> StoreIds, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Điểm lưu thông (monolith: CircPlaceController, quyền CIRC_PLACES). Mã không trùng; còn chính sách riêng thì không xoá được.</summary>
public sealed class CircPlaceResource(ICrudDbContext db) : CrudResource<CircPlaceResource, CircPlace, CrudSearch, CircPlaceRequest, CircPlaceDto>(db)
{
    public const string DefaultCode = "QUAY";

    protected override string EntityName => "Điểm lưu thông";

    protected override string? Describe(CircPlace entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<CircPlace, CircPlaceDto>> Projection =>
        x => new CircPlaceDto(x.Id, x.PublicId, x.Code, x.Name, x.StoreIds, x.CreatedAt, x.UpdatedAt);

    protected override CircPlace Create(CircPlaceRequest request) => CircPlace.Create(request.Code, request.Name, request.StoreIds);

    protected override void Update(CircPlace entity, CircPlaceRequest request) => entity.Update(request.Code, request.Name, request.StoreIds);

    protected override IQueryable<CircPlace> Filter(IQueryable<CircPlace> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<CircPlace> Order(IQueryable<CircPlace> query) => query.OrderBy(x => x.Code).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(CircPlace entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("CIRC_PLACE_CODE_EXISTS", $"Mã điểm lưu thông '{entity.Code}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(CircPlace entity, CancellationToken ct)
    {
        if (await Db.Set<LoanPolicy>().AnyAsync(p => p.CircPlaceId == entity.Id, ct))
            throw new ConflictException("CIRC_PLACE_IN_USE", $"Điểm {entity.Code} còn chính sách lưu thông riêng — xoá chính sách trước.");
    }

    /// <summary>Đơn vị mới: một quầy mượn trả để dùng được ngay.</summary>
    public async Task<bool> AddDefaultAsync(CancellationToken ct)
    {
        if (await Set.AnyAsync(ct)) return false;
        Set.Add(CircPlace.Create(DefaultCode, "Quầy mượn trả", null));
        await Db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record LoanPolicyRequest(long? ReaderTypeId, long? CircPlaceId, int LoanDays = LoanPolicy.DefaultLoanDays, int? MaxLoans = null,
    int? MaxRenewals = null, int RenewDays = LoanPolicy.DefaultRenewDays);

public sealed record LoanPolicyDto(
    long Id, Guid PublicId, long? ReaderTypeId, long? CircPlaceId, int LoanDays, int? MaxLoans, int? MaxRenewals, int RenewDays,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>
/// Chính sách lưu thông (monolith: CirculationCircPolicyController, quyền CIRC_POLICIES). Mỗi cặp loại bạn đọc × điểm lưu thông
/// (kể cả "mọi loại"/"mọi điểm") chỉ một chính sách.
/// </summary>
public sealed class LoanPolicyResource(ICrudDbContext db) : CrudResource<LoanPolicyResource, LoanPolicy, CrudSearch, LoanPolicyRequest, LoanPolicyDto>(db)
{
    protected override string EntityName => "Chính sách lưu thông";

    protected override string? Describe(LoanPolicy entity) =>
        $"loại bạn đọc {entity.ReaderTypeId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "mọi loại"}, " +
        $"điểm {entity.CircPlaceId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "mọi điểm"}: {entity.LoanDays} ngày";

    protected override Expression<Func<LoanPolicy, LoanPolicyDto>> Projection => x => new LoanPolicyDto(
        x.Id, x.PublicId, x.ReaderTypeId, x.CircPlaceId, x.LoanDays, x.MaxLoans, x.MaxRenewals, x.RenewDays, x.CreatedAt, x.UpdatedAt);

    protected override LoanPolicy Create(LoanPolicyRequest r) =>
        LoanPolicy.Create(r.ReaderTypeId, r.CircPlaceId, r.LoanDays, r.MaxLoans, r.MaxRenewals, r.RenewDays);

    protected override void Update(LoanPolicy entity, LoanPolicyRequest r) =>
        entity.Update(r.ReaderTypeId, r.CircPlaceId, r.LoanDays, r.MaxLoans, r.MaxRenewals, r.RenewDays);

    protected override IOrderedQueryable<LoanPolicy> Order(IQueryable<LoanPolicy> query) =>
        query.OrderBy(x => x.CircPlaceId).ThenBy(x => x.ReaderTypeId).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(LoanPolicy entity, CancellationToken ct)
    {
        if (entity.CircPlaceId is { } place && !await Db.Set<CircPlace>().AnyAsync(p => p.Id == place, ct))
            throw new BusinessRuleException("CIRC_PLACE_NOT_FOUND", "Điểm lưu thông đã chọn không còn.");
        if (await Set.AnyAsync(x => x.ReaderTypeId == entity.ReaderTypeId && x.CircPlaceId == entity.CircPlaceId && x.Id != entity.Id, ct))
            throw new ConflictException("POLICY_EXISTS", "Đã có chính sách cho loại bạn đọc và điểm lưu thông này — sửa chính sách đó.");
    }

    /// <summary>Đơn vị mới: chính sách chung (mọi loại, mọi điểm) theo mặc định của monolith.</summary>
    public async Task<bool> AddDefaultAsync(CancellationToken ct)
    {
        if (await Set.AnyAsync(ct)) return false;
        Set.Add(LoanPolicy.Create(null, null, LoanPolicy.DefaultLoanDays, null, null, LoanPolicy.DefaultRenewDays));
        await Db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Chính sách áp cho bạn đọc tại điểm lưu thông — cụ thể nhất thắng; không có thì mặc định.</summary>
    public static LoanPolicy Resolve(IEnumerable<LoanPolicy> policies, long? readerTypeId, long? circPlaceId) =>
        policies.Where(p => (p.ReaderTypeId is null || p.ReaderTypeId == readerTypeId) && (p.CircPlaceId is null || p.CircPlaceId == circPlaceId))
            .OrderByDescending(p => p.Specificity).ThenBy(p => p.Id).FirstOrDefault() ?? LoanPolicy.Default;
}
