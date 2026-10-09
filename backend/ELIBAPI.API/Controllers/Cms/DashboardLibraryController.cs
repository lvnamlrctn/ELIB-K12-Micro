using System.Security.Claims;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Cms;

/// <summary>
/// Số liệu tổng hợp cho bảng điều khiển quản trị: quy mô kho, phân bố tài liệu số theo loại,
/// xu hướng mượn sách in 12 tháng gần nhất và nhóm tài liệu được mượn nhiều nhất.
///
/// Tách khỏi <see cref="DashboardController"/> (endpoint Summary đã có sẵn KPI/borrowTrend/
/// typeDistribution/top5Borrowed) để không phá hợp đồng đang dùng ở nơi khác — bổ sung thêm các
/// biểu đồ xu hướng khác (đọc số/bổ sung sách in/bổ sung tài liệu số/lượt truy cập). Toàn bộ số
/// liệu đọc thẳng từ bảng nghiệp vụ, không có giá trị mô phỏng nào; thiếu dữ liệu thì trả 0/danh
/// sách rỗng để giao diện nói đúng sự thật.
/// </summary>
[Route("api/Cms/Dashboard")]
[Authorize]
public class DashboardLibraryController(ELIBAPIDbContext db, IPublicCounterRepository counters) : ControllerBase
{
    /// <summary>Số tháng hiển thị trên biểu đồ xu hướng mượn.</summary>
    private const int TrendMonths = 12;

    /// <summary>Số tài liệu trong bảng xếp hạng mượn nhiều nhất.</summary>
    private const int TopBorrowedSize = 5;

    /// <summary>Số ngày hiển thị trên biểu đồ mượn/trả theo ngày.</summary>
    private const int DailyTrendDays = 14;

    // Cùng cách DashboardController.Summary xác định phạm vi tenant: user thường bị ép theo TenantId
    // JWT, user đặc quyền (không có claim TenantId, hoặc role/tenant nằm trong ReadOnlyPolicy) xem
    // toàn bộ (tenantId = null).
    private long? GetEffectiveTenantId()
    {
        var tenantId = long.TryParse(User.FindFirstValue("TenantId"), out var tid) ? tid : (long?)null;

        var config      = HttpContext.RequestServices.GetService<IConfiguration>();
        var roleCode    = User.FindFirstValue("RoleCode");
        var tenantCode  = User.FindFirstValue("TenantCode");
        var adminRoles  = config?.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        var tenantCodes = config?.GetSection("ReadOnlyPolicy:TenantCodes").Get<string[]>() ?? [];
        if ((roleCode != null && adminRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase))
            || (tenantCode != null && tenantCodes.Contains(tenantCode, StringComparer.OrdinalIgnoreCase)))
            return null;

