using System.Globalization;
using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>
/// Lịch sử vào/ra (monolith: Receiption/Check/SearchHistory, quyền CHECK_IN_OUT). <see cref="State"/>: open (đang ở trong),
/// closed (đã quét ra); trống = tất cả. From/To lọc theo ngày quét vào (giờ Việt Nam). <see cref="CircPlaceId"/> khớp cả điểm
/// quét vào hoặc quét ra.
/// </summary>
public sealed class VisitSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public long? CircPlaceId { get; set; }
    public string? State { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record VisitDto(
    long Id, Guid PublicId, Guid ReaderPublicId, string CardNo, string? ReaderName, string? ReaderTypeName, string? ClassName,
    long CircPlaceId, string? CircPlaceName, DateTimeOffset CheckedInAt, DateTimeOffset? CheckedOutAt,
    long? CircPlaceOutId, string? CircPlaceOutName);

public sealed record VisitOpUnused;

public sealed record GateStatusRequest(string CardNo, long? CircPlaceId = null);

/// <summary>Trạng thái vào/ra lồng trên thẻ bạn đọc ở quầy (<see cref="ReaderPanel"/>) — quét cổng không cần tra lại nghiệp vụ mượn/trả.</summary>
public sealed record GateStatus(ReaderPanel Reader, bool CheckedIn, Guid? VisitPublicId, DateTimeOffset? CheckedInAt, int VisitCount);

public sealed record CheckInGateRequest(string CardNo, long CircPlaceId);

public sealed record CheckOutGateRequest(Guid VisitPublicId, long? CircPlaceId = null);

/// <summary>
/// Vào/ra thư viện theo thẻ bạn đọc (monolith: ReceiptionCheckController, quyền CHECK_IN_OUT). Tra bạn đọc qua
/// <see cref="Replicas"/> (bản sao cùng DB với lưu thông — patron down vẫn quét được), thẻ bạn đọc dùng chung
/// <see cref="LoanResource.ReaderPanelAsync"/> như quầy mượn trả. Quét vào 2 lần (đầu đọc lặp) trả lại lượt đang mở, không
/// tạo trùng; quét ra theo đúng lượt đang mở (PublicId trả từ Status/CheckIn).
/// </summary>
public sealed class VisitResource(ICrudDbContext db, Replicas replicas, LoanResource loans, ICurrentActor actor, TimeProvider clock)
    : CrudResource<VisitResource, LibraryVisit, VisitSearch, VisitOpUnused, VisitDto>(db)
{
    protected override string EntityName => "Lượt vào/ra";

    protected override string? Describe(LibraryVisit entity) => $"Thẻ {entity.CardNo}";

    protected override Expression<Func<LibraryVisit, VisitDto>> Projection => x => new VisitDto(
        x.Id, x.PublicId, x.ReaderPublicId, x.CardNo,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.ReaderTypeName).FirstOrDefault(),
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.ClassName).FirstOrDefault(),
        x.CircPlaceId,
        Db.Set<CircPlace>().Where(p => p.Id == x.CircPlaceId).Select(p => p.Name).FirstOrDefault(),
        x.CheckedInAt, x.CheckedOutAt, x.CircPlaceOutId,
        Db.Set<CircPlace>().Where(p => p.Id == x.CircPlaceOutId).Select(p => p.Name).FirstOrDefault());

    protected override LibraryVisit Create(VisitOpUnused request) => throw new NotSupportedException("Lượt vào/ra tạo qua CheckInAsync.");

    protected override void Update(LibraryVisit entity, VisitOpUnused request) => throw new NotSupportedException("Lượt vào/ra sửa qua CheckOutAsync.");

    protected override IQueryable<LibraryVisit> Filter(IQueryable<LibraryVisit> query, VisitSearch s)
    {
        if (!string.IsNullOrWhiteSpace(s.CardNo))
        {
            var card = PatronReplica.Key(s.CardNo);
            query = query.Where(x => x.CardNo == card);
        }
        if (s.CircPlaceId is { } place) query = query.Where(x => x.CircPlaceId == place || x.CircPlaceOutId == place);
        query = (s.State ?? "").Trim().ToLowerInvariant() switch
        {
            "open" => query.Where(x => x.CheckedOutAt == null),
            "closed" => query.Where(x => x.CheckedOutAt != null),
            _ => query,
        };
        if (s.From is { } from) query = query.Where(x => x.CheckedInAt >= Loan.VietnamStart(from));
        if (s.To is { } to) query = query.Where(x => x.CheckedInAt < Loan.VietnamStart(to.AddDays(1)));
        return query;
    }

    protected override IOrderedQueryable<LibraryVisit> Order(IQueryable<LibraryVisit> query) =>
        query.OrderByDescending(x => x.CheckedInAt).ThenByDescending(x => x.Id);

    /// <summary>Thẻ bạn đọc ở cổng: panel lưu thông (như quầy mượn trả) + tình trạng vào/ra hiện tại.</summary>
    public async Task<GateStatus> StatusAsync(GateStatusRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var panel = await loans.ReaderPanelAsync(new ReaderPanelRequest(request.CardNo, request.CircPlaceId), ct);
        var open = await OpenVisitAsync(panel.ReaderPublicId, ct);
        var count = await Set.AsNoTracking().CountAsync(x => x.ReaderPublicId == panel.ReaderPublicId, ct);
        return new GateStatus(panel, open is not null, open?.PublicId, open?.CheckedInAt, count);
    }

    /// <summary>Quét vào (monolith: CheckIn). Quét trùng khi đã có lượt mở → trả lại lượt đó, không tạo thêm.</summary>
    public async Task<VisitDto> CheckInAsync(CheckInGateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var place = await Db.Set<CircPlace>().AsNoTracking().AnyAsync(p => p.Id == request.CircPlaceId, ct);
        if (!place) throw new BusinessRuleException("CIRC_PLACE_REQUIRED", "Chọn điểm lưu thông.");
        var reader = await RequireReaderAsync(request.CardNo, ct);
        if (await OpenVisitAsync(reader.ReaderPublicId, ct) is { } already) return (await ToDtosAsync([already], ct))[0];

        var visit = LibraryVisit.Open(reader, request.CircPlaceId, clock.GetUtcNow(), actor.Id);
        Set.Add(visit);
        await AuditAsync(visit, CrudChange.Added, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([visit], ct))[0];
    }

    /// <summary>Quét ra (monolith: CheckOut) theo đúng lượt đang mở — PublicId trả từ Status/CheckIn.</summary>
    public async Task<VisitDto> CheckOutAsync(CheckOutGateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var visit = await LoadAsync(request.VisitPublicId, ct);
        visit.Close(clock.GetUtcNow(), request.CircPlaceId, actor.Id);
        await AuditAsync(visit, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([visit], ct))[0];
    }

    /// <summary>Xuất Excel lịch sử vào/ra theo bộ lọc đang xem (monolith: ExportHistory), tối đa <see cref="CrudExcel.MaxExportRows"/> dòng.</summary>
    public async Task<byte[]> ExportAsync(VisitSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var rows = await Query(search).Take(CrudExcel.MaxExportRows + 1).Select(Projection).ToListAsync(ct);
        if (rows.Count > CrudExcel.MaxExportRows)
            throw new BusinessRuleException("EXPORT_TOO_LARGE", $"Kết quả quá {CrudExcel.MaxExportRows:N0} lượt — thu hẹp khoảng ngày hoặc bộ lọc.");
        static string At(DateTimeOffset at) => at.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        return CrudExcel.Export<VisitDto>("Lịch sử vào ra",
        [
            new("cardNo", "Số thẻ", v => v.CardNo),
            new("readerName", "Họ tên", v => v.ReaderName),
            new("readerType", "Loại bạn đọc", v => v.ReaderTypeName),
            new("placeIn", "Điểm vào", v => v.CircPlaceName),
            new("checkedInAt", "Giờ vào", v => At(v.CheckedInAt)),
            new("placeOut", "Điểm ra", v => v.CircPlaceOutName),
            new("checkedOutAt", "Giờ ra", v => v.CheckedOutAt is { } c ? At(c) : "Đang ở trong"),
        ], rows);
    }

    private Task<LibraryVisit?> OpenVisitAsync(Guid readerPublicId, CancellationToken ct) =>
        Set.AsNoTracking().Where(x => x.ReaderPublicId == readerPublicId && x.CheckedOutAt == null)
            .OrderByDescending(x => x.CheckedInAt).FirstOrDefaultAsync(ct);

    private async Task<PatronReplica> RequireReaderAsync(string? cardNo, CancellationToken ct) =>
        await replicas.ReaderAsync(cardNo ?? "", ct)
        ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{cardNo?.Trim()}'.", 404);

    private async Task<IReadOnlyList<VisitDto>> ToDtosAsync(IReadOnlyList<LibraryVisit> visits, CancellationToken ct)
    {
        var ids = visits.Select(v => v.Id).ToList();
        return await Set.AsNoTracking().Where(v => ids.Contains(v.Id)).Select(Projection).ToListAsync(ct);
    }
}
