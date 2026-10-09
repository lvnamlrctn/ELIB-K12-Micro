using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookItemLoanRepository
    : BaseRepository<EbookItemLoan, EbookItemLoanSearchRequest, EbookItemLoanRequest>,
      IEbookItemLoanRepository
{
    private readonly IEbookItemReservationRepository _reservationRepo;

    public EbookItemLoanRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http, IEbookItemReservationRepository reservationRepo)
        : base(ctx, http)
    {
        _reservationRepo = reservationRepo;
    }

    protected override IQueryable<EbookItemLoan> BuildQuery(EbookItemLoanSearchRequest r)
    {
        var q =
            from loan in _dbSet
            join item in _context.EbookItems on loan.EbookItemId equals item.Id
            join xmlRow in _context.EbookItemXmls on item.Id equals xmlRow.Id into xmlJoin
            from xml in xmlJoin.DefaultIfEmpty()
            join reader in _context.Readers on loan.ReaderId equals reader.Id
            where loan.IsDelete != 2
            select new { loan, xml, reader };

        if (r.EbookItemId.HasValue) q = q.Where(x => x.loan.EbookItemId == r.EbookItemId.Value);
        if (r.ReaderId.HasValue)    q = q.Where(x => x.loan.ReaderId == r.ReaderId.Value);
        if (r.LoanStatus.HasValue)  q = q.Where(x => x.loan.Status == r.LoanStatus.Value);
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
            .OrderByDescending(x => x.loan.Id)
            .Select(x => new EbookItemLoan
            {
                Id             = x.loan.Id,
                EbookItemId    = x.loan.EbookItemId,
                ReaderId       = x.loan.ReaderId,
                CheckedOutAt   = x.loan.CheckedOutAt,
                ExpiresAt      = x.loan.ExpiresAt,
                LastAccessAt   = x.loan.LastAccessAt,
                Status         = x.loan.Status,
                RecalledAt     = x.loan.RecalledAt,
                RecalledBy     = x.loan.RecalledBy,
                RecallReason   = x.loan.RecallReason,
                IsDelete       = x.loan.IsDelete,
                CreatedRowBy   = x.loan.CreatedRowBy,
                UpdateRowBy    = x.loan.UpdateRowBy,
                CreatedRowDate = x.loan.CreatedRowDate,
                UpdatedRowDate = x.loan.UpdatedRowDate,
                TenantId       = x.loan.TenantId,
                PublicId       = x.loan.PublicId,
                EbookTitle     = x.xml != null ? x.xml.Title : null,
                ReaderName     = ((x.reader.FirstName ?? "") + " " + (x.reader.LastName ?? "")).Trim(),
                ReaderCardNo   = x.reader.Cardno
            });
    }

    protected override void MapRequestToEntity(EbookItemLoanRequest r, EbookItemLoan e, long userId, bool isNew) { }

    protected override void SoftDelete(EbookItemLoan e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookItemLoan e, int status, long userId) { }

    // ── Nghiệp vụ mượn/thu hồi ───────────────────────────────────────────────

    public async Task<(bool Ok, string? Error, int StatusCode, EbookItemLoan? Loan)> CheckoutOrResumeAsync(long ebookItemId, long readerId)
    {
        var now = DateTime.Now;

        var existing = await _dbSet.FirstOrDefaultAsync(l =>
            l.EbookItemId == ebookItemId && l.ReaderId == readerId && l.IsDelete != 2 && l.Status == 1);

        if (existing != null)
        {
            if (existing.ExpiresAt.HasValue && existing.ExpiresAt.Value < now)
                return (false, "Đã hết hạn mượn offline, vui lòng mượn lại.", 410, null);

            existing.LastAccessAt = now;
            await _context.SaveChangesAsync();
            return (true, null, 200, existing);
        }

        var item = await _context.EbookItems.FirstOrDefaultAsync(i => i.Id == ebookItemId && i.IsDelete != 2);
        if (item == null) return (false, "Không tìm thấy tài liệu.", 404, null);

        if (item.PrintCopies.HasValue && item.PrintCopies.Value > 0)
        {
            var activeCount = await _dbSet.CountAsync(l =>
                l.EbookItemId == ebookItemId && l.IsDelete != 2 && l.Status == 1
                && (l.ExpiresAt == null || l.ExpiresAt > now));
            if (activeCount >= item.PrintCopies.Value)
                return (false, $"Tài liệu đã đạt giới hạn {item.PrintCopies.Value} bản đọc đồng thời.", 429, null);
        }

        var offlineDays = await GetEffectiveOfflineDaysAsync(item, readerId);

        var loan = new EbookItemLoan
        {
            EbookItemId    = ebookItemId,
            ReaderId       = readerId,
            CheckedOutAt   = now,
            ExpiresAt      = offlineDays.HasValue ? now.AddDays(offlineDays.Value) : null,
            LastAccessAt   = now,
            Status         = 1,
            CreatedRowDate = now,
            UpdatedRowDate = now,
            TenantId       = GetCurrentTenantId(),
            PublicId       = Guid.NewGuid()
        };
        _dbSet.Add(loan);
        await _context.SaveChangesAsync();
        return (true, null, 200, loan);
    }

    public async Task<bool> ReturnAsync(Guid loanPublicId, long readerId)
    {
        var loan = await _dbSet.FirstOrDefaultAsync(l =>
            l.PublicId == loanPublicId && l.ReaderId == readerId && l.IsDelete != 2 && l.Status == 1);
        if (loan == null) return false;

        var now = DateTime.Now;
        loan.Status         = 3;   // Returned (tự trả) — khác 2 (Recalled, do nhân viên thu hồi)
        loan.RecalledAt      = now;
        loan.RecalledBy      = null;
        loan.RecallReason    = "Reader self-return";
        loan.UpdateRowBy     = readerId;
        loan.UpdatedRowDate  = now;
        await _context.SaveChangesAsync();

        await _reservationRepo.PromoteNextIfSlotAvailableAsync(loan.EbookItemId);
        return true;
    }

    public async Task<(bool Valid, string? Error, int StatusCode)> ValidateAccessAsync(long ebookItemId, long readerId)
    {
        var now = DateTime.Now;
        var loan = await _dbSet
            .Where(l => l.EbookItemId == ebookItemId && l.ReaderId == readerId && l.IsDelete != 2)
            .OrderByDescending(l => l.CheckedOutAt)
            .FirstOrDefaultAsync();

        if (loan == null) return (false, "Bạn chưa có quyền truy cập tài liệu này.", 403);
        if (loan.Status == 2) return (false, "Tài liệu đã bị thu hồi.", 403);
        if (loan.ExpiresAt.HasValue && loan.ExpiresAt.Value < now) return (false, "Đã hết hạn mượn offline, vui lòng mượn lại.", 410);
        return (true, null, 200);
    }

    public async Task<bool> RecallAsync(Guid loanPublicId, long recalledByUserId, string? reason)
    {
        var loan = await _dbSet.FirstOrDefaultAsync(l => l.PublicId == loanPublicId && l.IsDelete != 2);
        if (loan == null || loan.Status != 1) return false;

        var now = DateTime.Now;
        loan.Status         = 2;
        loan.RecalledAt     = now;
        loan.RecalledBy     = recalledByUserId;
        loan.RecallReason   = reason;
        loan.UpdateRowBy    = recalledByUserId;
        loan.UpdatedRowDate = now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> RecallAllActiveForItemAsync(long ebookItemId, long recalledByUserId, string? reason)
    {
        var now = DateTime.Now;
        var loans = await _dbSet
            .Where(l => l.EbookItemId == ebookItemId && l.IsDelete != 2 && l.Status == 1)
            .ToListAsync();

        foreach (var loan in loans)
        {
            loan.Status         = 2;
            loan.RecalledAt     = now;
            loan.RecalledBy     = recalledByUserId;
            loan.RecallReason   = reason;
            loan.UpdateRowBy    = recalledByUserId;
            loan.UpdatedRowDate = now;
        }
        if (loans.Count > 0) await _context.SaveChangesAsync();
        return loans.Count;
    }

    // Tài liệu tự đặt OfflineDays → ưu tiên; không có → mặc định theo (Loại độc giả, Bộ sưu tập)
    // của PolicyDigitalByCollection; không có nữa → không giới hạn.
    private async Task<int?> GetEffectiveOfflineDaysAsync(EbookItem item, long readerId)
    {
        if (item.OfflineDays.HasValue && item.OfflineDays.Value > 0) return item.OfflineDays;
        if (!item.CollectionId.HasValue) return null;

        var readerTypeId = await _context.Readers
            .Where(r => r.Id == readerId && r.IsDelete != 2)
            .Select(r => r.ReaderTypeId)
            .FirstOrDefaultAsync();
        if (!readerTypeId.HasValue) return null;

        var policy = await _context.PolicyDigitalByCollections
            .Where(p => p.CollectionId == (int)item.CollectionId.Value
                     && p.ReaderTypeid == (int)readerTypeId.Value
                     && p.IsDelete != 2)
            .FirstOrDefaultAsync();
        return policy?.OfflineDays is > 0 ? policy.OfflineDays : null;
    }
}