        return tenantId;
    }

    [HttpGet("Library")]
    [Permission("DASHBOARD", "view")]
    public async Task<IActionResult> Library()
    {
        var tenantId = GetEffectiveTenantId();

        var digitalDocs  = await db.EbookItems.CountAsync(x => x.Status == 2 && x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId));
        var printDocs    = await db.Bibs.CountAsync(b => b.IsDelete != 2 && (tenantId == null || b.TenantId == tenantId));
        var printCopies  = await db.Barcodes.CountAsync(c => c.IsDelete != 2 && (tenantId == null || c.TenantId == tenantId));
        var totalDocs    = digitalDocs + printDocs;

        return Ok(ApiResponse<object>.Ok(new
        {
            kpi = new
            {
                totalDocs,
                digitalDocs,
                printDocs,
                printCopies,
                // Tỷ lệ số hóa = đầu tài liệu số / tổng đầu tài liệu. Làm tròn về số nguyên phần trăm.
                digitalRatio = totalDocs == 0 ? 0 : (int)Math.Round(digitalDocs * 100.0 / totalDocs)
            },
            distribution = await BuildDistributionAsync(digitalDocs, tenantId),
            borrowTrend  = await BuildBorrowTrendAsync(tenantId),
            overdueReaders = await BuildOverdueReadersAsync(tenantId),
            dailyTrend   = await BuildDailyTrendAsync(tenantId),
            digitalReads = await BuildDigitalReadsAsync(tenantId),
            printAdded   = await BuildPrintAddedAsync(tenantId),
            digitalAdded = await BuildDigitalAddedAsync(tenantId),
            siteVisits   = await BuildSiteVisitsAsync(tenantId),
            topBorrowed  = await BuildTopBorrowedAsync(tenantId)
        }));
    }

    /// <summary>Số đầu sách tối đa trong danh sách "sách chưa từng mượn" ở tab Biên mục.</summary>
    private const int NeverBorrowedTop = 10;

    /// <summary>Số tuần hiển thị trên biểu đồ biểu ghi mới ở tab Biên mục.</summary>
    private const int CatalogingWeeks = 8;

    /// <summary>Tab Biên mục của dashboard theo vai trò (Đợt 21): số biểu ghi, biểu ghi mới theo tuần và sách chưa từng
    /// mượn (bản ĐKCB chưa có phiếu mượn nào — cùng anti-join theo cặp mã + đơn vị với báo cáo lưu thông loại 9, Đợt 20).
    /// Danh sách xếp đầu sách biên mục sớm nhất lên trước — nằm kho lâu mà chưa ai mượn là tín hiệu thanh lý rõ nhất.
    /// Số cảnh báo chất lượng dữ liệu KHÔNG nằm ở đây: lấy từ <c>api/Dbo/DataQuality/summary</c> (quyền theo từng quy tắc).</summary>
    [HttpGet("Cataloging")]
    [Permission("DASHBOARD", "view")]
    public async Task<IActionResult> Cataloging()
    {
        var tenantId = GetEffectiveTenantId();
        var bibs = db.Bibs.Where(b => b.IsDelete != 2 && (tenantId == null || b.TenantId == tenantId));
        var printTitles = await bibs.CountAsync();

        // Tuần bắt đầu thứ Hai; tuần cuối là tuần hiện tại.
        var today = DateTime.Today;
        var thisWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var since = thisWeek.AddDays(-7 * (CatalogingWeeks - 1));
        var added = await bibs
            .Select(b => b.CreatedTime ?? b.CreatedRowDate)
            .Where(at => at != null && at >= since)
            .ToListAsync();
        var weeklyAdded = Enumerable.Range(0, CatalogingWeeks).Select(i =>
        {
            var start = since.AddDays(7 * i);
            return new { weekStart = start, count = added.Count(at => at >= start && at < start.AddDays(7)) };
        }).ToList();

        var neverBorrowed = db.Barcodes.Where(bc => bc.IsDelete != 2 && (tenantId == null || bc.TenantId == tenantId)
            && !db.BookOuts.Any(bo => bo.IsDelete != 2 && bo.Barcode == bc.BarcodeValue && (bo.TenantId ?? 0) == (bc.TenantId ?? 0)));
        var neverBorrowedCopies = await neverBorrowed.CountAsync();
        // Gom nhóm trước rồi mới JOIN — EF không dịch được "group ... into g join ..." trong cùng 1 biểu thức.
        var copiesPerBib = neverBorrowed.Where(bc => bc.BibId != null)
            .GroupBy(bc => bc.BibId!.Value)
            .Select(g => new { BibId = g.Key, Copies = g.Count() });
        var top = await (
            from g in copiesPerBib
            join b in db.Bibs on g.BibId equals b.Bibid
            where b.IsDelete != 2
            orderby (b.CreatedTime ?? b.CreatedRowDate), b.Bibid
            select new { BibId = b.Bibid, b.Mfn, CatalogedAt = b.CreatedTime ?? b.CreatedRowDate, g.Copies })
            .Take(NeverBorrowedTop).ToListAsync();
        var topIds = top.Select(t => t.BibId).ToList();
        var titles = await db.BibXmls.Where(x => topIds.Contains(x.BibId)).Select(x => new { x.BibId, x.Title, x.Author }).ToListAsync();
        var titleMap = titles.GroupBy(x => x.BibId).ToDictionary(g => g.Key, g => g.First());

        return Ok(ApiResponse<object>.Ok(new
        {
            printTitles,
            weeklyAdded,
            neverBorrowedCopies,
            neverBorrowedTop = top.Select(t => new
            {
                bibId = t.BibId, mfn = t.Mfn, catalogedAt = t.CatalogedAt, copies = t.Copies,
                title = titleMap.GetValueOrDefault(t.BibId)?.Title, author = titleMap.GetValueOrDefault(t.BibId)?.Author,
            }),
        }));
    }

    /// <summary>Tab Bổ sung & Kho của dashboard theo vai trò (Đợt 22.4). Số liệu theo đơn vị hiệu lực,
    /// đúng phạm vi <see cref="GetEffectiveTenantId"/> như các tab khác.</summary>
    [HttpGet("Acquisition")]
    [Permission("DASHBOARD", "view")]
    public async Task<IActionResult> Acquisition()
    {
        var tenantId = GetEffectiveTenantId();

        var orders = db.AbOrders.Where(o => o.IsDelete != 2 && (tenantId == null || o.TenantId == tenantId));
        var ordersByStatus = await orders.GroupBy(o => o.Payment_status)
            .Select(g => new { status = g.Key, count = g.Count() }).ToListAsync();

        var receipts = db.AbReceipts.Where(r => r.IsDelete != 2 && (tenantId == null || r.TenantId == tenantId));
        var receiptsByStatus = await receipts.GroupBy(r => r.Payment_Status)
            .Select(g => new { status = g.Key, count = g.Count() }).ToListAsync();

        // Dòng phiếu nhập CHƯA đủ ĐKCB: số bản đã đăng ký (Barcode.Receipt_Id, cùng tenant) < Amount khai báo.
        // Viết bằng subquery tương quan (COUNT lồng trong WHERE) thay vì LEFT JOIN + GroupBy — EF không dịch
        // được biểu thức 3 ngôi so sánh trên kết quả GroupJoin (InvalidOperationException lúc chạy thử).
        var lines = db.AbReceiptDetails.Where(l => l.IsDelete != 2 && (tenantId == null || l.TenantId == tenantId));
        var totalLines = await lines.CountAsync();
        var sufficientLines = await lines.CountAsync(l =>
            db.Barcodes.Count(b => b.IsDelete != 2 && b.Receipt_Id == l.Id && (tenantId == null || b.TenantId == tenantId)) >= (l.Amount ?? 0));
        var unregisteredLines = totalLines - sufficientLines;

        // Tỷ lệ lấp đầy kho — chỉ kho đã cấu hình Capacity (Đợt 22.4, cột mới); kho chưa cấu hình báo riêng
        // để không hiện "0%" sai lệch (0% và "chưa cấu hình" là 2 trạng thái khác nhau).
        var stores = await db.Stores.Where(s => s.IsDelete != 2 && (tenantId == null || s.TenantId == tenantId))
            .Select(s => new { s.Id, s.Name, s.Capacity }).ToListAsync();
        var storeIds = stores.Select(s => s.Id).ToList();
        var storeCounts = await db.Barcodes
            .Where(b => b.IsDelete != 2 && b.Status == "R" && b.Store != null && storeIds.Contains(b.Store!.Value) && (tenantId == null || b.TenantId == tenantId))
            .GroupBy(b => b.Store!.Value).Select(g => new { StoreId = g.Key, Count = g.Count() }).ToListAsync();
        var storeFillRate = stores.Select(s => new
        {
            storeId = s.Id, name = s.Name, capacity = s.Capacity,
            count = storeCounts.FirstOrDefault(c => c.StoreId == s.Id)?.Count ?? 0,
            fillRate = s.Capacity is > 0 ? Math.Round((storeCounts.FirstOrDefault(c => c.StoreId == s.Id)?.Count ?? 0) * 100.0 / s.Capacity.Value, 1) : (double?)null,
        }).OrderByDescending(s => s.fillRate ?? -1).ToList();

        // Ngân sách bổ sung vs chi tiêu thực tế 12 tháng gần nhất — nhóm theo quỹ (AbReceiptDetail đã có
        // Price/Rate; Submited là thời điểm đăng ký dòng, gần đúng thời điểm chi thực tế).
        var since = DateTime.Today.AddMonths(-11);
        var spending = await (
            from l in db.AbReceiptDetails
            join r in db.AbReceipts on l.Receipt_Id equals r.Id
            where l.IsDelete != 2 && r.IsDelete != 2 && r.FundId != null
                  && (l.Submited ?? l.CreatedRowDate) >= since
                  && (tenantId == null || l.TenantId == tenantId)
            group l by r.FundId!.Value into g
            select new { FundId = g.Key, Spent = g.Sum(x => (x.Amount ?? 0) * (x.Price ?? 0) * (x.Rate ?? 1)) })
            .ToListAsync();
        var fundIds = spending.Select(s => s.FundId).ToList();
        var funds = await db.Funds.Where(f => fundIds.Contains(f.Id)).Select(f => new { f.Id, f.Name, f.Blane }).ToListAsync();
        var budgetVsSpent = spending.Select(s => new
        {
            fundId = s.FundId, name = funds.FirstOrDefault(f => f.Id == s.FundId)?.Name,
            budget = funds.FirstOrDefault(f => f.Id == s.FundId)?.Blane, spent = s.Spent,
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { ordersByStatus, receiptsByStatus, unregisteredLines, storeFillRate, budgetVsSpent }));
    }

    /// <summary>Tab Không gian & Phòng học (Đợt 22.4) — dùng thẳng RoomBooking đã có sẵn đủ trường, không
    /// cần cột mới. No-show = đã duyệt (Status=2, xem RoomBookingController) nhưng CheckedInAt null dù đã
    /// qua EndAt.</summary>
    [HttpGet("Space")]
    [Permission("DASHBOARD", "view")]
    public async Task<IActionResult> Space()
    {
        var tenantId = GetEffectiveTenantId();
        var now = DateTime.Now;

        var bookings = db.RoomBookings.Where(b => b.IsDelete != 2 && (tenantId == null || b.TenantId == tenantId));
        var inUseNow = await bookings.CountAsync(b => b.Status == 2 && b.StartAt <= now && b.EndAt >= now);
        var totalRooms = await db.MapObjects.CountAsync(m => m.IsDelete != 2 && (tenantId == null || m.TenantId == tenantId));

        var since = now.AddDays(-30);
        var recent = await bookings.Where(b => b.EndAt < now && b.EndAt >= since && (b.Status == 2 || b.Status == 4)).ToListAsync();
        var noShowCount = recent.Count(b => b.CheckedInAt == null);
        var noShowRate = recent.Count > 0 ? Math.Round(noShowCount * 100.0 / recent.Count, 1) : (double?)null;

        return Ok(ApiResponse<object>.Ok(new
        {
            roomsInUse = inUseNow, roomsTotal = totalRooms, roomsFree = Math.Max(0, totalRooms - inUseNow),
            noShowCount, noShowSample = recent.Count, noShowRate,
        }));
    }

    /// <summary>Tab DevOps & Vận hành (Đợt 22.4) — CHỈ tài khoản hệ thống (không theo tenant, đúng bản chất
    /// hạ tầng dùng chung mọi đơn vị). Kiểm tra ở controller bằng SystemAdminOnly, không lọc tenant ở đây.</summary>
    [HttpGet("DevOps")]
    [SystemAdminOnly]
    [Permission("DASHBOARD", "view")]
    public async Task<IActionResult> DevOps()
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var rateLimitRejected24h = await db.UserLogs.CountAsync(l => l.ActionType == "RateLimitRejected" && l.Submited >= since);

        var jobCounts = await db.Database.SqlQuery<HangfireStateCount>(
            $"select statename as \"StateName\", count(*) as \"Count\" from hangfire.job group by statename").ToListAsync();

        var storage = await db.DigitalStorageAuditResults.OrderByDescending(x => x.Id).FirstOrDefaultAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            rateLimitRejected24h,
            jobsByState = jobCounts.Select(j => new { state = j.StateName, count = j.Count }),
            digitalStorage = storage == null ? null : new
            {
                runAt = storage.RunAt, totalObjects = storage.TotalObjects, totalSizeBytes = storage.TotalSizeBytes,
                orphanCount = storage.OrphanCount, dbFileCount = storage.DbFileCount,
                brokenFileCount = storage.BrokenFileCount,
            },
        }));
    }

    private sealed class HangfireStateCount { public string? StateName { get; set; } public int Count { get; set; } }

    /// <summary>Số đầu tài liệu số tối đa trong bảng "đọc nhiều nhất" ở tab Tài liệu số.</summary>
    private const int TopReadSize = 10;

    /// <summary>Tab Tài liệu số (Đợt 22.4) — số liệu THEO ĐƠN VỊ, tính trực tiếp từ DB (top đọc nhiều, phân
    /// bố định dạng file, dung lượng đã dùng CỦA ĐƠN VỊ MÌNH). KHÔNG dùng <see cref="DigitalStorageAuditResult"/>
    /// ở đây — kết quả đó đối soát TOÀN BỘ bucket dùng chung mọi đơn vị, không tách được theo tenant; số liệu
    /// mồ côi/hỏng link toàn hệ thống chỉ hiện ở tab DevOps (chỉ tài khoản hệ thống).</summary>
    [HttpGet("Digital")]
    [Permission("DASHBOARD", "view")]
    public async Task<IActionResult> Digital()
    {
        var tenantId = GetEffectiveTenantId();

        var topRead = await db.EbookLogs
            .Where(l => l.Type == 1 && l.IsDelete != 2 && l.Bookid != null && (tenantId == null || l.TenantId == tenantId))
            .GroupBy(l => l.Bookid!.Value)
            .OrderByDescending(g => g.Count())
            .Take(TopReadSize)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToListAsync();
        var topReadIds = topRead.Select(r => r.ItemId).ToList();
        var topReadMeta = await db.EbookItemXmls.Where(x => topReadIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Title, x.Author }).ToListAsync();
        var topReadItems = topRead.Select(r => new
        {
            itemId = r.ItemId, count = r.Count,
            title = topReadMeta.FirstOrDefault(m => m.Id == r.ItemId)?.Title,
            author = topReadMeta.FirstOrDefault(m => m.Id == r.ItemId)?.Author,
        }).ToList();

        var files = db.EbookFiles.Where(f => f.IsDelete != 2 && (tenantId == null || f.TenantId == tenantId));
        var formatDistribution = await files
            .GroupBy(f => (f.FileExt ?? "?").ToLower())
            .Select(g => new { format = g.Key, count = g.Count(), sizeBytes = g.Sum(x => x.FileSize ?? 0) })
            .OrderByDescending(g => g.count).ToListAsync();
        var totalSizeBytes = formatDistribution.Sum(f => f.sizeBytes);

        return Ok(ApiResponse<object>.Ok(new { topRead = topReadItems, formatDistribution, totalSizeBytes }));
    }

    /// <summary>Phân bố ĐẦU tài liệu số theo loại (Ebook.Item.TypeId -> Ebook.DigType).</summary>
    private async Task<object> BuildDistributionAsync(int digitalTotal, long? tenantId)
    {
        var raw = await db.EbookItems
            .Where(x => x.Status == 2 && x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId))
            .GroupBy(x => x.TypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToListAsync();

        var typeIds = raw.Where(r => r.TypeId != null).Select(r => r.TypeId!.Value).Distinct().ToList();
        var typeMap = await db.DigTypes
            .Where(t => typeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.DescriptionVn);

        // Gộp theo TÊN đã quy đổi chứ không theo TypeId: TypeId rỗng và TypeId trỏ tới loại đã bị
        // xoá/để trống tên đều hiển thị "Chưa phân loại", nếu gộp theo id sẽ ra hai dòng trùng tên.
        var items = raw
            .GroupBy(r => r.TypeId != null && typeMap.TryGetValue(r.TypeId.Value, out var n) && !string.IsNullOrWhiteSpace(n)
                ? n!
                : "Chưa phân loại")
            .Select(g => new
            {
                name  = g.Key,
                count = g.Sum(x => x.Count),
                percent = digitalTotal == 0 ? 0 : (int)Math.Round(g.Sum(x => x.Count) * 100.0 / digitalTotal)
            })
            .OrderByDescending(x => x.count)
            .ToList();

        return new { total = digitalTotal, items };
    }

    /// <summary>
    /// Lượt mượn sách in theo từng tháng của 12 tháng gần nhất (tính cả tháng hiện tại).
    /// Tháng không có lượt mượn nào vẫn trả về với giá trị 0 để trục hoành không bị đứt quãng.
    /// </summary>
    private async Task<object> BuildBorrowTrendAsync(long? tenantId)
    {
        var since = WindowStart();

        var raw = await db.BookOuts
            .Where(o => o.IsDelete != 2 && o.BorrowDate != null && o.BorrowDate >= since && (tenantId == null || o.TenantId == tenantId))
            .GroupBy(o => new { o.BorrowDate!.Value.Year, o.BorrowDate!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        return ToSeries(raw.Select(r => (r.Year, r.Month, r.Count)));
    }

    /// <summary>Mốc đầu của cửa sổ 12 tháng: ngày 1 của tháng cách đây 11 tháng.</summary>
    private static DateTime WindowStart()
    {
        var now = DateTime.Now;
        return new DateTime(now.Year, now.Month, 1).AddMonths(-(TrendMonths - 1));
    }

    /// <summary>
    /// Trải các cặp (năm, tháng, số lượng) lên đủ 12 tháng của cửa sổ — tháng không có số liệu
    /// vẫn xuất hiện với giá trị 0 để trục hoành của mọi biểu đồ trùng khớp nhau.
    /// </summary>
    private static object ToSeries(IEnumerable<(int Year, int Month, int Count)> raw)
    {
        var buckets = raw.ToList();
        var start   = WindowStart();
        var points  = Enumerable.Range(0, TrendMonths)
            .Select(i => start.AddMonths(i))
            .Select(m => new
            {
                label = $"Th{m.Month}",
                year  = m.Year,
                month = m.Month,
                value = buckets.FirstOrDefault(b => b.Year == m.Year && b.Month == m.Month).Count
            })
            .ToList();

        return new { points, total = points.Sum(p => p.value) };
    }

    /// <summary>
    /// Số bạn đọc riêng biệt đang có tài liệu in quá hạn — BookOut chỉ giữ đúng phiếu CHƯA trả
    /// (Status == "O", nhất quán với CirculationLoanController.CheckReaderCanBorrowAsync), nên chỉ
    /// cần lọc DueDate đã qua, không cần hợp nhất thêm với BookIn.
    /// </summary>
    private async Task<int> BuildOverdueReadersAsync(long? tenantId)
    {
        var now = DateTime.Now;
        return await db.BookOuts
            .Where(o => o.IsDelete != 2 && o.Status == "O" && o.ReaderId != null
                        && o.DueDate != null && o.DueDate < now && (tenantId == null || o.TenantId == tenantId))
            .Select(o => o.ReaderId)
            .Distinct()
            .CountAsync();
    }

    /// <summary>
    /// Lượt mượn (BookOut.BorrowDate) và lượt trả (BookIn.ReturnDate) theo từng ngày của
    /// <see cref="DailyTrendDays"/> ngày gần nhất (tính cả hôm nay) — bổ sung góc nhìn ngắn hạn bên
    /// cạnh xu hướng theo tháng ở trên, phục vụ xem nhanh hoạt động mỗi sáng.
    /// </summary>
    private async Task<object> BuildDailyTrendAsync(long? tenantId)
    {
        var since = DateTime.Today.AddDays(-(DailyTrendDays - 1));

        var borrowRaw = await db.BookOuts
            .Where(o => o.IsDelete != 2 && o.BorrowDate != null && o.BorrowDate >= since && (tenantId == null || o.TenantId == tenantId))
            .GroupBy(o => o.BorrowDate!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var returnRaw = await db.BookIns
            .Where(i => i.IsDelete != 2 && i.ReturnDate != null && i.ReturnDate >= since && (tenantId == null || i.TenantId == tenantId))
            .GroupBy(i => i.ReturnDate!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var points = Enumerable.Range(0, DailyTrendDays)
            .Select(i => since.AddDays(i))
            .Select(d => new
            {
                label    = d.ToString("dd/MM"),
                date     = d.ToString("yyyy-MM-dd"),
                borrowed = borrowRaw.FirstOrDefault(b => b.Date == d)?.Count ?? 0,
                returned = returnRaw.FirstOrDefault(r => r.Date == d)?.Count ?? 0
            })
            .ToList();

        return new { points };
    }

    /// <summary>Lượt đọc tài liệu số theo tháng (Ebook.EbookLog, Type = 1 — cùng loại log mà
    /// endpoint Summary cũ dùng cho biểu đồ "Lượt xem tài liệu số").</summary>
    private async Task<object> BuildDigitalReadsAsync(long? tenantId)
    {
        var since = WindowStart();
        var raw = await db.EbookLogs
            .Where(x => x.Type == 1 && x.IsDelete != 2 && x.Submited != null && x.Submited >= since && (tenantId == null || x.TenantId == tenantId))
            .GroupBy(x => new { x.Submited!.Value.Year, x.Submited!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        return ToSeries(raw.Select(r => (r.Year, r.Month, r.Count)));
    }

    /// <summary>
    /// Số ĐẦU tài liệu in được bổ sung theo tháng. Mốc thời gian ưu tiên PrintBook.Bib.CreatedTime
    /// (ngày biên mục thật), dự phòng CreatedRowDate cho các biểu ghi cũ không có CreatedTime.
    /// </summary>
    private async Task<object> BuildPrintAddedAsync(long? tenantId)
    {
        var since = WindowStart();
        var raw = await db.Bibs
            .Where(b => b.IsDelete != 2 && (tenantId == null || b.TenantId == tenantId))
            .Select(b => new { At = b.CreatedTime ?? b.CreatedRowDate })
            .Where(x => x.At != null && x.At >= since)
            .GroupBy(x => new { x.At!.Value.Year, x.At!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        return ToSeries(raw.Select(r => (r.Year, r.Month, r.Count)));
    }

    /// <summary>Số tài liệu số được bổ sung theo tháng (Ebook.Item.Submited — ngày đưa lên kho số).</summary>
    private async Task<object> BuildDigitalAddedAsync(long? tenantId)
    {
        var since = WindowStart();
        var raw = await db.EbookItems
            .Where(x => x.IsDelete != 2 && x.Submited != null && x.Submited >= since && (tenantId == null || x.TenantId == tenantId))
            .GroupBy(x => new { x.Submited!.Value.Year, x.Submited!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        return ToSeries(raw.Select(r => (r.Year, r.Month, r.Count)));
    }

    /// <summary>
    /// Lượt truy cập cổng thông tin. Tổng/hôm nay/7 ngày/30 ngày lấy đúng từ
    /// <see cref="IPublicCounterRepository"/> (theo PublicId của tenant hiện tại, null = toàn hệ
    /// thống) để khớp con số đang hiện trên OPAC; chuỗi theo tháng đếm thêm từ cms.Counter, bỏ dòng
    /// lũy kế (dòng Id nhỏ nhất chỉ giữ tổng, không phải 1 lượt).
    /// </summary>
    private async Task<object> BuildSiteVisitsAsync(long? tenantId)
    {
        Guid? tenantPublicId = null;
        if (tenantId.HasValue)
            tenantPublicId = await db.Tenants.Where(t => t.Id == tenantId).Select(t => (Guid?)t.PublicId).FirstOrDefaultAsync();

        var stats = await counters.GetStatsAsync(tenantPublicId);
        var since = WindowStart();

        var accumulatorId = await db.Counters
            .Where(x => x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId))
            .OrderBy(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync();

        var raw = await db.Counters
            .Where(x => x.IsDelete != 2 && x.Id != accumulatorId && x.Submited != null && x.Submited >= since && (tenantId == null || x.TenantId == tenantId))
            .GroupBy(x => new { x.Submited!.Value.Year, x.Submited!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        // Tổng/hôm nay/7 ngày/30 ngày lấy nguyên từ repository (đã đếm theo dòng chi tiết) để
        // bảng điều khiển và OPAC luôn hiển thị cùng một con số.
        return new
        {
            total     = stats.Total,
            today     = stats.Today,
            lastWeek  = stats.LastWeek,
            lastMonth = stats.LastMonth,
            trend     = ToSeries(raw.Select(r => (r.Year, r.Month, r.Count)))
        };
    }

    /// <summary>
    /// Xếp hạng biểu ghi in theo tổng số lượt mượn tích lũy (mỗi dòng BookOut là 1 lượt).
    /// DDC lấy từ biên mục MARC21 trường 082$a, dự phòng bằng BibXml.DDC — cùng thứ tự ưu tiên
    /// mà trang chi tiết tài liệu in đang dùng.
    /// </summary>
    private async Task<object> BuildTopBorrowedAsync(long? tenantId)
    {
        var ranking = await (
            from o in db.BookOuts
            where o.IsDelete != 2 && o.Barcode != null && (tenantId == null || o.TenantId == tenantId)
            join c in db.Barcodes
                on new { K = o.Barcode, T = o.TenantId ?? 0 } equals new { K = c.BarcodeValue, T = c.TenantId ?? 0 }
            where c.IsDelete != 2 && c.BibId != null
            group o by c.BibId!.Value into g
            orderby g.Count() descending
            select new { BibId = g.Key, Count = g.Count() })
            .Take(TopBorrowedSize)
            .ToListAsync();

        if (ranking.Count == 0) return Array.Empty<object>();

        var bibIds = ranking.Select(r => r.BibId).ToList();

        var meta = await (
            from b in db.Bibs
            where bibIds.Contains(b.Bibid) && b.IsDelete != 2
            join x in db.BibXmls on b.Bibid equals x.BibId into gx
            from x in gx.DefaultIfEmpty()
            select new
            {
                b.Bibid,
                b.PublicId,
                Title  = x != null ? x.Title : null,
                Author = x != null ? x.Author : null,
                Ddc    = x != null ? x.DDC : null
            }).ToListAsync();

        var ddc082 = await db.BibDatas
            .Where(d => d.BibId != null && bibIds.Contains(d.BibId.Value)
                        && d.Field == "082" && d.SubField == "a" && d.IsDelete != 2)
            .Select(d => new { d.BibId, d.Data })
            .ToListAsync();

        return ranking.Select(r =>
        {
            var m = meta.FirstOrDefault(x => x.Bibid == r.BibId);
            var realDdc = ddc082.FirstOrDefault(d => d.BibId == r.BibId)?.Data;
            return new
            {
                bibId    = r.BibId,
                publicId = m?.PublicId.ToString() ?? "",
                title    = m?.Title ?? "",
                author   = m?.Author ?? "",
                ddc      = realDdc ?? m?.Ddc ?? "",
                count    = r.Count
            };
        }).ToList();
    }
}
