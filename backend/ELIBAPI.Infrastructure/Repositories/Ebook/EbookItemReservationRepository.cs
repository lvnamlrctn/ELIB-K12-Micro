using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookItemReservationRepository
    : BaseRepository<EbookItemReservation, EbookItemReservationSearchRequest, EbookItemReservationRequest>,
      IEbookItemReservationRepository
{
    private static readonly TimeSpan ReadyWindow = TimeSpan.FromHours(48);
    private readonly IEbookReservationReadyNotifier _readyNotifier;

    public EbookItemReservationRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http, IEbookReservationReadyNotifier readyNotifier)
        : base(ctx, http)
    {
        _readyNotifier = readyNotifier;
    }

    protected override IQueryable<EbookItemReservation> BuildQuery(EbookItemReservationSearchRequest r)
    {
        var q =
            from res in _dbSet
            join item in _context.EbookItems on res.EbookItemId equals item.Id
            join xmlRow in _context.EbookItemXmls on item.Id equals xmlRow.Id into xmlJoin
            from xml in xmlJoin.DefaultIfEmpty()
            join reader in _context.Readers on res.ReaderId equals reader.Id
            where res.IsDelete != 2
            select new { res, xml, reader };

        if (r.EbookItemId.HasValue)       q = q.Where(x => x.res.EbookItemId == r.EbookItemId.Value);
        if (r.ReaderId.HasValue)          q = q.Where(x => x.res.ReaderId == r.ReaderId.Value);
        if (r.ReservationStatus.HasValue) q = q.Where(x => x.res.Status == r.ReservationStatus.Value);
        if (!string.IsNullOrEmpty(r.Keyword))
        {
            var kw = r.Keyword;
            q = q.Where(x =>
                (x.xml != null && x.xml.Title != null && x.xml.Title.Contains(kw)) ||
                (x.reader.Cardno != null && x.reader.Cardno.Contains(kw)) ||
                (x.reader.FirstName != null && x.reader.FirstName.Contains(kw)) ||
                (x.reader.LastName != null && x.reader.LastName.Contains(kw)));
        }

        return q
            .OrderBy(x => x.res.Id)
            .Select(x => new EbookItemReservation
            {
                Id             = x.res.Id,
                EbookItemId    = x.res.EbookItemId,
                ReaderId       = x.res.ReaderId,
                RequestedAt    = x.res.RequestedAt,
                Status         = x.res.Status,
                ReadyAt        = x.res.ReadyAt,
                ReadyExpiresAt = x.res.ReadyExpiresAt,
                FulfilledAt    = x.res.FulfilledAt,
                CancelledAt    = x.res.CancelledAt,
                IsDelete       = x.res.IsDelete,
                CreatedRowBy   = x.res.CreatedRowBy,
                UpdateRowBy    = x.res.UpdateRowBy,
                CreatedRowDate = x.res.CreatedRowDate,
                UpdatedRowDate = x.res.UpdatedRowDate,
                TenantId       = x.res.TenantId,
                PublicId       = x.res.PublicId,
                EbookTitle     = x.xml != null ? x.xml.Title : null,
                ReaderName     = ((x.reader.FirstName ?? "") + " " + (x.reader.LastName ?? "")).Trim(),
                ReaderCardNo   = x.reader.Cardno
            });
    }

    protected override void MapRequestToEntity(EbookItemReservationRequest r, EbookItemReservation e, long userId, bool isNew) { }

    protected override void SoftDelete(EbookItemReservation e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookItemReservation e, int status, long userId) { }

    // ── Nghiệp vụ đặt trước / thăng hạng ─────────────────────────────────────

    public async Task<(bool Ok, string? Error, int StatusCode, EbookItemReservation? Reservation)> ReserveAsync(long ebookItemId, long readerId)
    {
        var now = DateTime.Now;

        var item = await _context.EbookItems.FirstOrDefaultAsync(i => i.Id == ebookItemId && i.IsDelete != 2);
        if (item == null) return (false, "Không tìm thấy tài liệu.", 404, null);

        var hasActiveLoan = await _context.EbookItemLoans.AnyAsync(l =>
            l.EbookItemId == ebookItemId && l.ReaderId == readerId && l.IsDelete != 2 && l.Status == 1
            && (l.ExpiresAt == null || l.ExpiresAt > now));
        if (hasActiveLoan) return (false, "Bạn đang mượn tài liệu này rồi.", 400, null);

        var hasPending = await _dbSet.AnyAsync(r =>
            r.EbookItemId == ebookItemId && r.ReaderId == readerId && r.IsDelete != 2
            && (r.Status == 1 || r.Status == 2));
        if (hasPending) return (false, "Bạn đã có một yêu cầu đặt trước đang chờ cho tài liệu này.", 400, null);

        if (!item.PrintCopies.HasValue || item.PrintCopies.Value <= 0)
            return (false, "Tài liệu này không giới hạn số bản đọc đồng thời, không cần đặt trước.", 400, null);

        var occupiedCount = await GetOccupiedCountAsync(ebookItemId, now);
        if (occupiedCount < item.PrintCopies.Value)
            return (false, "Tài liệu vẫn còn bản trống, vui lòng mượn trực tiếp thay vì đặt trước.", 400, null);

        var reservation = new EbookItemReservation
        {
            EbookItemId    = ebookItemId,
            ReaderId       = readerId,
            RequestedAt    = now,
            Status         = 1,
            CreatedRowDate = now,
            UpdatedRowDate = now,
            TenantId       = GetCurrentTenantId(),
            PublicId       = Guid.NewGuid()
        };
        _dbSet.Add(reservation);
        await _context.SaveChangesAsync();
        return (true, null, 200, reservation);
    }

    public async Task<bool> CancelReservationAsync(Guid reservationPublicId, long readerId)
    {
        var res = await _dbSet.FirstOrDefaultAsync(r =>
            r.PublicId == reservationPublicId && r.ReaderId == readerId && r.IsDelete != 2 && (r.Status == 1 || r.Status == 2));
        if (res == null) return false;

        var now = DateTime.Now;
        res.Status = 4;
        res.CancelledAt = now;
        res.UpdateRowBy = readerId;
        res.UpdatedRowDate = now;
        await _context.SaveChangesAsync();

        await PromoteNextIfSlotAvailableAsync(res.EbookItemId);
        return true;
    }

    public async Task<int> PromoteNextIfSlotAvailableAsync(long ebookItemId)
    {
        var now = DateTime.Now;

        // Hết hạn Ready chưa Mượn → rơi khỏi hàng đợi, coi như slot lại trống.
        var expiredReady = await _dbSet.Where(r =>
            r.EbookItemId == ebookItemId && r.IsDelete != 2 && r.Status == 2
            && r.ReadyExpiresAt != null && r.ReadyExpiresAt < now).ToListAsync();
        foreach (var r in expiredReady) { r.Status = 5; r.UpdatedRowDate = now; }
        if (expiredReady.Count > 0) await _context.SaveChangesAsync();

        var item = await _context.EbookItems.FirstOrDefaultAsync(i => i.Id == ebookItemId && i.IsDelete != 2);
        if (item?.PrintCopies is null or <= 0) return 0;

        var promoted = new List<EbookItemReservation>();
        while (true)
        {
            var occupiedCount = await GetOccupiedCountAsync(ebookItemId, now);
            if (occupiedCount >= item.PrintCopies!.Value) break;

            var next = await _dbSet
                .Where(r => r.EbookItemId == ebookItemId && r.IsDelete != 2 && r.Status == 1)
                .OrderBy(r => r.Id)
                .FirstOrDefaultAsync();
            if (next == null) break;

            next.Status = 2;
            next.ReadyAt = now;
            next.ReadyExpiresAt = now.Add(ReadyWindow);
            next.UpdatedRowDate = now;
            await _context.SaveChangesAsync();
            promoted.Add(next);
        }

        // Gửi email "đến lượt mượn" ngay tại đây — điểm chốt duy nhất của mọi luồng thăng hạng (huỷ đặt
        // trước, trả sớm, job tự động hết hạn, lazy-check khi tải danh sách đặt trước) — thay vì để từng
        // nơi gọi tự lo gửi email (trước đây chỉ đúng 1/4 nơi làm việc này).
        if (promoted.Count > 0) await _readyNotifier.NotifyAsync(promoted);

        return promoted.Count;
    }

    // Đếm cả loan đang Active lẫn reservation đang Ready còn hạn — tránh 2 người cùng được
    // thăng hạng cho 1 slot (slot của người Ready coi như đã bị chiếm cho tới khi hết hạn/Mượn).
    private async Task<int> GetOccupiedCountAsync(long ebookItemId, DateTime now) =>
        await _context.EbookItemLoans.CountAsync(l =>
            l.EbookItemId == ebookItemId && l.IsDelete != 2 && l.Status == 1
            && (l.ExpiresAt == null || l.ExpiresAt > now))
        + await _dbSet.CountAsync(r =>
            r.EbookItemId == ebookItemId && r.IsDelete != 2 && r.Status == 2
            && (r.ReadyExpiresAt == null || r.ReadyExpiresAt > now));
}
