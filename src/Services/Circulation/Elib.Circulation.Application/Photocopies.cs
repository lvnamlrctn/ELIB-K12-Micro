using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Payment;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>Danh sách phiếu sao chụp. Paid: true đã thanh toán, false chưa. From/To: ngày sao chụp.</summary>
public sealed class PhotocopySearch : CrudSearch
{
    public bool? Paid { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record PhotocopyRequest(
    string CardNo, string Barcode, int FromPage, int ToPage, int Copies, decimal UnitPrice, DateOnly? PhotoDate = null, bool Paid = false, string? Note = null);

public sealed record PhotocopyDto(
    long Id, Guid PublicId, Guid ReaderPublicId, string CardNo, string? ReaderName, string? ClassName, string Barcode, long Mfn, string? Title,
    int FromPage, int ToPage, int Copies, decimal UnitPrice, decimal Total, bool Paid, DateOnly PhotoDate, string? Note, DateTimeOffset CreatedAt)
{
    public string Pages => FromPage == ToPage ? $"{FromPage}" : $"{FromPage}–{ToPage}";
}

public sealed record PhotocopyTotals(int Count, decimal Total, decimal Paid, decimal Unpaid);

public sealed record PhotocopyPaidRequest(Guid PublicId, bool Paid);

/// <summary>Sao chụp tài liệu (monolith: CPhotoController, quyền C_PHOTO). Bạn đọc theo số thẻ, tài liệu theo ĐKCB (bản sao cùng DB).</summary>
public sealed class PhotocopyResource(
    ICrudDbContext db, Replicas replicas, TimeProvider clock, IPublishEndpoint publisher, ITenantContext tenantContext, ICurrentActor actor)
    : CrudResource<PhotocopyResource, Photocopy, PhotocopySearch, PhotocopyRequest, PhotocopyDto>(db)
{
    protected override string EntityName => "Phiếu sao chụp";

    protected override string? Describe(Photocopy entity) =>
        $"Thẻ {entity.CardNo}, ĐKCB {entity.Barcode}, trang {entity.FromPage}–{entity.ToPage} × {entity.Copies} bản";

    protected override Expression<Func<Photocopy, PhotocopyDto>> Projection => x => new PhotocopyDto(
        x.Id, x.PublicId, x.ReaderPublicId, x.CardNo,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.ClassName).FirstOrDefault(),
        x.Barcode, x.Mfn,
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.Mfn).Select(b => b.Title).FirstOrDefault(),
        x.FromPage, x.ToPage, x.Copies, x.UnitPrice, x.Total, x.Paid, x.PhotoDate, x.Note, x.CreatedAt);

    protected override Task OnSavingAsync(Photocopy entity, CrudChange change, CancellationToken ct) =>
        change == CrudChange.Deleted ? PublishWaivedAsync(entity, ct) : PublishChargeAsync(entity, ct);

    /// <summary>Báo dịch vụ thanh toán số còn phải thu của phiếu sao chụp (đã thu = 0).</summary>
    private Task PublishChargeAsync(Photocopy photo, CancellationToken ct, string? justRecordedCode = null)
    {
        if (photo.PublicId == Guid.Empty) photo.PublicId = Guid.CreateVersion7();
        return publisher.Publish(ToCharge(photo, tenantContext.RequireTenantId(), justRecordedCode ?? photo.PaymentCode, new EventActor(actor.Id, actor.Kind)), ct);
    }

    private Task PublishWaivedAsync(Photocopy photo, CancellationToken ct) => publisher.Publish(new ChargeWaived
    {
        TenantId = tenantContext.RequireTenantId(), Actor = new EventActor(actor.Id, actor.Kind), SourceService = "circulation", SourceId = photo.PublicId, Version = photo.Version + 1,
    }, ct);

    public static ChargeIssued ToCharge(Photocopy photo, long tenantId, string? lastPaymentCode, EventActor? actor = null) => new()
    {
        TenantId = tenantId, Actor = actor ?? EventActor.System, SourceService = "circulation", SourceId = photo.PublicId, Kind = "PHOTOCOPY",
        ReaderPublicId = photo.ReaderPublicId, CardNo = photo.CardNo, Label = $"Sao chụp {photo.Barcode} (tr. {photo.FromPage}–{photo.ToPage} × {photo.Copies} bản)",
        Amount = photo.Paid ? 0 : Math.Round(photo.Total, 0, MidpointRounding.AwayFromZero), Version = photo.Version, LastPaymentCode = lastPaymentCode,
    };

    /// <summary>Phiếu sao chụp chưa thu theo id tăng dần — payment dựng khoản thu lần đầu cho phiếu lập trước khi dùng thanh toán.</summary>
    public async Task<(IReadOnlyList<ChargeIssued> Items, long? Next)> OpenChargesAsync(long afterId, int take, CancellationToken ct)
    {
        var size = Math.Clamp(take, 1, 1000);
        var rows = await Set.AsNoTracking().Where(p => p.Id > afterId && !p.Paid && p.Total > 0).OrderBy(p => p.Id).Take(size).ToListAsync(ct);
        var tenantId = tenantContext.RequireTenantId();
        return ([.. rows.Select(p => ToCharge(p, tenantId, p.PaymentCode))], rows.Count == size ? rows[^1].Id : null);
    }

