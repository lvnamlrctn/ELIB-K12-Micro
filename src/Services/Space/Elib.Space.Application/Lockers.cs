using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

public sealed class LockerCabinetSearch : CrudSearch
{
    public long? FloorId { get; set; }
    public long? BuildingId { get; set; }
}

public sealed record LockerCabinetRequest(long FloorId, string Code, string Name, int Rows, int Cols, string? ColorHex, string? IconName, string? Note);

public sealed record LockerCabinetDto(
    long Id, Guid PublicId, long FloorId, string? FloorName, long? BuildingId, string? BuildingName, string Code, string Name, int Rows, int Cols,
    double? PositionX, double? PositionY, double? Width, double? Height, string? ColorHex, string? IconName, string? Note,
    int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record PlaceLockerRequest(double? PositionX, double? PositionY, double? Width, double? Height);

/// <summary>
/// Tủ đựng đồ (monolith: PrintBook.Cabinet, quyền SPACE_LOCKERS). Tạo/đổi Rows×Cols đồng bộ lại lưới ngăn
/// (<see cref="LockerCompartment"/>) ngay sau khi lưu — xem <see cref="SyncCompartmentsAsync"/>. Vị trí trên sơ đồ sửa qua
/// <see cref="PlaceAsync"/> riêng, không qua form cấu hình thường.
/// </summary>
public sealed class LockerCabinetResource(ICrudDbContext db)
    : CrudResource<LockerCabinetResource, LockerCabinet, LockerCabinetSearch, LockerCabinetRequest, LockerCabinetDto>(db)
{
    protected override string EntityName => "Tủ đựng đồ";

    protected override string? Describe(LockerCabinet entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<LockerCabinet, LockerCabinetDto>> Projection => x => new LockerCabinetDto(
        x.Id, x.PublicId, x.FloorId, Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.Name).FirstOrDefault(),
        Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.BuildingId).FirstOrDefault(),
        Db.Set<MapBuilding>().Where(b => b.Id == Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.BuildingId).FirstOrDefault())
            .Select(b => b.Name).FirstOrDefault(),
        x.Code, x.Name, x.Rows, x.Cols, x.PositionX, x.PositionY, x.Width, x.Height, x.ColorHex, x.IconName, x.Note,
        x.Status, x.CreatedAt, x.UpdatedAt);

    protected override LockerCabinet Create(LockerCabinetRequest request) =>
        LockerCabinet.Create(request.FloorId, request.Code, request.Name, request.Rows, request.Cols, request.ColorHex, request.IconName, request.Note);

    protected override void Update(LockerCabinet entity, LockerCabinetRequest request) =>
        entity.Update(request.FloorId, request.Code, request.Name, request.Rows, request.Cols, request.ColorHex, request.IconName, request.Note);

    protected override IQueryable<LockerCabinet> Filter(IQueryable<LockerCabinet> query, LockerCabinetSearch search)
    {
        if (search.FloorId is { } floorId) query = query.Where(x => x.FloorId == floorId);
        if (search.BuildingId is { } buildingId)
        {
            var floorIds = Db.Set<MapFloor>().Where(f => f.BuildingId == buildingId).Select(f => f.Id);
            query = query.Where(x => floorIds.Contains(x.FloorId));
        }
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<LockerCabinet> Order(IQueryable<LockerCabinet> query) => query.OrderBy(x => x.FloorId).ThenBy(x => x.Code);

    protected override async Task ValidateAsync(LockerCabinet entity, CancellationToken ct)
    {
        if (!await Db.Set<MapFloor>().AnyAsync(f => f.Id == entity.FloorId, ct))
            throw new BusinessRuleException("FLOOR_NOT_FOUND", "Tầng đã chọn không còn.");
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("CABINET_CODE_EXISTS", $"Mã tủ '{entity.Code}' đã có.");
        // Mã ngăn tự sinh = mã tủ + hậu tố vị trí, tối đa LockerCompartment.MaxCodeLength — kiểm với ô lớn nhất của lưới.
        var longest = LockerCompartment.GeneratedCode(entity.Code, entity.Rows, entity.Cols);
        if (longest.Length > LockerCompartment.MaxCodeLength)
            throw new BusinessRuleException("CABINET_CODE_TOO_LONG", $"Mã tủ quá dài cho lưới {entity.Rows}×{entity.Cols}: mã ngăn '{longest}' vượt {LockerCompartment.MaxCodeLength} ký tự.");
    }

    public override async Task<LockerCabinetDto> AddAsync(LockerCabinetRequest request, CancellationToken ct)
    {
        var dto = await base.AddAsync(request, ct);
        var cabinet = await LoadAsync(dto.PublicId, ct);
        await SyncCompartmentsAsync(cabinet, oldRows: 0, oldCols: 0, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(cabinet);
    }

    public override async Task<LockerCabinetDto> UpdateAsync(Guid publicId, LockerCabinetRequest request, CancellationToken ct)
    {
        var cabinet = await LoadAsync(publicId, ct);
        // Kiểm TRƯỚC khi lưu — tránh lưu Rows/Cols mới rồi mới phát hiện chặn, để lại Cabinet lệch khỏi lưới ngăn thật.
        await EnsureGridShrinkAllowedAsync(cabinet.Id, request.Rows, request.Cols, ct);
        var (oldRows, oldCols) = (cabinet.Rows, cabinet.Cols);
        await base.UpdateAsync(publicId, request, ct);
        await SyncCompartmentsAsync(cabinet, oldRows, oldCols, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(cabinet);
    }

    /// <summary>Đặt vị trí trên sơ đồ tầng (trang sơ đồ trực quan) — không đi qua UpdateAsync/form cấu hình thường.</summary>
    public async Task<LockerCabinetDto> PlaceAsync(Guid publicId, PlaceLockerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await LoadAsync(publicId, ct);
        entity.SetPosition(request.PositionX, request.PositionY, request.Width, request.Height);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    protected override async Task EnsureDeletableAsync(LockerCabinet entity, CancellationToken ct)
    {
        var compartmentIds = Db.Set<LockerCompartment>().Where(c => c.CabinetId == entity.Id).Select(c => c.Id);
        if (await Db.Set<LockerLoan>().AnyAsync(l => compartmentIds.Contains(l.CompartmentId) && l.ReturnedAt == null, ct))
            throw new BusinessRuleException("CABINET_HAS_ACTIVE_LOAN", "Tủ còn ngăn đang được mượn, không thể xoá.");
    }

    /// <summary>Kiểm trước khi đổi lưới — chặn nếu ngăn sẽ bị loại khỏi lưới đang có lượt mượn mở.</summary>
    private async Task EnsureGridShrinkAllowedAsync(long cabinetId, int newRows, int newCols, CancellationToken ct)
    {
        var outOfGridIds = await Db.Set<LockerCompartment>()
            .Where(c => c.CabinetId == cabinetId && (c.RowIndex > newRows || c.ColIndex > newCols))
            .Select(c => c.Id).ToListAsync(ct);
        if (outOfGridIds.Count == 0) return;
        if (await Db.Set<LockerLoan>().AnyAsync(l => outOfGridIds.Contains(l.CompartmentId) && l.ReturnedAt == null, ct))
            throw new BusinessRuleException("GRID_SHRINK_HAS_ACTIVE_LOAN", "Không thể giảm số dòng/cột vì có ngăn đang được mượn.");
    }

    /// <summary>
    /// Sinh/đồng bộ lưới ngăn theo Rows×Cols — chỉ thêm ngăn cho các ô <b>mới lộ ra</b> khi lưới lớn lên (ngoài lưới cũ <paramref name="oldRows"/>×
    /// <paramref name="oldCols"/>), xoá mềm ngăn dư khi lưới nhỏ lại. Ô trong lưới cũ mà không có ngăn là ngăn đã bị xoá có chủ đích — không tự sinh lại
    /// mỗi lần lưu tủ (muốn có lại thì thêm ngăn ở ô đó). Gọi sau khi <see cref="EnsureGridShrinkAllowedAsync"/> đã xác nhận an toàn (UpdateAsync)
    /// hoặc tủ còn mới (AddAsync).
    /// </summary>
    private async Task SyncCompartmentsAsync(LockerCabinet cabinet, int oldRows, int oldCols, CancellationToken ct)
    {
        var existing = await Db.Set<LockerCompartment>().Where(c => c.CabinetId == cabinet.Id).ToListAsync(ct);
        for (var row = 1; row <= cabinet.Rows; row++)
        {
            for (var col = 1; col <= cabinet.Cols; col++)
            {
                if (row <= oldRows && col <= oldCols) continue;
                if (existing.Any(c => c.RowIndex == row && c.ColIndex == col)) continue;
                Db.Set<LockerCompartment>().Add(LockerCompartment.Create(cabinet.Id, row, col, LockerCompartment.GeneratedCode(cabinet.Code, row, col), null, null));
            }
        }
        var outOfGrid = existing.Where(c => c.RowIndex > cabinet.Rows || c.ColIndex > cabinet.Cols).ToList();
        if (outOfGrid.Count > 0) Db.Set<LockerCompartment>().RemoveRange(outOfGrid);
    }
}

public sealed class LockerCompartmentSearch : CrudSearch
{
    public long? CabinetId { get; set; }
}

public sealed record LockerCompartmentRequest(long CabinetId, int RowIndex, int ColIndex, string Code, string? Name, string? Note);

public sealed record LockerCompartmentDto(long Id, Guid PublicId, long CabinetId, int RowIndex, int ColIndex, string Code, string? Name, string? Note,
    int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>
/// Ngăn trong tủ (monolith: PrintBook.CabinetCompartment, quyền SPACE_LOCKERS). Lưới ban đầu sinh tự động theo Rows×Cols
/// của tủ (xem LockerCabinetResource); CRUD đây cho thêm/sửa/xoá riêng từng ngăn (đúng bản gốc).
/// </summary>
public sealed class LockerCompartmentResource(ICrudDbContext db)
    : CrudResource<LockerCompartmentResource, LockerCompartment, LockerCompartmentSearch, LockerCompartmentRequest, LockerCompartmentDto>(db)
{
    protected override string EntityName => "Ngăn tủ";

    protected override string? Describe(LockerCompartment entity) => entity.Code;

    protected override Expression<Func<LockerCompartment, LockerCompartmentDto>> Projection => x =>
        new LockerCompartmentDto(x.Id, x.PublicId, x.CabinetId, x.RowIndex, x.ColIndex, x.Code, x.Name, x.Note, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override LockerCompartment Create(LockerCompartmentRequest request) =>
        LockerCompartment.Create(request.CabinetId, request.RowIndex, request.ColIndex, request.Code, request.Name, request.Note);

    protected override void Update(LockerCompartment entity, LockerCompartmentRequest request) =>
        entity.Update(request.Code, request.Name, request.Note);

    protected override IQueryable<LockerCompartment> Filter(IQueryable<LockerCompartment> query, LockerCompartmentSearch search) =>
        search.CabinetId is { } cabinetId ? query.Where(x => x.CabinetId == cabinetId) : query;

    protected override IOrderedQueryable<LockerCompartment> Order(IQueryable<LockerCompartment> query) => query.OrderBy(x => x.RowIndex).ThenBy(x => x.ColIndex);

    protected override async Task ValidateAsync(LockerCompartment entity, CancellationToken ct)
    {
        if (!await Db.Set<LockerCabinet>().AnyAsync(c => c.Id == entity.CabinetId, ct))
            throw new BusinessRuleException("CABINET_NOT_FOUND", "Tủ đã chọn không còn.");
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("COMPARTMENT_CODE_EXISTS", $"Mã ngăn '{entity.Code}' đã có.");
        var cabinet = await Db.Set<LockerCabinet>().AsNoTracking().Where(c => c.Id == entity.CabinetId).Select(c => new { c.Rows, c.Cols }).FirstAsync(ct);
        if (entity.RowIndex < 1 || entity.RowIndex > cabinet.Rows || entity.ColIndex < 1 || entity.ColIndex > cabinet.Cols)
            throw new BusinessRuleException("COMPARTMENT_OUT_OF_GRID", $"Ô ({entity.RowIndex}, {entity.ColIndex}) nằm ngoài lưới {cabinet.Rows}×{cabinet.Cols} của tủ.");
        if (await Set.AnyAsync(x => x.CabinetId == entity.CabinetId && x.RowIndex == entity.RowIndex && x.ColIndex == entity.ColIndex && x.Id != entity.Id, ct))
            throw new ConflictException("COMPARTMENT_CELL_TAKEN", $"Ô ({entity.RowIndex}, {entity.ColIndex}) đã có ngăn.");
    }

    protected override async Task EnsureDeletableAsync(LockerCompartment entity, CancellationToken ct) => await EnsureNoActiveLoanAsync(entity.Id, ct);

    /// <summary>Chặn chuyển Không hoạt động khi ngăn đang có lượt mượn mở (cải tiến — bản gốc không chặn).</summary>
    protected override async Task OnSavingAsync(LockerCompartment entity, CrudChange change, CancellationToken ct)
    {
        if (change == CrudChange.StatusChanged && entity.Status == IHasStatus.Inactive) await EnsureNoActiveLoanAsync(entity.Id, ct);
    }

    private async Task EnsureNoActiveLoanAsync(long compartmentId, CancellationToken ct)
    {
        if (await Db.Set<LockerLoan>().AnyAsync(l => l.CompartmentId == compartmentId && l.ReturnedAt == null, ct))
            throw new BusinessRuleException("COMPARTMENT_HAS_ACTIVE_LOAN", "Ngăn tủ đang được mượn.");
    }
}

public sealed class LockerLoanSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public long? CompartmentId { get; set; }

    /// <summary>1 = Đang mượn, 2 = Đã trả — suy ra từ ReturnedAt, không lưu field riêng (đúng bản gốc).</summary>
    public int? LoanStatus { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record LockerLoanDto(
    long Id, Guid PublicId, long CompartmentId, string? CompartmentCode, long? CabinetId, string? CabinetCode,
    Guid ReaderPublicId, string CardNo, string? ReaderName, DateTimeOffset BorrowedAt, DateTimeOffset? ReturnedAt, string? Note,
    int LoanStatus, DateTimeOffset CreatedAt);

public sealed record LockerLoanOpUnused;

public sealed record SnapshotLockerRequest(string CardNo);

public sealed record BorrowLockerRequest(string CompartmentCode, string CardNo, string? Note);

public sealed record ReturnLockerRequest(string? CompartmentCode, Guid? LoanPublicId);

/// <summary>
/// Mượn/trả ngăn tủ (monolith: Receiption/BorrowKey, quyền SPACE_LOCKER_LOANS). Không có Add/Update chung — chỉ các
/// thao tác nghiệp vụ (mượn/trả/xem nhanh), đúng khuôn BookingResource/BanResource.
/// </summary>
public sealed class LockerLoanResource(ICrudDbContext db, Replicas replicas, ICurrentActor actor, TimeProvider clock)
    : CrudResource<LockerLoanResource, LockerLoan, LockerLoanSearch, LockerLoanOpUnused, LockerLoanDto>(db)
{
    protected override string EntityName => "Lượt mượn tủ";

    protected override string? Describe(LockerLoan entity) => $"Thẻ {entity.CardNo}";

    protected override Expression<Func<LockerLoan, LockerLoanDto>> Projection => x => new LockerLoanDto(
        x.Id, x.PublicId, x.CompartmentId, Db.Set<LockerCompartment>().Where(c => c.Id == x.CompartmentId).Select(c => c.Code).FirstOrDefault(),
        Db.Set<LockerCompartment>().Where(c => c.Id == x.CompartmentId).Select(c => c.CabinetId).FirstOrDefault(),
        Db.Set<LockerCabinet>().Where(cab => cab.Id == Db.Set<LockerCompartment>().Where(c => c.Id == x.CompartmentId).Select(c => c.CabinetId).FirstOrDefault())
            .Select(cab => cab.Code).FirstOrDefault(),
        x.ReaderPublicId, x.CardNo, Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.BorrowedAt, x.ReturnedAt, x.Note, x.ReturnedAt == null ? 1 : 2, x.CreatedAt);

    protected override LockerLoan Create(LockerLoanOpUnused request) => throw new NotSupportedException("Lượt mượn tủ tạo qua BorrowAsync.");

    protected override void Update(LockerLoan entity, LockerLoanOpUnused request) => throw new NotSupportedException("Lượt mượn tủ sửa qua ReturnAsync.");

    protected override IQueryable<LockerLoan> Filter(IQueryable<LockerLoan> query, LockerLoanSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.CardNo))
        {
            var card = PatronReplica.Key(search.CardNo);
            query = query.Where(x => x.CardNo == card);
        }
        if (search.CompartmentId is { } compartmentId) query = query.Where(x => x.CompartmentId == compartmentId);
        if (search.LoanStatus == 1) query = query.Where(x => x.ReturnedAt == null);
        else if (search.LoanStatus == 2) query = query.Where(x => x.ReturnedAt != null);
        if (search.From is { } from) query = query.Where(x => x.BorrowedAt >= Local(from));
        if (search.To is { } to) query = query.Where(x => x.BorrowedAt < Local(to.AddDays(1)));
        return query;
    }

    protected override IOrderedQueryable<LockerLoan> Order(IQueryable<LockerLoan> query) => query.OrderByDescending(x => x.BorrowedAt).ThenByDescending(x => x.Id);

    /// <summary>Tủ đang giữ của một bạn đọc (theo thẻ) — xem nhanh trước khi mượn/trả.</summary>
    public async Task<IReadOnlyList<LockerLoanDto>> SnapshotAsync(SnapshotLockerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reader = await replicas.ReaderAsync(request.CardNo, ct)
            ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{request.CardNo?.Trim()}'.", 404);
        var loans = await Set.AsNoTracking().Where(l => l.ReaderPublicId == reader.ReaderPublicId && l.ReturnedAt == null).ToListAsync(ct);
        return await ToDtosAsync(loans, ct);
    }

    /// <summary>
    /// Mượn ngăn tủ theo mã ngăn + số thẻ (quét/gõ) — đúng bản gốc: ngăn đang trống của chính bạn đọc này trả lại bản ghi
    /// hiện có (vô hại nếu quét lại), ngăn đang có bạn đọc KHÁC giữ bị chặn.
    /// </summary>
    public async Task<LockerLoanDto> BorrowAsync(BorrowLockerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reader = await replicas.ReaderAsync(request.CardNo, ct)
            ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{request.CardNo?.Trim()}'.", 404);
        var compartment = await RequireCompartmentAsync(request.CompartmentCode, ct);
        if (compartment.Status == IHasStatus.Inactive)
            throw new BusinessRuleException("COMPARTMENT_INACTIVE", "Ngăn tủ đang không hoạt động, không thể mượn.");

        var open = await Set.FirstOrDefaultAsync(l => l.CompartmentId == compartment.Id && l.ReturnedAt == null, ct);
        if (open is not null)
        {
            if (open.ReaderPublicId == reader.ReaderPublicId) return (await ToDtosAsync([open], ct))[0];
            throw new BusinessRuleException("COMPARTMENT_OCCUPIED", $"Ngăn tủ đang được thẻ {open.CardNo} giữ.", 409);
        }

        var loan = LockerLoan.Open(compartment.Id, reader.ReaderPublicId, reader.CardNo, actor.Id, clock.GetUtcNow(), request.Note);
        Set.Add(loan);
        await AuditAsync(loan, CrudChange.Added, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    /// <summary>Trả ngăn tủ — theo mã ngăn (đóng lượt mượn mở hiện có) hoặc trực tiếp theo PublicId lượt mượn.</summary>
    public async Task<LockerLoanDto> ReturnAsync(ReturnLockerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        LockerLoan loan;
        if (request.LoanPublicId is { } publicId)
        {
            loan = await Set.FirstOrDefaultAsync(l => l.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        }
        else
        {
            var compartment = await RequireCompartmentAsync(request.CompartmentCode ?? "", ct);
            loan = await Set.FirstOrDefaultAsync(l => l.CompartmentId == compartment.Id && l.ReturnedAt == null, ct)
                ?? throw new BusinessRuleException("LOAN_NOT_OPEN", "Ngăn tủ hiện không có lượt mượn nào đang mở.");
        }
        loan.Return(actor.Id, clock.GetUtcNow());
        await AuditAsync(loan, CrudChange.StatusChanged, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    private async Task<LockerCompartment> RequireCompartmentAsync(string code, CancellationToken ct)
    {
        var key = (code ?? "").Trim().ToUpperInvariant();
        return await Db.Set<LockerCompartment>().FirstOrDefaultAsync(c => c.Code == key, ct) ?? throw new NotFoundException("Ngăn tủ", key);
    }

    private async Task<IReadOnlyList<LockerLoanDto>> ToDtosAsync(IReadOnlyList<LockerLoan> loans, CancellationToken ct)
    {
        var ids = loans.Select(l => l.Id).ToList();
        return await Set.AsNoTracking().Where(l => ids.Contains(l.Id)).Select(Projection).ToListAsync(ct);
    }

    private static DateTimeOffset Local(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
}
