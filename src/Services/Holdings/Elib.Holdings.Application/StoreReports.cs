using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>
/// Thống kê tài liệu kho. Criterion: status | store | ddc | bibtype (mặc định). From/To: ngày nhận (xem <see cref="StoreReports"/>).
/// </summary>
public sealed record DocumentStatsRequest(string? Criterion = null, DateOnly? From = null, DateOnly? To = null, long? StoreId = null);

/// <summary>Titles: số biểu ghi (đầu sách), Copies: số bản.</summary>
public sealed record DocumentStatRow(string Key, string Label, int Titles, int Copies);

public sealed record DocumentStats(string Criterion, IReadOnlyList<DocumentStatRow> Rows, int Titles, int Copies);

/// <summary>Báo cáo sách trong kho: bản chưa mất/chưa thanh lý, lọc theo kho và ngày nhận.</summary>
public sealed record StoreBookReportRequest(long? StoreId = null, DateOnly? From = null, DateOnly? To = null, int PageIndex = 1, int PageSize = 50);

/// <summary><see cref="ReceivedOn"/>/<see cref="ReceiptCode"/>: ngày nhận và số của phiếu nhận đã đăng ký bản (null = đăng ký không qua phiếu nhận).</summary>
public sealed record StoreBookRow(string Barcode, long Mfn, string? Title, string? Author, string? Ddc, DateOnly RegisteredOn, string? StoreName, string Status,
    bool OnLoan, DateOnly? ReceivedOn = null, long? ReceiptCode = null);

public sealed record StoreBookStat(long? StoreId, string StoreName, int Titles, int Copies);

public sealed record StoreBookReport(IReadOnlyList<StoreBookRow> Rows, int TotalCount, int TitleCount, IReadOnlyList<StoreBookStat> StoreStats, string? LibraryName);

/// <summary>
/// Thống kê và báo cáo kho (monolith: StoreStatisticsController quyền STAT_DOC, StoreBookReportController quyền STORE_BOOK_REPORT).
/// Đếm trên bản sao biểu ghi trong DB holdings — không gọi catalog. Lọc ngày theo NGÀY NHẬN như ELIB-LRC ("sách đã nhận —
/// ab_receipt.Receipt_Date — trong khoảng ngày"): bản đăng ký theo phiếu nhận lấy ngày nhận của phiếu (bản sao từ acquisition); bản
/// đăng ký thẳng ở biên mục / dữ liệu cũ không có phiếu thì lấy ngày đăng ký cá biệt (monolith lọc mọi bản theo ngày tạo bản ghi).
/// </summary>
public sealed class StoreReports(ICrudDbContext db, HoldingsExcel excel)
{
    public const string ByStatus = "status";
    public const string ByStore = "store";
    public const string ByDdc = "ddc";
    public const string ByBibType = "bibtype";

    /// <summary>10 lớp chính DDC (bản tóm tắt thứ nhất) — monolith khớp theo khung phân loại tự nhập; ở đây cố định theo lớp chính.</summary>
    public static readonly IReadOnlyDictionary<char, string> DdcClasses = new Dictionary<char, string>
    {
        ['0'] = "Tin học, thông tin & tác phẩm tổng quát",
        ['1'] = "Triết học & tâm lý học",
        ['2'] = "Tôn giáo",
        ['3'] = "Khoa học xã hội",
        ['4'] = "Ngôn ngữ",
        ['5'] = "Khoa học tự nhiên & toán học",
        ['6'] = "Công nghệ (khoa học ứng dụng)",
        ['7'] = "Nghệ thuật & vui chơi giải trí",
        ['8'] = "Văn học",
        ['9'] = "Lịch sử & địa lý",
    };

    private const string Unclassified = "Chưa phân loại";

