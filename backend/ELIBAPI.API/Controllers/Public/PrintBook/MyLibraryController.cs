using System.Security.Claims;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers;

/// <summary>
/// Sổ mượn của bạn đọc hiện tại cho OPAC, đọc trực tiếp PrintBook.BookOut/BookIn/BookRequest theo
/// ReaderId của người gọi cho SÁCH IN, và Ebook.ItemLoan/ItemReservation cho TÀI LIỆU SỐ.
/// GET api/public/MyLibrary/Borrowing · /History · /Reserved · POST /Hold (sách in)
/// POST /BorrowDigital · /ReturnDigital · /ReserveDigital · GET /DigitalBorrowing · /DigitalHistory
/// · /DigitalReserved · DELETE /CancelDigitalReservation/{publicId} (tài liệu số)
/// </summary>
[ApiController]
[Route("api/public/[controller]")]
[Authorize]
public class MyLibraryController(
    ELIBAPIDbContext db,
    IEbookItemLoanRepository loanRepo,
    IEbookItemReservationRepository reservationRepo,
    INotificationDispatcher notificationDispatcher) : ControllerBase
{
    // Cùng cách PublicEbookController/EbookFileController xác định bạn đọc: NameIdentifier =
    // Reader.PublicId (Guid), phân biệt với nhân viên (Users.Id, long) qua claim "Type".
    private async Task<long?> GetCurrentReaderIdAsync()
    {
        if (User.FindFirstValue("Type") != "Reader") return null;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var readerPublicId)) return null;
        return await db.Readers
            .Where(r => r.PublicId == readerPublicId && r.IsDelete != 2)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync();
    }

    [HttpGet("Borrowing")]
    public async Task<IActionResult> Borrowing()
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        // "Đang mượn" = có BookOut nhưng chưa có BookIn khớp BookOutId (chưa trả).
        var rows = await (
            from o in db.BookOuts
            where o.ReaderId == readerId && o.IsDelete != 2
                  && !db.BookIns.Any(i => i.BookOutId == o.Id && i.IsDelete != 2)
            join c in db.Barcodes
                on new { K = o.Barcode, T = o.TenantId ?? 0 } equals new { K = c.BarcodeValue, T = c.TenantId ?? 0 } into gc
            from c in gc.DefaultIfEmpty()
            join x in db.BibXmls on (c != null ? c.BibId : (long?)null) equals (long?)x.BibId into gx
            from x in gx.DefaultIfEmpty()
            join bb in db.Bibs on (c != null ? c.BibId : (long?)null) equals (long?)bb.Bibid into gb
            from bb in gb.DefaultIfEmpty()
            select new
            {
                o.Id,
                BibId        = bb != null ? (long?)bb.Bibid : null,
                BibPublicId  = bb != null ? (Guid?)bb.PublicId : null,
                Title  = x != null ? x.Title : null,
                Author = x != null ? x.Author : null,
                o.Barcode,
                o.BorrowDate,
                o.DueDate,
                o.Renew
            }).ToListAsync();

        var today = DateTime.Now;
        var items = rows.Select(r => new
        {
            id         = r.Id.ToString(),
            // Định danh của TÀI LIỆU (khác `id` là mã lượt mượn) — để OPAC dựng liên kết
            // sang trang chi tiết tài liệu in.
            bookPublicId = r.BibPublicId?.ToString(),
            bibId        = r.BibId,
            title      = r.Title ?? "",
            author     = r.Author ?? "",
            barcode    = r.Barcode ?? "",
            borrowDate = r.BorrowDate,
            dueDate    = r.DueDate,
            renewCount = r.Renew ?? 0,
            overdue    = r.DueDate.HasValue && r.DueDate.Value.Date < today.Date
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    [HttpGet("History")]
    public async Task<IActionResult> History()
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var rows = await (
            from i in db.BookIns
            where i.ReaderId == readerId && i.IsDelete != 2
            join c in db.Barcodes
                on new { K = i.Barcode, T = i.TenantId ?? 0 } equals new { K = c.BarcodeValue, T = c.TenantId ?? 0 } into gc
            from c in gc.DefaultIfEmpty()
            join x in db.BibXmls on (c != null ? c.BibId : (long?)null) equals (long?)x.BibId into gx
            from x in gx.DefaultIfEmpty()
            join bb in db.Bibs on (c != null ? c.BibId : (long?)null) equals (long?)bb.Bibid into gb
            from bb in gb.DefaultIfEmpty()
            orderby i.ReturnDate descending
            select new
            {
                i.Id,
                BibId       = bb != null ? (long?)bb.Bibid : null,
                BibPublicId = bb != null ? (Guid?)bb.PublicId : null,
                Title  = x != null ? x.Title : null,
                Author = x != null ? x.Author : null,
                i.Barcode,
                i.BorrowDate,
                i.ReturnDate
            }).ToListAsync();

        var items = rows.Select(r => new
        {
            id         = r.Id.ToString(),
            bookPublicId = r.BibPublicId?.ToString(),
            bibId        = r.BibId,
            title      = r.Title ?? "",
            author     = r.Author ?? "",
            barcode    = r.Barcode ?? "",
            borrowDate = r.BorrowDate,
            returnDate = r.ReturnDate
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    private static string StatusLabel(string? status) => status switch
    {
        "pending"  => "Đang chờ lấy",
        "approved" => "Đã duyệt - chờ nhận sách",
        "rejected" => "Đã từ chối",
        _          => status ?? ""
    };

    [HttpGet("Reserved")]
    public async Task<IActionResult> Reserved()
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var rows = await (
            from r in db.BookRequests
            where r.ReaderId == readerId && r.IsDelete != 2
            join c in db.Barcodes
                on new { K = r.Barcode, T = r.TenantId ?? 0 } equals new { K = c.BarcodeValue, T = c.TenantId ?? 0 } into gc
            from c in gc.DefaultIfEmpty()
            join x in db.BibXmls on (c != null ? c.BibId : (long?)null) equals (long?)x.BibId into gx
            from x in gx.DefaultIfEmpty()
            join bb in db.Bibs on (c != null ? c.BibId : (long?)null) equals (long?)bb.Bibid into gb
            from bb in gb.DefaultIfEmpty()
            select new
            {
                r.Id,
                BibId       = bb != null ? (long?)bb.Bibid : null,
                BibPublicId = bb != null ? (Guid?)bb.PublicId : null,
                Title  = x != null ? x.Title : null,
                Author = x != null ? x.Author : null,
                r.Barcode,
                r.CreatedRowDate,
                r.DueDate,
                r.Status
            }).ToListAsync();

        // Vị trí xếp hàng = số yêu cầu đang chờ cho cùng barcode có Id nhỏ hơn hoặc bằng.
        var readerTenantIdForQueue = await db.Readers.Where(x => x.Id == readerId).Select(x => x.TenantId).FirstOrDefaultAsync();
        var barcodes = rows.Select(r => r.Barcode).Where(b => !string.IsNullOrEmpty(b)).Distinct().ToList();
        var pendingByBarcode = await db.BookRequests
            .Where(r => r.Status == "pending" && r.IsDelete != 2 && r.Barcode != null && barcodes.Contains(r.Barcode)
                     && r.TenantId == readerTenantIdForQueue) // hàng đợi chỉ tính trong đơn vị của bạn đọc (Đợt 20)
            .Select(r => new { r.Barcode, r.Id })
            .ToListAsync();

        var items = rows.Select(r => new
        {
            id           = r.Id.ToString(),
            bookPublicId = r.BibPublicId?.ToString(),
            bibId        = r.BibId,
            title        = r.Title ?? "",
            author       = r.Author ?? "",
            status       = StatusLabel(r.Status),
            reserveDate  = r.CreatedRowDate,
            expireDate   = r.DueDate,
            queuePosition = r.Status == "pending"
                ? pendingByBarcode.Count(p => p.Barcode == r.Barcode && p.Id <= r.Id)
                : (int?)null
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    public class HoldRequest
    {
        public long BibId { get; set; }
    }

    [HttpPost("Hold")]
    public async Task<IActionResult> Hold([FromBody] HoldRequest request)
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var bibExists = await db.Bibs.AnyAsync(b => b.Bibid == request.BibId && b.IsDelete != 2);
        if (!bibExists) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài liệu.", 404));

        var alreadyPending = await (
            from r in db.BookRequests
            join c in db.Barcodes
                on new { K = r.Barcode, T = r.TenantId ?? 0 } equals new { K = c.BarcodeValue, T = c.TenantId ?? 0 }
            where r.ReaderId == readerId && r.Status == "pending" && r.IsDelete != 2 && c.BibId == request.BibId
            select r.Id).AnyAsync();
        if (alreadyPending)
            return BadRequest(ApiResponse<object>.Fail("Bạn đã có một yêu cầu đặt mượn đang chờ cho tài liệu này."));

        // ELIB đa tenant: chỉ được đặt mượn bản thuộc đúng tenant của bạn đọc — tránh giữ chỗ nhầm
        // sang bản của tenant khác dùng chung 1 biểu ghi Bib.
        var readerInfo = await db.Readers
            .Where(r => r.Id == readerId.Value)
            .Select(r => new { r.TenantId, r.Phone, r.FirstName, r.LastName })
            .FirstOrDefaultAsync();
        var readerTenantId = readerInfo?.TenantId;

        // Chọn 1 bản còn trống: chưa có BookOut mở (chưa trả) và chưa có BookRequest đang chờ.
        var candidateBarcode = await (
            from c in db.Barcodes
            where c.BibId == request.BibId && c.IsDelete != 2
                  && (readerTenantId == null || c.TenantId == readerTenantId)
                  // Mã ĐKCB chỉ duy nhất trong 1 đơn vị (Đợt 20) — giao dịch của đơn vị khác trùng mã không được tính.
                  && !db.BookOuts.Any(o => o.Barcode == c.BarcodeValue && o.TenantId == c.TenantId && o.IsDelete != 2
                        && !db.BookIns.Any(i => i.BookOutId == o.Id && i.IsDelete != 2))
                  && !db.BookRequests.Any(r => r.Barcode == c.BarcodeValue && r.TenantId == c.TenantId && r.Status == "pending" && r.IsDelete != 2)
            select c.BarcodeValue).FirstOrDefaultAsync();

        if (candidateBarcode == null)
            return BadRequest(ApiResponse<object>.Fail("Hiện không còn bản nào sẵn sàng để đặt mượn."));

        db.BookRequests.Add(new BookRequest
        {
            ReaderId       = readerId,
            Barcode        = candidateBarcode,
            BorrowDate     = DateTime.Now,
            DueDate        = DateTime.Now.AddHours(48),
            Status         = "pending",
            Note           = "Đặt mượn qua OPAC",
            TenantId       = readerTenantId,
            PublicId       = Guid.NewGuid(),
            CreatedRowDate = DateTime.Now
        });
        await db.SaveChangesAsync();

        // Điểm hoàn toàn mới — trước đây Hold không gửi bất kỳ thông báo nào. Chỉ thêm SMS/Zalo, không
        // thêm email (ngoài phạm vi giữ nguyên hành vi hiện có của luồng này).
        var readerName = $"{readerInfo?.FirstName} {readerInfo?.LastName}".Trim();
        var tokens = new Dictionary<string, string>
        {
            ["readerName"] = readerName,
            ["barcode"]    = candidateBarcode,
        };
        // await thật (không fire-and-forget) — NotificationDispatcher dùng chung DbContext theo scope
        // request này, fire-and-forget sẽ khiến DbContext bị dispose giữa chừng khi request kết thúc.
        await notificationDispatcher.DispatchSmsAsync(readerTenantId, readerInfo?.Phone, "PRINT_HOLD_SUCCESS", tokens);
        await notificationDispatcher.DispatchZaloAsync(readerTenantId, readerInfo?.Phone, "PRINT_HOLD_SUCCESS", tokens);

        return Ok(ApiResponse<object>.Ok(new { queued = true }, "Đã ghi nhận yêu cầu đặt mượn."));
    }

    [HttpGet("Stats")]
    public async Task<IActionResult> Stats()
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        // Cùng quy ước với ReadingTrackingController: Type == 1 = log đọc thật (không tính log tải/khác).
        var logsQuery = db.EbookLogs.Where(l => l.ReaderId == readerId && l.Type == 1 && l.IsDelete != 2);

        var totalReads   = await logsQuery.CountAsync();
        var distinctDocs = await logsQuery.Select(l => l.Bookid).Distinct().CountAsync();
        var totalPages   = await logsQuery.SumAsync(l => (long?)l.Page) ?? 0;

        var now = DateTime.Now;
        var thisMonthDocs = await logsQuery
            .Where(l => l.Submited.HasValue && l.Submited.Value.Year == now.Year && l.Submited.Value.Month == now.Month)
            .Select(l => l.Bookid).Distinct().CountAsync();

        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
        var monthlyRaw = await logsQuery
            .Where(l => l.Submited.HasValue && l.Submited.Value >= sixMonthsAgo)
            .GroupBy(l => new { l.Submited!.Value.Year, l.Submited!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Reads = g.Count(), Pages = g.Sum(x => (long?)x.Page) ?? 0 })
            .ToListAsync();

        var monthly = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var d = now.AddMonths(-i);
            var m = monthlyRaw.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
            monthly.Add(new { year = d.Year, month = d.Month, label = $"Th{d.Month}", reads = m?.Reads ?? 0, pages = m?.Pages ?? 0 });
        }

        // Lấy dư 200 dòng log gần nhất rồi rút gọn còn tài liệu phân biệt phía client (tránh
        // GroupBy lồng join phức tạp trên SQL) — đủ cho top-10 tài liệu đọc gần nhất thực tế.
        var recentRows = await (
            from log in db.EbookLogs
            where log.ReaderId == readerId && log.Type == 1 && log.IsDelete != 2
            join xml in db.EbookItemXmls on log.Bookid equals xml.Id into gx
            from xml in gx.DefaultIfEmpty()
            join item in db.EbookItems on log.Bookid equals (long?)item.Id into gi
            from item in gi.DefaultIfEmpty()
            orderby log.Submited descending
            select new
            {
                log.Bookid,
                Title    = xml != null ? xml.Title : null,
                Author   = xml != null ? xml.Author : null,
                PublicId = item != null ? item.PublicId : (Guid?)null,
                log.Submited,
                log.Page
            }).Take(200).ToListAsync();

        var recentReads = recentRows
            .GroupBy(r => r.Bookid)
            .Select(g => g.First())
            .Take(10)
            .Select(r => new
            {
                id     = r.PublicId?.ToString() ?? "",
                title  = r.Title ?? "",
                author = r.Author ?? "",
                date   = r.Submited,
                page   = r.Page ?? 0
            }).ToList();

        return Ok(ApiResponse<object>.Ok(new { totalReads, distinctDocs, totalPages, thisMonthDocs, monthly, recentReads }));
    }

    /// <summary>
    /// Huy hiệu đọc (Gamification) của bạn đọc hiện tại — đã đạt + đang tiến tới (chưa đạt, kèm tiến độ
    /// hiện tại/ngưỡng). Tắt qua SystemParameter GAMIFICATION_ENABLED (theo tenant của bạn đọc) thì trả
    /// enabled:false, frontend không hiện tab "Huy hiệu". Danh mục Badge tenant-scoped — chỉ đối chiếu
    /// huy hiệu do đúng tenant của bạn đọc định nghĩa. Tiến độ tính theo cùng mốc GAMIFICATION_ENABLED_AT
    /// (theo tenant) mà BadgeEvaluationJob dùng — không tính hồi tố hoạt động trước khi bật tính năng.
    /// </summary>
    [HttpGet("Badges")]
    public async Task<IActionResult> Badges([FromServices] ISystemParameterService sysParam)
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var readerTenantId = await db.Readers
            .Where(r => r.Id == readerId.Value)
            .Select(r => r.TenantId)
            .FirstOrDefaultAsync();

        if (!await sysParam.IsEnabledAsync("GAMIFICATION_ENABLED", readerTenantId))
            return Ok(ApiResponse<object>.Ok(new { enabled = false, earned = Array.Empty<object>(), inProgress = Array.Empty<object>() }));

        var earnedRows = await (
            from rb in db.ReaderBadges
            where rb.ReaderId == readerId && rb.IsDelete != 2
            join b in db.Badges on rb.BadgeId equals b.Id
            orderby rb.EarnedAt descending
            select new { rb.EarnedAt, b.Id, b.Code, b.Name, b.Description, b.IconName }
        ).ToListAsync();

        var earned = earnedRows.Select(r => new
        {
            badgeId  = r.Id,
            code     = r.Code,
            name     = r.Name ?? "",
            description = r.Description ?? "",
            iconName = r.IconName ?? "military_tech",
            earnedAt = r.EarnedAt
        }).ToList();

        var earnedBadgeIds = earnedRows.Select(r => r.Id).ToHashSet();

        var cutoffRaw = await sysParam.GetValueAsync("GAMIFICATION_ENABLED_AT", readerTenantId);
        var cutoffCleaned = cutoffRaw == null ? null : System.Text.RegularExpressions.Regex.Replace(cutoffRaw, "<.*?>", "").Trim();
        var cutoff = DateTime.TryParse(cutoffCleaned, out var cutoffDt) ? cutoffDt : DateTime.Now;

        var activeBadges = await db.Badges
            .Where(b => b.IsDelete != 2 && b.Status == 2 && b.TenantId == readerTenantId && !earnedBadgeIds.Contains(b.Id))
            .ToListAsync();

        var inProgress = new List<object>();
        foreach (var badge in activeBadges)
        {
            if (string.IsNullOrEmpty(badge.CriteriaType) || !badge.Threshold.HasValue) continue;

            long current = badge.CriteriaType switch
            {
                "TotalDigitalReads" => await db.EbookLogs.CountAsync(x =>
                    x.ReaderId == readerId && x.Type == 1 && x.IsDelete != 2 && x.Submited != null && x.Submited >= cutoff),
                "DistinctDigitalTitles" => await db.EbookLogs
                    .Where(x => x.ReaderId == readerId && x.Type == 1 && x.IsDelete != 2 && x.Submited != null && x.Submited >= cutoff)
                    .Select(x => x.Bookid).Distinct().CountAsync(),
                "TotalPagesRead" => await db.EbookLogs
                    .Where(x => x.ReaderId == readerId && x.Type == 1 && x.IsDelete != 2 && x.Submited != null && x.Submited >= cutoff)
                    .SumAsync(x => (long?)x.Page) ?? 0,
                "TotalPrintBorrows" => await db.BookOuts.CountAsync(x =>
                    x.ReaderId == readerId && x.IsDelete != 2 && x.BorrowDate != null && x.BorrowDate >= cutoff),
                _ => 0
            };

            inProgress.Add(new
            {
                badgeId   = badge.Id,
                code      = badge.Code,
                name      = badge.Name ?? "",
                description = badge.Description ?? "",
                iconName  = badge.IconName ?? "military_tech",
                current,
                threshold = badge.Threshold.Value
            });
        }

        return Ok(ApiResponse<object>.Ok(new { enabled = true, earned, inProgress }));
    }

    // ── Mượn tài liệu số (Ebook.ItemLoan / ItemReservation) ───────────────────

    [HttpPost("BorrowDigital")]
    public async Task<IActionResult> BorrowDigital([FromBody] BorrowDigitalRequest request)
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var ebookItemId = await db.EbookItems
            .Where(e => e.PublicId == request.EbookItemPublicId && e.IsDelete != 2)
            .Select(e => (long?)e.Id).FirstOrDefaultAsync();
        if (ebookItemId == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài liệu.", 404));

        var (ok, error, statusCode, loan) = await loanRepo.CheckoutOrResumeAsync(ebookItemId.Value, readerId.Value);
        if (!ok) return StatusCode(statusCode, ApiResponse<object>.Fail(error ?? "Không thể mượn tài liệu.", statusCode));

        return Ok(ApiResponse<object>.Ok(new { loanPublicId = loan!.PublicId, expiresAt = loan.ExpiresAt }, "Đã mượn tài liệu số."));
    }

    [HttpPost("ReturnDigital")]
    public async Task<IActionResult> ReturnDigital([FromBody] ReturnDigitalRequest request)
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var ok = await loanRepo.ReturnAsync(request.LoanPublicId, readerId.Value);
        return ok
            ? Ok(ApiResponse<object>.Ok(null!, "Đã trả tài liệu số."))
            : NotFound(ApiResponse<object>.Fail("Không tìm thấy lượt mượn đang hoạt động.", 404));
    }

    [HttpGet("DigitalBorrowing")]
    public async Task<IActionResult> DigitalBorrowing()
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));
        var now = DateTime.Now;

        var rows = await (
            from l in db.EbookItemLoans
            where l.ReaderId == readerId && l.IsDelete != 2 && l.Status == 1
                  && (l.ExpiresAt == null || l.ExpiresAt > now)
            join item in db.EbookItems on l.EbookItemId equals item.Id into gi
            from item in gi.DefaultIfEmpty()
            join xml in db.EbookItemXmls on l.EbookItemId equals xml.Id into gx
            from xml in gx.DefaultIfEmpty()
            select new
            {
                l.Id,
                EbookPublicId = item != null ? (Guid?)item.PublicId : null,
                Title  = xml != null ? xml.Title : null,
                Author = xml != null ? xml.Author : null,
                l.CheckedOutAt,
                l.ExpiresAt,
                LoanPublicId = l.PublicId
            }).ToListAsync();

        var items = rows.Select(r => new
        {
            id           = r.Id.ToString(),
            loanPublicId = r.LoanPublicId.ToString(),
            ebookPublicId = r.EbookPublicId?.ToString(),
            title      = r.Title ?? "",
            author     = r.Author ?? "",
            borrowDate = r.CheckedOutAt,
            dueDate    = r.ExpiresAt,
            overdue    = r.ExpiresAt.HasValue && r.ExpiresAt.Value < now
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    [HttpGet("DigitalHistory")]
    public async Task<IActionResult> DigitalHistory()
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));
        var now = DateTime.Now;

        // Lịch sử = đã Recall(2)/Return(3), HOẶC Active nhưng đã hết hạn ExpiresAt.
        var rows = await (
            from l in db.EbookItemLoans
            where l.ReaderId == readerId && l.IsDelete != 2
                  && (l.Status == 2 || l.Status == 3 || (l.Status == 1 && l.ExpiresAt != null && l.ExpiresAt < now))
            join item in db.EbookItems on l.EbookItemId equals item.Id into gi
            from item in gi.DefaultIfEmpty()
            join xml in db.EbookItemXmls on l.EbookItemId equals xml.Id into gx
            from xml in gx.DefaultIfEmpty()
            orderby l.Id descending
            select new
            {
                l.Id,
                EbookPublicId = item != null ? (Guid?)item.PublicId : null,
                Title  = xml != null ? xml.Title : null,
                Author = xml != null ? xml.Author : null,
                l.CheckedOutAt,
                l.Status,
                ReturnDate = l.RecalledAt
            }).ToListAsync();

        var items = rows.Select(r => new
        {
            id            = r.Id.ToString(),
            ebookPublicId = r.EbookPublicId?.ToString(),
            title         = r.Title ?? "",
            author        = r.Author ?? "",
            borrowDate    = r.CheckedOutAt,
            returnDate    = r.ReturnDate,
            selfReturned  = r.Status == 3
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    [HttpPost("ReserveDigital")]
    public async Task<IActionResult> ReserveDigital([FromBody] BorrowDigitalRequest request)
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var ebookItemId = await db.EbookItems
            .Where(e => e.PublicId == request.EbookItemPublicId && e.IsDelete != 2)
            .Select(e => (long?)e.Id).FirstOrDefaultAsync();
        if (ebookItemId == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài liệu.", 404));

        var (ok, error, statusCode, reservation) = await reservationRepo.ReserveAsync(ebookItemId.Value, readerId.Value);
        if (!ok) return StatusCode(statusCode, ApiResponse<object>.Fail(error ?? "Không thể đặt trước.", statusCode));
        return Ok(ApiResponse<object>.Ok(new { reservationPublicId = reservation!.PublicId }, "Đã ghi nhận yêu cầu đặt trước tài liệu số."));
    }

    [HttpGet("DigitalReserved")]
    public async Task<IActionResult> DigitalReserved()
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var myItemIds = await db.EbookItemReservations
            .Where(r => r.ReaderId == readerId && r.IsDelete != 2 && (r.Status == 1 || r.Status == 2))
            .Select(r => r.EbookItemId).Distinct().ToListAsync();
        foreach (var id in myItemIds) await reservationRepo.PromoteNextIfSlotAvailableAsync(id);

        var rows = await (
            from r in db.EbookItemReservations
            where r.ReaderId == readerId && r.IsDelete != 2 && (r.Status == 1 || r.Status == 2)
            join item in db.EbookItems on r.EbookItemId equals item.Id into gi
            from item in gi.DefaultIfEmpty()
            join xml in db.EbookItemXmls on r.EbookItemId equals xml.Id into gx
            from xml in gx.DefaultIfEmpty()
            select new
            {
                r.Id,
                r.PublicId,
                EbookPublicId = item != null ? (Guid?)item.PublicId : null,
                Title  = xml != null ? xml.Title : null,
                Author = xml != null ? xml.Author : null,
                r.EbookItemId,
                r.RequestedAt,
                r.Status,
                r.ReadyExpiresAt
            }).ToListAsync();

        var itemIds = rows.Select(x => x.EbookItemId).Distinct().ToList();
        var pendingByItem = await db.EbookItemReservations
            .Where(r => r.Status == 1 && r.IsDelete != 2 && itemIds.Contains(r.EbookItemId))
            .Select(r => new { r.EbookItemId, r.Id }).ToListAsync();

        var items = rows.Select(r => new
        {
            id                  = r.Id.ToString(),
            reservationPublicId = r.PublicId.ToString(),
            ebookPublicId       = r.EbookPublicId?.ToString(),
            title               = r.Title ?? "",
            author              = r.Author ?? "",
            reserveDate         = r.RequestedAt,
            status              = r.Status == 2 ? "ready" : "pending",
            readyExpiresAt      = r.ReadyExpiresAt,
            queuePosition       = r.Status == 1
                ? pendingByItem.Count(p => p.EbookItemId == r.EbookItemId && p.Id <= r.Id)
                : (int?)null
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    [HttpDelete("CancelDigitalReservation/{publicId:guid}")]
    public async Task<IActionResult> CancelDigitalReservation(Guid publicId)
    {
        var readerId = await GetCurrentReaderIdAsync();
        if (readerId == null) return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var ok = await reservationRepo.CancelReservationAsync(publicId, readerId.Value);
        return ok
            ? Ok(ApiResponse<object>.Ok(null!, "Đã huỷ đặt trước."))
            : NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu đặt trước đang chờ.", 404));
    }
}

public class BorrowDigitalRequest { public Guid EbookItemPublicId { get; set; } }
public class ReturnDigitalRequest { public Guid LoanPublicId { get; set; } }