    /// <summary>Tiền về qua cổng thanh toán: đánh dấu đã thu (idempotent theo mã giao dịch). Đã thu bằng cách khác trước đó thì ghi nhật ký cảnh báo cần hoàn tiền.</summary>
    public async Task<bool> ApplyPaymentAsync(PaymentSucceeded e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!string.Equals(e.SourceService, "circulation", StringComparison.OrdinalIgnoreCase) || e.Kind != "PHOTOCOPY") return false;
        var photo = await Set.FirstOrDefaultAsync(p => p.PublicId == e.SourceId, ct);
        if (photo is null) return false;
        if (photo.PaymentCode == e.TransactionCode) return true;
        var alreadyPaid = photo.Paid;
        photo.MarkPaidByGateway(e.TransactionCode);
        await PublishChargeAsync(photo, ct, e.TransactionCode);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Photocopy), EntityName, photo.PublicId, CrudChange.Updated,
                alreadyPaid
                    ? $"CẦN HOÀN TIỀN: phiếu sao chụp đã thu trước đó nhưng tiền {e.Amount:N0}đ vẫn về qua {e.Provider} (mã {e.TransactionCode})"
                    : $"Thu {e.Amount:N0}đ qua {e.Provider} (mã {e.TransactionCode})"), ct);
        await Db.SaveChangesAsync(ct);
        return true;
    }

    protected override Photocopy Create(PhotocopyRequest request) => throw new NotSupportedException("Phiếu sao chụp tạo qua CreateAsync.");

    protected override async Task<Photocopy> CreateAsync(PhotocopyRequest r, CancellationToken ct)
    {
        var (reader, item) = await ResolveAsync(r, ct);
        return Photocopy.Create(reader, item, r.FromPage, r.ToPage, r.Copies, r.UnitPrice, r.PhotoDate ?? Today, r.Paid, r.Note);
    }

    protected override void Update(Photocopy entity, PhotocopyRequest request) => throw new NotSupportedException("Phiếu sao chụp sửa qua UpdateAsync.");

    protected override async Task UpdateAsync(Photocopy entity, PhotocopyRequest r, CancellationToken ct)
    {
        var (reader, item) = await ResolveAsync(r, ct);
        entity.Update(reader, item, r.FromPage, r.ToPage, r.Copies, r.UnitPrice, r.PhotoDate ?? entity.PhotoDate, r.Paid, r.Note);
    }

    protected override IQueryable<Photocopy> Filter(IQueryable<Photocopy> query, PhotocopySearch s)
    {
        if (s.Paid is { } paid) query = query.Where(x => x.Paid == paid);
        if (s.From is { } from) query = query.Where(x => x.PhotoDate >= from);
        if (s.To is { } to) query = query.Where(x => x.PhotoDate <= to);
        if (s.Term is { } term)
        {
            var key = term.ToUpperInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower()/ToUpper() sang SQL
            query = query.Where(x => x.CardNo.StartsWith(key) || x.Barcode.ToUpper().StartsWith(key)
                || Db.Set<PatronReplica>().Any(r => r.ReaderPublicId == x.ReaderPublicId && r.FullName.ToLower().Contains(term))
                || Db.Set<BibSnapshot>().Any(b => b.Mfn == x.Mfn && b.Title.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<Photocopy> Order(IQueryable<Photocopy> query) => query.OrderByDescending(x => x.PhotoDate).ThenByDescending(x => x.Id);

    /// <summary>Tổng thành tiền / đã thu / chưa thu trên toàn bộ kết quả lọc.</summary>
    public async Task<PhotocopyTotals> TotalsAsync(PhotocopySearch search, CancellationToken ct)
    {
        var rows = await Query(search).Select(x => new { x.Total, x.Paid }).ToListAsync(ct);
        return new PhotocopyTotals(rows.Count, rows.Sum(x => x.Total), rows.Where(x => x.Paid).Sum(x => x.Total), rows.Where(x => !x.Paid).Sum(x => x.Total));
    }

    /// <summary>Đánh dấu đã/chưa thanh toán.</summary>
    public async Task<PhotocopyDto> SetPaidAsync(PhotocopyPaidRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var photo = await LoadAsync(request.PublicId, ct);
        photo.SetPaid(request.Paid);
        await PublishChargeAsync(photo, ct);
        await AuditAsync(photo, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return await GetAsync(photo.Id, ct);
    }

    private async Task<(PatronReplica Reader, ItemReplica Item)> ResolveAsync(PhotocopyRequest r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var reader = await replicas.ReaderAsync(r.CardNo ?? "", ct)
            ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{r.CardNo?.Trim()}'.");
        var item = await replicas.ItemAsync(r.Barcode ?? "", ct)
            ?? throw new BusinessRuleException("ITEM_NOT_FOUND", $"Không tìm thấy ĐKCB \"{r.Barcode?.Trim()}\".");
        try
        {
            await replicas.EnsureBibsAsync([item.Mfn], ct);
        }
        catch (HttpRequestException)
        {
            // catalog đang down — phiếu vẫn lập được, chỉ thiếu nhan đề
        }
        return (reader, item);
    }

    private DateOnly Today => Loan.LocalDate(clock.GetUtcNow());
}