    public async Task<DocumentStats> DocumentAsync(DocumentStatsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        HoldingsExcel.EnsureRange(request.From, request.To);
        var criterion = (request.Criterion ?? ByBibType).Trim().ToLowerInvariant();
        var items = Filter(db.Set<Item>().AsNoTracking(), request.StoreId, request.From, request.To);
        List<DocumentStatRow> rows;
        switch (criterion)
        {
            case ByStatus:
            {
                var groups = await items.GroupBy(i => i.Status)
                    .Select(g => new { g.Key, Titles = g.Select(i => i.BibId).Distinct().Count(), Copies = g.Count() }).ToListAsync(ct);
                rows = groups.Select(g => new DocumentStatRow(g.Key, ItemStatus.Names.GetValueOrDefault(g.Key, g.Key), g.Titles, g.Copies))
                    .OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
                break;
            }
            case ByStore:
            {
                var stores = await db.Set<Store>().AsNoTracking().ToDictionaryAsync(s => s.Id, s => $"{s.Code} — {s.Name}", ct);
                var groups = await items.GroupBy(i => i.StoreId)
                    .Select(g => new { g.Key, Titles = g.Select(i => i.BibId).Distinct().Count(), Copies = g.Count() }).ToListAsync(ct);
                rows = groups.Select(g => new DocumentStatRow(g.Key?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                        g.Key is { } id && stores.TryGetValue(id, out var name) ? name : "(Chưa chọn kho)", g.Titles, g.Copies))
                    .OrderBy(r => r.Label, StringComparer.CurrentCulture).ToList();
                break;
            }
            case ByDdc or ByBibType:
            {
                // Đếm bản theo biểu ghi trong DB, gộp nhóm (lớp DDC / loại tài liệu) ở bộ nhớ — mỗi biểu ghi một dòng.
                var perBib = await (
                    from g in items.GroupBy(i => i.BibPublicId).Select(g => new { BibPublicId = g.Key, Copies = g.Count() })
                    join b in db.Set<BibSnapshot>() on g.BibPublicId equals b.BibPublicId into bj
                    from b in bj.DefaultIfEmpty()
                    select new { g.Copies, Ddc = b == null ? null : b.Ddc, TypeId = b == null ? null : b.BibTypeId, TypeName = b == null ? null : b.BibTypeName })
                    .ToListAsync(ct);
                rows = criterion == ByDdc
                    ? perBib.GroupBy(x => DdcKey(x.Ddc))
                        .Select(g => new DocumentStatRow(g.Key, g.Key == "" ? Unclassified : $"{g.Key}00 - {DdcClasses[g.Key[0]]}", g.Count(), g.Sum(x => x.Copies)))
                        .OrderBy(r => r.Key == "" ? "~" : r.Key, StringComparer.Ordinal).ToList()
                    : perBib.GroupBy(x => (x.TypeId, x.TypeName))
                        .Select(g => new DocumentStatRow(g.Key.TypeId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                            g.Key.TypeName ?? "(Chưa xác định loại)", g.Count(), g.Sum(x => x.Copies)))
                        .OrderByDescending(r => r.Copies).ToList();
                break;
            }
            default:
                throw new BusinessRuleException("CRITERION_INVALID", $"Tiêu chí thống kê '{request.Criterion}' không hợp lệ.");
        }
        return new DocumentStats(criterion, rows, await items.Select(i => i.BibId).Distinct().CountAsync(ct), await items.CountAsync(ct));
    }

    public async Task<ExcelFile> DocumentExportAsync(DocumentStatsRequest request, CancellationToken ct)
    {
        var stats = await DocumentAsync(request, ct);
        var title = stats.Criterion switch
        {
            ByStatus => "THỐNG KÊ TÀI LIỆU THEO TRẠNG THÁI",
            ByStore => "THỐNG KÊ TÀI LIỆU THEO KHO",
            ByDdc => "THỐNG KÊ TÀI LIỆU THEO PHÂN LOẠI DDC",
            _ => "THỐNG KÊ TÀI LIỆU THEO LOẠI TÀI LIỆU",
        };
        return await excel.BuildAsync("thong-ke-tai-lieu", ct, new ExcelSheet("Thống kê tài liệu", title, HoldingsExcel.Period(request.From, request.To),
            ["Tiêu chí", "Số biểu ghi", "Số bản"],
            [.. stats.Rows.Select(r => (IReadOnlyList<object?>)[r.Label, r.Titles, r.Copies]), ["Tổng cộng", stats.Titles, stats.Copies]]));
    }

    public async Task<StoreBookReport> BookReportAsync(StoreBookReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var items = BookReportItems(request);
        var (page, size) = (Math.Max(1, request.PageIndex), Math.Clamp(request.PageSize, 1, 500));
        var rows = await BookRows(items).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new StoreBookReport(rows, await items.CountAsync(ct), await items.Select(i => i.BibId).Distinct().CountAsync(ct),
            await StoreStatsAsync(items, ct), await excel.LibraryNameAsync(ct));
    }

