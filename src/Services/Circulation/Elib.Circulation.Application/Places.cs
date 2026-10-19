using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Circulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

public sealed record CircPlaceRequest(string Code, string Name, IReadOnlyList<long>? StoreIds = null, IReadOnlyList<long>? ReaderTypeIds = null,
    int Kind = CircPlace.KindBorrow, int? MaxLoans = null, IReadOnlyList<WorkSession>? WorkSessions = null, string? Note = null);

/// <summary>Nơi lưu thông đang dùng một loại bạn đọc: tên các điểm phục vụ loại đó + số chính sách lưu thông theo loại đó.</summary>
public sealed record ReaderTypeUsage(IReadOnlyList<string> Places, int Policies);

public sealed record CircPlaceDto(long Id, Guid PublicId, string Code, string Name, IReadOnlyList<long> StoreIds, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    IReadOnlyList<long>? ReaderTypeIds = null, int Kind = CircPlace.KindBorrow, int? MaxLoans = null, IReadOnlyList<WorkSession>? WorkSessions = null,
    string? Note = null);

/// <summary>
/// Điểm lưu thông (ELIB-LRC: CircPlaceController + GetMapping/ReplaceStoreMapping/ReplaceReaderTypeMapping, quyền CIRC_PLACES). Mã không trùng
/// trong đơn vị. Kho và loại bạn đọc gửi cùng request thay hai lệnh thay mapping riêng của LRC. Mỗi loại bạn đọc mới gán vào điểm có sẵn một
/// chính sách riêng (loại × điểm) chép từ chính sách đang áp dụng, để mở ra cấu hình ngay như LRC. Xoá điểm thì xoá luôn chính sách riêng
/// của điểm; còn trạm SIP2 dùng điểm thì không xoá được.
/// </summary>
public sealed class CircPlaceResource(ICrudDbContext db) : CrudResource<CircPlaceResource, CircPlace, CrudSearch, CircPlaceRequest, CircPlaceDto>(db)
{
    public const string DefaultCode = "QUAY";

    protected override string EntityName => "Điểm lưu thông";

