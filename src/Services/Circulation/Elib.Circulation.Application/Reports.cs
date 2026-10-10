using System.Globalization;
using System.Linq.Expressions;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Circulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>
/// Báo cáo lưu thông (monolith: CirculationReport, quyền CIRC_REPORT) — 11 loại như monolith. From/To: khoảng ngày (giờ VN) theo
/// nghĩa của từng loại (ngày mượn, ngày trả, hạn trả…); bỏ trống = không giới hạn (báo cáo 1 mặc định 30 ngày gần nhất).
/// </summary>
public sealed record CirculationReportRequest(
    int ReportType, DateOnly? From = null, DateOnly? To = null, long? CircPlaceId = null, long? ReaderTypeId = null, string? ClassName = null,
    int PageIndex = 1, int PageSize = 20);

public sealed record CirculationReport(
    int ReportType, string Title, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, int TotalCount,
    IReadOnlyList<string>? TotalRow, string? LibraryName, bool Truncated);

public sealed record CirculationReportFile(string FileName, byte[] Content);

public sealed class CirculationReports(ICrudDbContext db, ITenantContext tenant, TimeProvider clock)
{
    public const int RowCap = 5000;
    public const int TopN = 50;
    private const string D = "dd/MM/yyyy";

    public static readonly IReadOnlyDictionary<int, string> Titles = new Dictionary<int, string>
    {
        [1] = "HOẠT ĐỘNG PHỤC VỤ TẠI THƯ VIỆN",
        [2] = "DANH SÁCH TÀI LIỆU ĐANG MƯỢN",
        [3] = "TÀI LIỆU ĐANG MƯỢN THEO NGÀY TRẢ",
        [4] = "DANH SÁCH TÀI LIỆU ĐANG MƯỢN QUÁ HẠN",
        [5] = "DANH SÁCH TÀI LIỆU TRẢ QUÁ HẠN",
        [6] = "BẠN ĐỌC HẾT HẠN THẺ CHƯA TRẢ SÁCH",
        [7] = "BẠN ĐỌC QUÁ HẠN SÁCH",
        [8] = "THỐNG KÊ TÀI LIỆU MƯỢN NHIỀU",
        [9] = "THỐNG KÊ TÀI LIỆU KHÔNG ĐƯỢC MƯỢN",
        [10] = "DANH SÁCH TÀI LIỆU ĐÃ TRẢ",
        [11] = "DANH SÁCH TÀI LIỆU MẤT",
    };

    public async Task<CirculationReport> BuildAsync(CirculationReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (headers, rows, total) = await RowsAsync(request, ct);
        var size = Math.Clamp(request.PageSize, 1, 500);
        var page = rows.Skip((Math.Max(1, request.PageIndex) - 1) * size).Take(size).ToList();
        return new CirculationReport(request.ReportType, Titles[request.ReportType], headers, page, rows.Count, total, await LibraryNameAsync(ct),
            rows.Count >= RowCap);
    }