    public async Task<ExcelFile> BookReportExportAsync(StoreBookReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var items = BookReportItems(request);
        var rows = await BookRows(items).Take(HoldingsExcel.MaxRows).ToListAsync(ct);
        var stats = await StoreStatsAsync(items, ct);
        var period = HoldingsExcel.Period(request.From, request.To);
        return await excel.BuildAsync("sach-trong-kho", ct,
            new ExcelSheet("Sách trong kho", "BÁO CÁO SÁCH TRONG KHO", period,
                ["Đăng ký cá biệt", "Nhan đề", "Tác giả", "Phân loại", "Ngày nhận", "Đơn nhận", "Ngày đăng ký", "Kho", "Trạng thái"],
                rows.Select(r => (IReadOnlyList<object?>)[r.Barcode, r.Title, r.Author, r.Ddc, r.ReceivedOn, r.ReceiptCode, r.RegisteredOn, r.StoreName,
                    r.OnLoan ? "Đang mượn" : ItemStatus.Names.GetValueOrDefault(r.Status, r.Status)])),
            new ExcelSheet("Theo kho", "TỔNG HỢP THEO KHO", period, ["Kho", "Số đầu sách", "Số bản"],
                [.. stats.Select(s => (IReadOnlyList<object?>)[s.StoreName, s.Titles, s.Copies]),
                    ["Tổng cộng", await items.Select(i => i.BibId).Distinct().CountAsync(ct), await items.CountAsync(ct)]]));
    }

    private IQueryable<Item> BookReportItems(StoreBookReportRequest r)
    {
        HoldingsExcel.EnsureRange(r.From, r.To);
        return Filter(db.Set<Item>().AsNoTracking(), r.StoreId, r.From, r.To)
            .Where(i => i.Status != ItemStatus.Lost && i.Status != ItemStatus.Liquidated);
    }

    private IQueryable<StoreBookRow> BookRows(IQueryable<Item> items) =>
        from i in items
        join b in db.Set<BibSnapshot>() on i.BibPublicId equals b.BibPublicId into bj
        from b in bj.DefaultIfEmpty()
        join st in db.Set<Store>() on i.StoreId equals (long?)st.Id into sj
        from st in sj.DefaultIfEmpty()
        join rc in db.Set<ReceiptSnapshot>() on i.ReceiptPublicId equals (Guid?)rc.ReceiptPublicId into rj
        from rc in rj.DefaultIfEmpty()
        orderby i.CreatedAt descending, i.BarcodeKey
        select new StoreBookRow(i.Barcode, i.BibId, b == null ? null : b.Title, b == null ? null : b.Author, b == null ? null : b.Ddc,
            VietnamDate.Of(i.CreatedAt), st == null ? null : st.Name, i.Status, i.OnLoan, rc == null ? null : rc.ReceivedOn, rc == null ? null : rc.Code);

    private async Task<IReadOnlyList<StoreBookStat>> StoreStatsAsync(IQueryable<Item> items, CancellationToken ct)
    {
        var stores = await db.Set<Store>().AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var groups = await items.GroupBy(i => i.StoreId)
            .Select(g => new { g.Key, Titles = g.Select(i => i.BibId).Distinct().Count(), Copies = g.Count() }).ToListAsync(ct);
        return groups.Select(g => new StoreBookStat(g.Key, g.Key is { } id && stores.TryGetValue(id, out var name) ? name : "(Chưa chọn kho)", g.Titles, g.Copies))
            .OrderBy(s => s.StoreName, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>Lọc kho + khoảng ngày nhận (ngày nhận của phiếu nhận; bản không có phiếu nhận / phiếu chưa có ngày nhận → ngày đăng ký).</summary>
    private IQueryable<Item> Filter(IQueryable<Item> items, long? storeId, DateOnly? from, DateOnly? to)
    {
        if (storeId is { } store) items = items.Where(i => i.StoreId == store);
        var received = db.Set<ReceiptSnapshot>().Where(r => r.ReceivedOn != null);
        if (from is { } f)
        {
            var start = VietnamDate.Start(f);
            items = items.Where(i => received.Any(r => r.ReceiptPublicId == i.ReceiptPublicId && r.ReceivedOn >= f)
                || (!received.Any(r => r.ReceiptPublicId == i.ReceiptPublicId) && i.CreatedAt >= start));
        }
        if (to is { } t)
        {
            var end = VietnamDate.Start(t.AddDays(1));
            items = items.Where(i => received.Any(r => r.ReceiptPublicId == i.ReceiptPublicId && r.ReceivedOn <= t)
                || (!received.Any(r => r.ReceiptPublicId == i.ReceiptPublicId) && i.CreatedAt < end));
        }
        return items;
    }

    /// <summary>Chữ số đầu của chỉ số DDC ("895.9223" → "8"); không phải số → "" (chưa phân loại).</summary>
    public static string DdcKey(string? ddc)
    {
        var d = ddc?.TrimStart();
        return d is { Length: > 0 } && char.IsAsciiDigit(d[0]) ? d[..1] : "";
    }
}