    protected override string? Describe(CircPlace entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<CircPlace, CircPlaceDto>> Projection =>
        x => new CircPlaceDto(x.Id, x.PublicId, x.Code, x.Name, x.StoreIds, x.CreatedAt, x.UpdatedAt, x.ReaderTypeIds, x.Kind, x.MaxLoans, x.WorkSessions, x.Note);

    protected override CircPlace Create(CircPlaceRequest request) => CircPlace.Create(request.Code, request.Name, request.StoreIds, request.ReaderTypeIds,
        request.Kind, request.MaxLoans, request.WorkSessions, request.Note);

    protected override void Update(CircPlace entity, CircPlaceRequest request) => entity.Update(request.Code, request.Name, request.StoreIds, request.ReaderTypeIds,
        request.Kind, request.MaxLoans, request.WorkSessions, request.Note);

    /// <summary>Thêm điểm: lưu trước để có id rồi tạo chính sách cho loại bạn đọc đã gán — cùng một transaction.</summary>
    public override async Task<CircPlaceDto> AddAsync(CircPlaceRequest request, CancellationToken ct)
    {
        var entity = await CreateAsync(request, ct);
        await ValidateAsync(entity, ct);
        var strategy = Db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await Db.Database.BeginTransactionAsync(token);
            if (Set.Entry(entity).State == EntityState.Detached) Set.Add(entity);
            await AuditAsync(entity, CrudChange.Added, token);
            await Db.SaveChangesAsync(token);
            await EnsurePoliciesAsync(entity, token);
            await Db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }, ct);
        return ToDto(entity);
    }

    protected override async Task OnSavingAsync(CircPlace entity, CrudChange change, CancellationToken ct)
    {
        if (change == CrudChange.Updated) await EnsurePoliciesAsync(entity, ct);
        if (change == CrudChange.Deleted)
        {
            // Chính sách riêng của điểm không còn ý nghĩa khi điểm bị xoá (interceptor chuyển thành xoá mềm, lượt mượn cũ giữ CircPlaceId).
            var own = await Db.Set<LoanPolicy>().Where(p => p.CircPlaceId == entity.Id).ToListAsync(ct);
            Db.Set<LoanPolicy>().RemoveRange(own);
        }
    }

    /// <summary>Loại bạn đọc đã gán mà chưa có chính sách riêng tại điểm → tạo, chép từ chính sách đang áp cho cặp đó. Không đụng chính sách đã có.</summary>
    private async Task EnsurePoliciesAsync(CircPlace place, CancellationToken ct)
    {
        if (place.ReaderTypeIds.Count == 0) return;
        var policies = await Db.Set<LoanPolicy>().AsNoTracking().ToListAsync(ct);
        foreach (var readerTypeId in place.ReaderTypeIds)
        {
            if (policies.Any(p => p.CircPlaceId == place.Id && p.ReaderTypeId == readerTypeId)) continue;
            Db.Set<LoanPolicy>().Add(LoanPolicy.CopyFor(readerTypeId, place.Id, LoanPolicyResource.Resolve(policies, readerTypeId, place.Id)));
        }
    }

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
        if (await Db.Set<Sip2Station>().AnyAsync(s => s.CircPlaceId == entity.Id, ct))
            throw new ConflictException("CIRC_PLACE_IN_USE", $"Điểm {entity.Code} đang có trạm tự phục vụ SIP2 — chuyển trạm sang điểm khác trước.");
        if (await Db.Set<LibraryVisit>().AnyAsync(v => v.CircPlaceId == entity.Id || v.CircPlaceOutId == entity.Id, ct))
            throw new ConflictException("CIRC_PLACE_IN_USE", $"Điểm {entity.Code} đã có lượt vào/ra thư viện — không xoá được.");
    }

    /// <summary>
    /// Loại bạn đọc đang được lưu thông dùng (điểm phục vụ loại này, chính sách lưu thông theo loại này) — patron hỏi trước khi xoá loại bạn đọc
    /// (ELIB-LRC: ReaderTypeRepository.DeleteAsync kiểm CircPlaceReaderTypes + PolicyCircs). Chỉ trong đơn vị hiện tại.
    /// </summary>
    public async Task<ReaderTypeUsage> ReaderTypeUsageAsync(long readerTypeId, CancellationToken ct)
    {
        var places = await Set.AsNoTracking().Where(p => p.ReaderTypeIds.Contains(readerTypeId)).OrderBy(p => p.Code)
            .Select(p => p.Code + " — " + p.Name).ToListAsync(ct);
        var policies = await Db.Set<LoanPolicy>().CountAsync(p => p.ReaderTypeId == readerTypeId, ct);
        return new ReaderTypeUsage(places, policies);
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
    int? MaxRenewals = null, int RenewDays = LoanPolicy.DefaultRenewDays, decimal FinePerDay = 0, bool RenewFromToday = false, int? MaxHolds = null,
    int HoldDays = LoanPolicy.DefaultHoldDays, decimal TechFee = 0, decimal ShippingFee = 0, IReadOnlyList<BibTypeRule>? BibTypeRules = null);

public sealed record LoanPolicyDto(
    long Id, Guid PublicId, long? ReaderTypeId, long? CircPlaceId, int LoanDays, int? MaxLoans, int? MaxRenewals, int RenewDays,
    decimal FinePerDay, bool RenewFromToday, int? MaxHolds, int HoldDays, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    decimal TechFee = 0, decimal ShippingFee = 0, IReadOnlyList<BibTypeRule>? BibTypeRules = null);

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
        x.Id, x.PublicId, x.ReaderTypeId, x.CircPlaceId, x.LoanDays, x.MaxLoans, x.MaxRenewals, x.RenewDays, x.FinePerDay, x.RenewFromToday,
        x.MaxHolds, x.HoldDays, x.CreatedAt, x.UpdatedAt, x.TechFee, x.ShippingFee, x.BibTypeRules);

    protected override LoanPolicy Create(LoanPolicyRequest r)
    {
        var policy = LoanPolicy.Create(r.ReaderTypeId, r.CircPlaceId, r.LoanDays, r.MaxLoans, r.MaxRenewals, r.RenewDays, r.FinePerDay, r.RenewFromToday,
            r.MaxHolds, r.HoldDays);
        policy.SetFees(r.TechFee, r.ShippingFee);
        policy.SetBibTypeRules(r.BibTypeRules);
        return policy;
    }

    protected override void Update(LoanPolicy entity, LoanPolicyRequest r)
    {
        entity.Update(r.ReaderTypeId, r.CircPlaceId, r.LoanDays, r.MaxLoans, r.MaxRenewals, r.RenewDays, r.FinePerDay, r.RenewFromToday, r.MaxHolds, r.HoldDays);
        entity.SetFees(r.TechFee, r.ShippingFee);
        entity.SetBibTypeRules(r.BibTypeRules);
    }

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