    /// <summary>File Excel: tên thư viện, tiêu đề, khoảng ngày, bảng có viền, dòng tổng (monolith: Export có letterhead).</summary>
    public async Task<CirculationReportFile> ExportAsync(CirculationReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (headers, rows, total) = await RowsAsync(request, ct);
        var title = Titles[request.ReportType];
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Báo cáo lưu thông");
        var width = Math.Max(headers.Count, 1);
        sheet.Cell(1, 1).Value = (await LibraryNameAsync(ct) ?? "").ToUpperInvariant();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Value = title;
        sheet.Range(3, 1, 3, width).Merge().Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        var range = Period(request);
        if (range is not null)
            sheet.Range(4, 1, 4, width).Merge().SetValue(range).Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        const int head = 6;
        for (var c = 0; c < headers.Count; c++) sheet.Cell(head, c + 1).Value = headers[c];
        sheet.Range(head, 1, head, width).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF9"));
        var r = head;
        foreach (var row in total is null ? rows : [.. rows, total])
        {
            r++;
            for (var c = 0; c < row.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                if (long.TryParse(row[c], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && row[c].Length < 15) cell.Value = n;
                else cell.Value = row[c];
            }
        }
        if (total is not null) sheet.Range(r, 1, r, width).Style.Font.SetBold();
        var table = sheet.Range(head, 1, r, width).Style.Border;
        table.SetOutsideBorder(XLBorderStyleValues.Thin);
        table.SetInsideBorder(XLBorderStyleValues.Thin);
        sheet.Columns(1, width).AdjustToContents(head, Math.Min(r, head + 500), 6, 60);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var stamp = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return new CirculationReportFile($"bao-cao-luu-thong-{request.ReportType}-{stamp}.xlsx", stream.ToArray());
    }

    private async Task<(IReadOnlyList<string> Headers, List<IReadOnlyList<string>> Rows, IReadOnlyList<string>? Total)> RowsAsync(
        CirculationReportRequest r, CancellationToken ct)
    {
        if (!Titles.ContainsKey(r.ReportType)) throw new BusinessRuleException("REPORT_TYPE_INVALID", "Loại báo cáo không hợp lệ.");
        if (r.From is { } f && r.To is { } t && f > t) throw new BusinessRuleException("DATE_RANGE_INVALID", "Từ ngày phải trước đến ngày.");
        return r.ReportType switch
        {
            1 => await DailyActivityAsync(r, ct),
            2 => await OpenLoansAsync(r, byDue: false, overdueOnly: false, ct),
            3 => await OpenLoansAsync(r, byDue: true, overdueOnly: false, ct),
            4 => await OpenLoansAsync(r, byDue: false, overdueOnly: true, ct),
            5 => await ReturnedAsync(r, lateOnly: true, ct),
            6 => await ExpiredCardsAsync(r, ct),
            7 => await OverdueReadersAsync(r, ct),
            8 => await MostBorrowedAsync(r, ct),
            9 => await NeverBorrowedAsync(r, ct),
            10 => await ReturnedAsync(r, lateOnly: false, ct),
            _ => await LostAsync(r, ct),
        };
    }

    // 1. Hoạt động phục vụ theo ngày: lượt mượn, trả, gia hạn, số bạn đọc được phục vụ.
    private async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>, IReadOnlyList<string>?)> DailyActivityAsync(CirculationReportRequest r, CancellationToken ct)
    {
        var today = Today;
        var to = r.To ?? today;
        var from = r.From ?? to.AddDays(-29);
        if (to.DayNumber - from.DayNumber > 366) throw new BusinessRuleException("DATE_RANGE_INVALID", "Báo cáo hoạt động theo ngày tối đa 1 năm.");
        var (start, end) = (Start(from), Start(to.AddDays(1)));
        var loans = Loans(r);
        var borrowed = await loans.Where(l => l.LoanedAt >= start && l.LoanedAt < end).Select(l => new { At = l.LoanedAt, l.ReaderPublicId }).ToListAsync(ct);
        var returned = await loans.Where(l => l.ReturnedAt >= start && l.ReturnedAt < end && l.ClosedItemStatus == null)
            .Select(l => new { At = l.ReturnedAt!.Value, l.ReaderPublicId }).ToListAsync(ct);
        var renewals = db.Set<LoanRenewal>().Where(x => x.RenewedAt >= start && x.RenewedAt < end);
        if (r.CircPlaceId is { } place) renewals = renewals.Where(x => x.CircPlaceId == place);
        if (ReaderFilter(r) is { } readers) renewals = renewals.Where(x => readers.Contains(x.ReaderPublicId));
        var renewed = await renewals.Select(x => new { At = x.RenewedAt, x.ReaderPublicId }).ToListAsync(ct);

        var rows = new List<IReadOnlyList<string>>();
        long sumB = 0, sumR = 0, sumN = 0, sumP = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var b = borrowed.Where(x => Loan.LocalDate(x.At) == day).ToList();
            var ret = returned.Where(x => Loan.LocalDate(x.At) == day).ToList();
            var n = renewed.Where(x => Loan.LocalDate(x.At) == day).ToList();
            var people = b.Select(x => x.ReaderPublicId).Concat(ret.Select(x => x.ReaderPublicId)).Concat(n.Select(x => x.ReaderPublicId)).Distinct().Count();
            rows.Add([day.ToString(D, CultureInfo.InvariantCulture), Num(b.Count), Num(ret.Count), Num(n.Count), Num(people)]);
            (sumB, sumR, sumN, sumP) = (sumB + b.Count, sumR + ret.Count, sumN + n.Count, sumP + people);
        }
        return (["Ngày", "Số lượt mượn", "Số lượt trả", "Số lượt gia hạn", "Số bạn đọc phục vụ"], rows,
            rows.Count == 0 ? null : ["Tổng cộng", Num(sumB), Num(sumR), Num(sumN), Num(sumP)]);
    }

    // 2–4. Đang mượn (theo ngày mượn / theo hạn trả / quá hạn).
    private async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>, IReadOnlyList<string>?)> OpenLoansAsync(
        CirculationReportRequest r, bool byDue, bool overdueOnly, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = Loans(r).Where(l => l.ReturnedAt == null);
        if (overdueOnly) query = query.Where(l => l.DueAt < now);
        if (byDue) query = InRange(query, r, l => l.DueAt);
        else query = InRange(query, r, l => l.LoanedAt);
        var list = await (byDue ? query.OrderBy(l => l.DueAt).ThenBy(l => l.Id) : query.OrderByDescending(l => l.LoanedAt).ThenByDescending(l => l.Id))
            .Take(RowCap).ToListAsync(ct);
        var names = await NamesAsync(list, ct);
        var places = await PlaceNamesAsync(ct);
        var rows = list.Select((l, i) => (IReadOnlyList<string>)
        [
            Num(i + 1), l.Barcode, names.Title(l.Mfn), names.Reader(l.ReaderPublicId, l.CardNo), Day(l.LoanedAt), Day(l.DueAt),
            l.CircPlaceId is { } p ? places.GetValueOrDefault(p, "") : "", .. overdueOnly ? [Num(l.OverdueDays(now))] : Array.Empty<string>(),
        ]).ToList();
        string[] headers = ["STT", "Đăng ký cá biệt", "Nhan đề", "Người mượn", "Ngày mượn", "Hạn trả", "Điểm lưu thông"];
        return (overdueOnly ? [.. headers, "Số ngày quá hạn"] : headers, rows, null);
    }

    // 5, 10. Đã trả (theo ngày trả) / trả quá hạn.
    private async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>, IReadOnlyList<string>?)> ReturnedAsync(
        CirculationReportRequest r, bool lateOnly, CancellationToken ct)
    {
        var query = InRange(Loans(r).Where(l => l.ReturnedAt != null && l.ClosedItemStatus == null), r, l => l.ReturnedAt!.Value);
        var list = await query.OrderByDescending(l => l.ReturnedAt).ThenByDescending(l => l.Id).Take(lateOnly ? RowCap * 4 : RowCap).ToListAsync(ct);
        if (lateOnly) list = [.. list.Where(l => l.OverdueDays(l.ReturnedAt!.Value) > 0).Take(RowCap)];
        var names = await NamesAsync(list, ct);
        var rows = list.Select((l, i) => (IReadOnlyList<string>)
        [
            Num(i + 1), l.Barcode, names.Title(l.Mfn), names.Reader(l.ReaderPublicId, l.CardNo), Day(l.LoanedAt),
            .. lateOnly ? [Day(l.DueAt), Day(l.ReturnedAt!.Value), Num(l.OverdueDays(l.ReturnedAt.Value))] : new[] { Day(l.ReturnedAt!.Value) },
        ]).ToList();
        return (lateOnly
            ? ["STT", "Đăng ký cá biệt", "Nhan đề", "Người mượn", "Ngày mượn", "Hạn trả", "Ngày trả", "Số ngày trễ"]
            : ["STT", "Đăng ký cá biệt", "Nhan đề", "Người trả", "Ngày mượn", "Ngày trả"], rows, null);
    }

    // 6. Bạn đọc hết hạn thẻ còn giữ sách.
    private async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>, IReadOnlyList<string>?)> ExpiredCardsAsync(CirculationReportRequest r, CancellationToken ct)
    {
        var today = Today;
        var counts = await Loans(r).Where(l => l.ReturnedAt == null).GroupBy(l => l.ReaderPublicId)
            .Select(g => new { Reader = g.Key, Count = g.Count() }).ToListAsync(ct);
        var ids = counts.Select(c => c.Reader).ToList();
        var readers = await db.Set<PatronReplica>().AsNoTracking()
            .Where(p => ids.Contains(p.ReaderPublicId) && p.ExpireDate != null && p.ExpireDate < today).ToListAsync(ct);
        var rows = readers.OrderBy(p => p.ExpireDate).ThenBy(p => p.CardNo).Take(RowCap).Select((p, i) => (IReadOnlyList<string>)
        [
            Num(i + 1), p.CardNo, p.FullName, p.ClassName ?? "", p.CourseName ?? "", p.ReaderTypeName ?? "", p.ExpireDate!.Value.ToString(D, CultureInfo.InvariantCulture),
            Num(counts.First(c => c.Reader == p.ReaderPublicId).Count),
        ]).ToList();
        return (["STT", "Mã bạn đọc", "Họ tên", "Lớp", "Khóa", "Loại bạn đọc", "Ngày hết hạn thẻ", "Số sách đang mượn"], rows, null);
    }

    // 7. Bạn đọc quá hạn: số tài liệu quá hạn, số ngày quá hạn nhiều nhất.
    private async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>, IReadOnlyList<string>?)> OverdueReadersAsync(CirculationReportRequest r, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var overdue = await Loans(r).Where(l => l.ReturnedAt == null && l.DueAt < now).Select(l => new { l.ReaderPublicId, l.CardNo, l.DueAt }).ToListAsync(ct);
        var grouped = overdue.GroupBy(l => (l.ReaderPublicId, l.CardNo))
            .Select(g => new { g.Key.ReaderPublicId, g.Key.CardNo, Count = g.Count(), MaxDays = g.Max(x => Today.DayNumber - Loan.LocalDate(x.DueAt).DayNumber) })
            .OrderByDescending(x => x.Count).ThenByDescending(x => x.MaxDays).Take(RowCap).ToList();
        var ids = grouped.Select(g => g.ReaderPublicId).ToList();
        var readers = await db.Set<PatronReplica>().AsNoTracking().Where(p => ids.Contains(p.ReaderPublicId)).ToDictionaryAsync(p => p.ReaderPublicId, ct);
        var rows = grouped.Select((g, i) =>
        {
            var p = readers.GetValueOrDefault(g.ReaderPublicId);
            return (IReadOnlyList<string>)[Num(i + 1), g.CardNo, p?.FullName ?? "", p?.ClassName ?? "", p?.CourseName ?? "", p?.ReaderTypeName ?? "", Num(g.Count), Num(g.MaxDays)];
        }).ToList();
        return (["STT", "Mã bạn đọc", "Họ tên", "Lớp", "Khóa", "Loại bạn đọc", "Số tài liệu quá hạn", "Số ngày quá hạn nhiều nhất"], rows, null);
    }

    // 8. Tài liệu mượn nhiều (theo biểu ghi, 50 biểu ghi đầu).
    private async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>, IReadOnlyList<string>?)> MostBorrowedAsync(CirculationReportRequest r, CancellationToken ct)
    {
        var top = await InRange(Loans(r), r, l => l.LoanedAt).GroupBy(l => l.Mfn).Select(g => new { Mfn = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Mfn).Take(TopN).ToListAsync(ct);
        var mfns = top.Select(t => t.Mfn).ToList();
        var bibs = await db.Set<BibSnapshot>().AsNoTracking().Where(b => mfns.Contains(b.Mfn)).ToDictionaryAsync(b => b.Mfn, ct);
        var rows = top.Select((t, i) => (IReadOnlyList<string>)
            [Num(i + 1), bibs.GetValueOrDefault(t.Mfn)?.Title ?? $"MFN {t.Mfn}", bibs.GetValueOrDefault(t.Mfn)?.Author ?? "", Num(t.Count)]).ToList();
        return (["STT", "Nhan đề", "Tác giả", "Số lượt mượn"], rows, null);
    }

    // 9. Bản sách chưa từng được mượn (trong khoảng ngày nếu chọn).
    private async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>, IReadOnlyList<string>?)> NeverBorrowedAsync(CirculationReportRequest r, CancellationToken ct)
    {
        var borrowed = InRange(db.Set<Loan>().AsQueryable(), r, l => l.LoanedAt).Select(l => l.ItemPublicId);
        var items = await db.Set<ItemReplica>().AsNoTracking().Where(i => !i.Deleted && !borrowed.Contains(i.ItemPublicId))
            .OrderBy(i => i.BarcodeKey).Take(RowCap).ToListAsync(ct);
        var mfns = items.Select(i => i.Mfn).Distinct().ToList();
        var bibs = await db.Set<BibSnapshot>().AsNoTracking().Where(b => mfns.Contains(b.Mfn)).ToDictionaryAsync(b => b.Mfn, ct);
        var rows = items.Select((it, i) => (IReadOnlyList<string>)
            [Num(i + 1), it.Barcode, bibs.GetValueOrDefault(it.Mfn)?.Title ?? $"MFN {it.Mfn}", bibs.GetValueOrDefault(it.Mfn)?.Author ?? "", it.StoreName ?? ""]).ToList();
        return (["STT", "Đăng ký cá biệt", "Nhan đề", "Tác giả", "Kho"], rows, null);
    }

    // 11. Tài liệu mất — lượt mượn đóng vì mất tài liệu (báo mất qua phiếu phạt).
    private async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>, IReadOnlyList<string>?)> LostAsync(CirculationReportRequest r, CancellationToken ct)
    {
        var list = await InRange(Loans(r).Where(l => l.ClosedItemStatus == Loan.LostStatus), r, l => l.ReturnedAt!.Value)
            .OrderByDescending(l => l.ReturnedAt).Take(RowCap).ToListAsync(ct);
        var names = await NamesAsync(list, ct);
        var itemIds = list.Select(l => l.ItemPublicId).ToList();
        var stores = await db.Set<ItemReplica>().AsNoTracking().Where(i => itemIds.Contains(i.ItemPublicId)).ToDictionaryAsync(i => i.ItemPublicId, i => i.StoreName, ct);
        var loanIds = list.Select(l => (Guid?)l.PublicId).ToList();
        var tickets = await db.Set<FineLine>().AsNoTracking().Where(f => loanIds.Contains(f.LoanPublicId))
            .Join(db.Set<FineTicket>(), f => f.FineTicketId, t => t.Id, (f, t) => new { f.LoanPublicId, t.Number }).ToListAsync(ct);
        var rows = list.Select((l, i) => (IReadOnlyList<string>)
        [
            Num(i + 1), l.Barcode, names.Title(l.Mfn), stores.GetValueOrDefault(l.ItemPublicId) ?? "", names.Reader(l.ReaderPublicId, l.CardNo),
            Day(l.ReturnedAt!.Value), tickets.FirstOrDefault(t => t.LoanPublicId == l.PublicId) is { } t ? $"Phiếu phạt {FineTicket.FormatCode(t.Number)}" : "",
        ]).ToList();
        return (["STT", "Đăng ký cá biệt", "Nhan đề", "Kho", "Người mượn", "Ngày mất", "Ghi chú"], rows, null);
    }

    private IQueryable<Loan> Loans(CirculationReportRequest r)
    {
        var query = db.Set<Loan>().AsNoTracking();
        if (r.CircPlaceId is { } place) query = query.Where(l => l.CircPlaceId == place);
        if (ReaderFilter(r) is { } readers) query = query.Where(l => readers.Contains(l.ReaderPublicId));
        return query;
    }

    private IQueryable<Guid>? ReaderFilter(CirculationReportRequest r)
    {
        var className = r.ClassName?.Trim();
        if (r.ReaderTypeId is null && string.IsNullOrEmpty(className)) return null;
        var readers = db.Set<PatronReplica>().AsQueryable();
        if (r.ReaderTypeId is { } type) readers = readers.Where(p => p.ReaderTypeId == type);
        if (!string.IsNullOrEmpty(className)) readers = readers.Where(p => p.ClassName == className);
        return readers.Select(p => p.ReaderPublicId);
    }

    private static IQueryable<Loan> InRange(IQueryable<Loan> query, CirculationReportRequest r, Expression<Func<Loan, DateTimeOffset>> at)
    {
        if (r.From is { } from)
        {
            var start = Start(from);
            query = query.Where(Compose(at, v => v >= start));
        }
        if (r.To is { } to)
        {
            var end = Start(to.AddDays(1));
            query = query.Where(Compose(at, v => v < end));
        }
        return query;
    }

    private static Expression<Func<Loan, bool>> Compose(
        Expression<Func<Loan, DateTimeOffset>> selector, Expression<Func<DateTimeOffset, bool>> test)
    {
        var body = new ReplaceParameter(test.Parameters[0], selector.Body).Visit(test.Body);
        return Expression.Lambda<Func<Loan, bool>>(body, selector.Parameters);
    }

    private async Task<Names> NamesAsync(IReadOnlyCollection<Loan> loans, CancellationToken ct)
    {
        var readerIds = loans.Select(l => l.ReaderPublicId).Distinct().ToList();
        var mfns = loans.Select(l => l.Mfn).Distinct().ToList();
        return new Names(
            await db.Set<PatronReplica>().AsNoTracking().Where(p => readerIds.Contains(p.ReaderPublicId)).ToDictionaryAsync(p => p.ReaderPublicId, p => p.FullName, ct),
            await db.Set<BibSnapshot>().AsNoTracking().Where(b => mfns.Contains(b.Mfn)).ToDictionaryAsync(b => b.Mfn, b => b.Title, ct));
    }

    private Task<Dictionary<long, string>> PlaceNamesAsync(CancellationToken ct) =>
        db.Set<CircPlace>().AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);

    private async Task<string?> LibraryNameAsync(CancellationToken ct)
    {
        var id = tenant.RequireTenantId();
        return await db.Set<TenantReplicaRecord>().AsNoTracking().Where(t => t.TenantId == id).Select(t => t.Name).FirstOrDefaultAsync(ct);
    }

    private static string? Period(CirculationReportRequest r) => (r.From, r.To) switch
    {
        ({ } f, { } t) => $"Từ ngày {f.ToString(D, CultureInfo.InvariantCulture)} đến ngày {t.ToString(D, CultureInfo.InvariantCulture)}",
        ({ } f, null) => $"Từ ngày {f.ToString(D, CultureInfo.InvariantCulture)}",
        (null, { } t) => $"Đến ngày {t.ToString(D, CultureInfo.InvariantCulture)}",
        _ => null,
    };

    private DateOnly Today => Loan.LocalDate(clock.GetUtcNow());

    private static DateTimeOffset Start(DateOnly date) => Loan.VietnamStart(date);

    private static string Day(DateTimeOffset at) => Loan.LocalDate(at).ToString(D, CultureInfo.InvariantCulture);

    private static string Num(long n) => n.ToString(CultureInfo.InvariantCulture);

    private sealed record Names(Dictionary<Guid, string> Readers, Dictionary<long, string> Titles)
    {
        public string Reader(Guid id, string cardNo) => Readers.TryGetValue(id, out var name) ? $"{name} ({cardNo})" : cardNo;

        public string Title(long mfn) => Titles.TryGetValue(mfn, out var title) ? title : $"MFN {mfn}";
    }

    private sealed class ReplaceParameter(ParameterExpression from, Expression to)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
