using ClosedXML.Excel;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

// Logic dựng dữ liệu báo cáo lưu thông — tách ra khỏi CirculationReportController để dùng chung được từ
// cả action HTTP (Search/Export) lẫn ScheduledReportEmailJob (gửi báo cáo định kỳ qua email), tránh
// trùng lặp toàn bộ nghiệp vụ 11 loại báo cáo ở 2 nơi. Chỉ di chuyển cơ học + đổi tenantId từ biến đọc
// claim trong controller thành tham số truyền vào, giữ nguyên mọi điều kiện lọc TenantId đã có sẵn.
public class CirculationReportBuilder(ELIBAPIDbContext db)
{
    private const int RowCap = 5000;
    private const int TopN   = 50;
    private const string ReturnedStatus = PrintLoans.ReturnedStatus;

    public static string ReportTitle(int type) => type switch
    {
        1  => "HOẠT ĐỘNG PHỤC VỤ TẠI THƯ VIỆN",
        2  => "DANH SÁCH TÀI LIỆU ĐANG MƯỢN",
        3  => "TÀI LIỆU ĐANG MƯỢN THEO NGÀY TRẢ",
        4  => "DANH SÁCH TÀI LIỆU ĐANG MƯỢN QUÁ HẠN",
        5  => "DANH SÁCH TÀI LIỆU TRẢ QUÁ HẠN",
        6  => "BẠN ĐỌC HẾT HẠN THẺ CHƯA TRẢ SÁCH",
        7  => "BẠN ĐỌC QUÁ HẠN SÁCH",
        8  => "THỐNG KÊ TÀI LIỆU MƯỢN NHIỀU",
        9  => "THỐNG KÊ TÀI LIỆU KHÔNG ĐƯỢC MƯỢN",
        10 => "DANH SÁCH TÀI LIỆU ĐÃ TRẢ",
        11 => "DANH SÁCH TÀI LIỆU MẤT",
        _  => "BÁO CÁO LƯU THÔNG"
    };

    /// <summary>Dòng "Tổng cộng" cộng các cột số trên TOÀN BỘ dữ liệu (màn hình phân trang nên không cộng được ở frontend).
    /// Hiện có cho báo cáo 1 (hoạt động phục vụ theo ngày): cộng lượt mượn / trả / gia hạn / bạn đọc phục vụ của từng ngày.
    /// null = loại báo cáo không có dòng tổng.</summary>
    public static string[]? TotalRow(int type, IReadOnlyList<string[]> rows)
    {
        if (type != 1 || rows.Count == 0) return null;
        var width = rows[0].Length;
        var total = new string[width];
        total[0] = "Tổng cộng";
        for (var c = 1; c < width; c++)
            total[c] = rows.Sum(row => long.TryParse(row.ElementAtOrDefault(c), out var v) ? v : 0).ToString();
        return total;
    }

    /// Xuất file Excel đơn giản (không letterhead — dùng cho đính kèm email báo cáo định kỳ).
    /// Action HTTP Export vẫn tự dựng workbook riêng có letterhead qua ExcelReportHelper, không đổi.
    public async Task<byte[]> BuildExcelBytesAsync(CirculationReportRequest r, long? tenantId)
    {
        var (headers, rows) = await BuildReportAsync(r, tenantId);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(ReportTitle(r.ReportType));
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;
        var row = 2;
        foreach (var values in rows)
        {
            for (var i = 0; i < values.Length; i++) ws.Cell(row, i + 1).Value = values[i];
            row++;
        }
        if (TotalRow(r.ReportType, rows) is { } total)
        {
            for (var i = 0; i < total.Length; i++) ws.Cell(row, i + 1).Value = total[i];
            ws.Row(row).Style.Font.Bold = true;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<(string[] Headers, List<string[]> Rows)> BuildReportAsync(CirculationReportRequest r, long? tenantId)
    {
        var today = LibraryClock.Today;

        return r.ReportType switch
        {
            1  => await BuildDailyActivityAsync(r, tenantId),
            2  => await BuildBorrowedAsync(r, tenantId, today, orderByDue: false, overdueOnly: false),
            3  => await BuildBorrowedAsync(r, tenantId, today, orderByDue: true, overdueOnly: false),
            4  => await BuildBorrowedAsync(r, tenantId, today, orderByDue: false, overdueOnly: true),
            5  => await BuildReturnedLateAsync(r, tenantId),
            6  => await BuildExpiredCardHoldingBooksAsync(r, tenantId, today),
            7  => await BuildOverdueReadersAsync(r, tenantId, today),
            8  => await BuildMostBorrowedAsync(r, tenantId),
            9  => await BuildNeverBorrowedAsync(tenantId),
            10 => await BuildReturnedAsync(r, tenantId),
            11 => await BuildLostAsync(r, tenantId),
            _  => (Array.Empty<string>(), new List<string[]>())
        };
    }

    // ==================== 1. Hoạt động phục vụ tại thư viện (tổng hợp theo ngày) ====================
    private async Task<(string[], List<string[]>)> BuildDailyActivityAsync(CirculationReportRequest r, long? tenantId)
    {
        var headers = new[] { "Ngày", "Số lượt mượn", "Số lượt trả", "Số lượt gia hạn", "Số bạn đọc phục vụ" };
        var from = (r.DateFrom ?? LibraryClock.Today.AddDays(-29)).Date;
        var to   = (r.DateTo   ?? LibraryClock.Today).Date;
        var toEnd = to.AddDays(1);

        var readerIds = HasReaderFilter(r) ? FilterReaderIds(r) : null;

        // Lượt mượn = phiếu đang mượn + lượt đã trả (BookIn) theo ngày mượn — mỗi lượt đếm đúng 1 lần, kể cả dữ liệu
        // cũ (xoá BookOut khi trả) lẫn mới (giữ BookOut "R" + BookIn). Trước đây chỉ đếm BookOut còn tồn tại.
        var borrowQuery = FilterOpenLoans(r, tenantId).Where(bo => bo.BorrowDate >= from && bo.BorrowDate < toEnd)
            .Select(bo => new { bo.BorrowDate, bo.ReaderId })
            .Concat(FilterReturns(r, tenantId).Where(bi => bi.BorrowDate >= from && bi.BorrowDate < toEnd)
                .Select(bi => new { bi.BorrowDate, bi.ReaderId }));

        var returnQuery = db.BookIns.Where(bi => bi.IsDelete != 2 && bi.ReturnDate >= from && bi.ReturnDate < toEnd);
        if (tenantId.HasValue) returnQuery = returnQuery.Where(bi => bi.TenantId == tenantId);
        if (r.CircPlaceId.HasValue) returnQuery = returnQuery.Where(bi => bi.CircPlace == r.CircPlaceId);
        if (readerIds != null) returnQuery = returnQuery.Where(bi => bi.ReaderId.HasValue && readerIds.Contains(bi.ReaderId.Value));

        var renewQuery = db.CRenews.Where(cr => cr.IsDelete != 2 && cr.Renew_Date >= from && cr.Renew_Date < toEnd);
        if (tenantId.HasValue) renewQuery = renewQuery.Where(cr => cr.TenantId == tenantId);
        if (r.CircPlaceId.HasValue) renewQuery = renewQuery.Where(cr => cr.Circ_Place_Id == r.CircPlaceId);
        if (readerIds != null) renewQuery = renewQuery.Where(cr => cr.Reader_Id.HasValue && readerIds.Contains(cr.Reader_Id.Value));

        var borrowRows = (await borrowQuery.ToListAsync()).Select(bo => new { Date = bo.BorrowDate!.Value.Date, bo.ReaderId }).ToList();
        var returnRows = await returnQuery.Select(bi => new { Date = bi.ReturnDate!.Value.Date, bi.ReaderId }).ToListAsync();
        var renewRows  = await renewQuery.Select(cr => new { Date = cr.Renew_Date!.Value.Date, ReaderId = cr.Reader_Id }).ToListAsync();

        var rows = new List<string[]>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var readerSet = new HashSet<long>();
            var borrowCount = 0;
            foreach (var x in borrowRows) if (x.Date == d) { borrowCount++; if (x.ReaderId.HasValue) readerSet.Add(x.ReaderId.Value); }
            var returnCount = 0;
            foreach (var x in returnRows) if (x.Date == d) { returnCount++; if (x.ReaderId.HasValue) readerSet.Add(x.ReaderId.Value); }
            var renewCount = 0;
            foreach (var x in renewRows) if (x.Date == d) { renewCount++; if (x.ReaderId.HasValue) readerSet.Add(x.ReaderId.Value); }

            rows.Add([d.ToString("dd/MM/yyyy"), borrowCount.ToString(), returnCount.ToString(), renewCount.ToString(), readerSet.Count.ToString()]);
        }
        return (headers, rows);
    }

    // ==================== 2/3/4. Tài liệu đang mượn (mặc định / theo ngày trả / quá hạn) ====================
    private async Task<(string[], List<string[]>)> BuildBorrowedAsync(CirculationReportRequest r, long? tenantId, DateTime today, bool orderByDue, bool overdueOnly)
    {
        var headers = overdueOnly
            ? new[] { "STT", "Đăng ký cá biệt", "Nhan đề", "Người mượn", "Ngày mượn", "Hạn trả", "Điểm lưu thông", "Số ngày quá hạn" }
            : new[] { "STT", "Đăng ký cá biệt", "Nhan đề", "Người mượn", "Ngày mượn", "Hạn trả", "Điểm lưu thông" };

        // Đang mượn = Status khác "R" (xem FilterOpenLoans) — không lọc Status == "O" vì dữ liệu cũ lưu "1".
        var query = FilterOpenLoans(r, tenantId);
        if (overdueOnly) query = query.Where(bo => bo.DueDate.HasValue && bo.DueDate < today);
        if (r.DateFrom.HasValue) query = query.Where(bo => bo.BorrowDate >= r.DateFrom);
        if (r.DateTo.HasValue)   query = query.Where(bo => bo.BorrowDate <= r.DateTo);

        var joined =
            from bo in query
            join rd in db.Readers on bo.ReaderId equals rd.Id into rdj from rd in rdj.DefaultIfEmpty()
            join bc in db.Barcodes
                on new { K = bo.Barcode, T = bo.TenantId ?? 0 } equals new { K = bc.BarcodeValue, T = bc.TenantId ?? 0 } into bcj from bc in bcj.DefaultIfEmpty()
            join bx in db.BibXmls on bc.BibId equals bx.BibId into bxj from bx in bxj.DefaultIfEmpty()
            join cp in db.CircPlaces on bo.CircPlace equals cp.Id into cpj from cp in cpj.DefaultIfEmpty()
            select new
            {
                bo.Barcode, bo.BorrowDate, bo.DueDate,
                Title = bx != null ? bx.Title : null,
                ReaderName = rd != null ? rd.FirstName + " " + rd.LastName : null,
                CircPlaceName = cp != null ? cp.Name : null
            };

        var ordered = orderByDue ? joined.OrderBy(x => x.DueDate) : joined.OrderByDescending(x => x.BorrowDate);
        var list = await ordered.Take(RowCap).ToListAsync();

        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in list)
        {
            var values = new List<string>
            {
                (stt++).ToString(),
                x.Barcode ?? "",
                x.Title ?? "",
                (x.ReaderName ?? "").Trim(),
                x.BorrowDate?.ToString("dd/MM/yyyy") ?? "",
                x.DueDate?.ToString("dd/MM/yyyy") ?? "",
                x.CircPlaceName ?? ""
            };
            if (overdueOnly)
                values.Add(x.DueDate.HasValue ? (today - x.DueDate.Value.Date).Days.ToString() : "0");
            rows.Add(values.ToArray());
        }
        return (headers, rows);
    }

    // ==================== 5. Tài liệu trả quá hạn ====================
    private async Task<(string[], List<string[]>)> BuildReturnedLateAsync(CirculationReportRequest r, long? tenantId)
    {
        var headers = new[] { "STT", "Đăng ký cá biệt", "Nhan đề", "Người mượn", "Ngày mượn", "Hạn trả", "Ngày trả", "Số ngày trễ" };

        var query = db.BookIns.Where(bi => bi.IsDelete != 2 && bi.DueDate.HasValue && bi.ReturnDate.HasValue && bi.ReturnDate > bi.DueDate);
        if (tenantId.HasValue) query = query.Where(bi => bi.TenantId == tenantId);
        if (r.CircPlaceId.HasValue) query = query.Where(bi => bi.CircPlace == r.CircPlaceId);
        if (r.DateFrom.HasValue) query = query.Where(bi => bi.ReturnDate >= r.DateFrom);
        if (r.DateTo.HasValue)   query = query.Where(bi => bi.ReturnDate <= r.DateTo);
        if (HasReaderFilter(r))
        {
            var readerIds = FilterReaderIds(r);
            query = query.Where(bi => bi.ReaderId.HasValue && readerIds.Contains(bi.ReaderId.Value));
        }

        var joined =
            from bi in query
            join rd in db.Readers on bi.ReaderId equals rd.Id into rdj from rd in rdj.DefaultIfEmpty()
            join bc in db.Barcodes
                on new { K = bi.Barcode, T = bi.TenantId ?? 0 } equals new { K = bc.BarcodeValue, T = bc.TenantId ?? 0 } into bcj from bc in bcj.DefaultIfEmpty()
            join bx in db.BibXmls on bc.BibId equals bx.BibId into bxj from bx in bxj.DefaultIfEmpty()
            orderby bi.ReturnDate descending
            select new
            {
                bi.Barcode, bi.BorrowDate, bi.DueDate, bi.ReturnDate,
                Title = bx != null ? bx.Title : null,
                ReaderName = rd != null ? rd.FirstName + " " + rd.LastName : null
            };

        var list = await joined.Take(RowCap).ToListAsync();
        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in list)
        {
            var lateDays = (x.ReturnDate!.Value.Date - x.DueDate!.Value.Date).Days;
            rows.Add([
                (stt++).ToString(), x.Barcode ?? "", x.Title ?? "", (x.ReaderName ?? "").Trim(),
                x.BorrowDate?.ToString("dd/MM/yyyy") ?? "", x.DueDate?.ToString("dd/MM/yyyy") ?? "",
                x.ReturnDate?.ToString("dd/MM/yyyy") ?? "", lateDays.ToString()
            ]);
        }
        return (headers, rows);
    }

    // ==================== 6. Bạn đọc hết hạn thẻ chưa trả sách ====================
    private async Task<(string[], List<string[]>)> BuildExpiredCardHoldingBooksAsync(CirculationReportRequest r, long? tenantId, DateTime today)
    {
        var headers = new[] { "STT", "Mã bạn đọc", "Họ tên", "Lớp", "Khóa", "Đơn vị", "Ngày hết hạn thẻ", "Số sách đang mượn" };

        var readerQuery = db.Readers.Where(rd => rd.IsDelete != 2 && rd.ExpireDate.HasValue && rd.ExpireDate < today);
        if (tenantId.HasValue) readerQuery = readerQuery.Where(rd => rd.TenantId == tenantId);
        if (r.ClassId.HasValue)      readerQuery = readerQuery.Where(rd => rd.ClassId == r.ClassId);
        if (r.CourseId.HasValue)     readerQuery = readerQuery.Where(rd => rd.CourseId == r.CourseId);
        if (r.OrgId.HasValue)        readerQuery = readerQuery.Where(rd => rd.OrgId == r.OrgId);
        if (r.ReaderTypeId.HasValue) readerQuery = readerQuery.Where(rd => rd.ReaderTypeId == r.ReaderTypeId);

        readerQuery = readerQuery.Where(rd => OpenLoans.Any(bo => bo.ReaderId == rd.Id
            && (!r.CircPlaceId.HasValue || bo.CircPlace == r.CircPlaceId)));

        var joined =
            from rd in readerQuery
            join cls in db.Classes on rd.ClassId equals cls.Id into clsj from cls in clsj.DefaultIfEmpty()
            join crs in db.Courses on rd.CourseId equals crs.Id into crsj from crs in crsj.DefaultIfEmpty()
            join org in db.Orgs on rd.OrgId equals org.Id into orgj from org in orgj.DefaultIfEmpty()
            select new
            {
                rd.Id, rd.Cardno, rd.FirstName, rd.LastName, rd.ExpireDate,
                ClassName = cls != null ? cls.Name : null,
                CourseName = crs != null ? crs.Name : null,
                OrgName = org != null ? org.Name : null
            };

        var list = await joined.Take(RowCap).ToListAsync();
        var readerIds = list.Select(x => x.Id).ToList();
        var borrowCounts = await OpenLoans.Where(bo => bo.ReaderId.HasValue && readerIds.Contains(bo.ReaderId.Value)
                && (!r.CircPlaceId.HasValue || bo.CircPlace == r.CircPlaceId))
            .GroupBy(bo => bo.ReaderId!.Value)
            .Select(g => new { ReaderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ReaderId, x => x.Count);

        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in list)
        {
            borrowCounts.TryGetValue(x.Id, out var cnt);
            rows.Add([
                (stt++).ToString(), x.Cardno ?? "", $"{x.FirstName} {x.LastName}".Trim(),
                x.ClassName ?? "", x.CourseName ?? "", x.OrgName ?? "",
                x.ExpireDate?.ToString("dd/MM/yyyy") ?? "", cnt.ToString()
            ]);
        }
        return (headers, rows);
    }

    // ==================== 7. Bạn đọc quá hạn sách ====================
    private async Task<(string[], List<string[]>)> BuildOverdueReadersAsync(CirculationReportRequest r, long? tenantId, DateTime today)
    {
        var headers = new[] { "STT", "Mã bạn đọc", "Họ tên", "Lớp", "Khóa", "Đơn vị", "Số tài liệu quá hạn", "Số ngày quá hạn nhiều nhất" };

        var overdueQuery = FilterOpenLoans(r, tenantId).Where(bo => bo.DueDate.HasValue && bo.DueDate < today && bo.ReaderId.HasValue);

        var grouped = overdueQuery
            .GroupBy(bo => bo.ReaderId!.Value)
            .Select(g => new { ReaderId = g.Key, Count = g.Count(), MinDueDate = g.Min(x => x.DueDate) });

        var list = await grouped.Take(RowCap).ToListAsync();
        var readerIds = list.Select(x => x.ReaderId).ToList();
        var readerMap = await db.Readers.Where(rd => readerIds.Contains(rd.Id))
            .ToDictionaryAsync(rd => rd.Id, rd => new { rd.Cardno, rd.FirstName, rd.LastName, rd.ClassId, rd.CourseId, rd.OrgId });
        var classMap  = await db.Classes.ToDictionaryAsync(c => c.Id, c => c.Name);
        var courseMap = await db.Courses.ToDictionaryAsync(c => c.Id, c => c.Name);
        var orgMap    = await db.Orgs.ToDictionaryAsync(o => o.Id, o => o.Name);

        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in list.OrderByDescending(x => x.Count))
        {
            readerMap.TryGetValue(x.ReaderId, out var rd);
            var maxOverdueDays = x.MinDueDate.HasValue ? (today - x.MinDueDate.Value.Date).Days : 0;
            var className  = rd?.ClassId  != null && classMap.TryGetValue(rd.ClassId.Value, out var cn)   ? cn  : null;
            var courseName = rd?.CourseId != null && courseMap.TryGetValue(rd.CourseId.Value, out var con) ? con : null;
            var orgName    = rd?.OrgId    != null && orgMap.TryGetValue(rd.OrgId.Value, out var on)        ? on  : null;
            rows.Add([
                (stt++).ToString(), rd?.Cardno ?? "", rd != null ? $"{rd.FirstName} {rd.LastName}".Trim() : "",
                className ?? "", courseName ?? "", orgName ?? "", x.Count.ToString(), maxOverdueDays.ToString()
            ]);
        }
        return (headers, rows);
    }

    // ==================== 8. Thống kê tài liệu mượn nhiều ====================
    private async Task<(string[], List<string[]>)> BuildMostBorrowedAsync(CirculationReportRequest r, long? tenantId)
    {
        var headers = new[] { "STT", "Nhan đề", "Tác giả", "Số lượt mượn" };

        // Mọi lượt mượn (đang mượn + đã trả), không chỉ phiếu còn trong BookOut — dữ liệu cũ xoá BookOut khi trả nên
        // chỉ đếm BookOut sẽ bỏ sót gần hết lịch sử. Giữ TenantId để join ĐKCB theo (mã, đơn vị).
        var loans = FilterOpenLoans(r, tenantId).Select(bo => new { bo.Barcode, bo.BorrowDate, bo.TenantId })
            .Concat(FilterReturns(r, tenantId).Select(bi => new { bi.Barcode, bi.BorrowDate, bi.TenantId }));
        if (r.DateFrom.HasValue) loans = loans.Where(bo => bo.BorrowDate >= r.DateFrom);
        if (r.DateTo.HasValue)   loans = loans.Where(bo => bo.BorrowDate <= r.DateTo);

        var grouped =
            from bo in loans
            join bc in db.Barcodes
                on new { K = bo.Barcode, T = bo.TenantId ?? 0 } equals new { K = bc.BarcodeValue, T = bc.TenantId ?? 0 } into bcj from bc in bcj.DefaultIfEmpty()
            where bc != null && bc.BibId != null
            group bc by bc.BibId into g
            select new { BibId = g.Key, Count = g.Count() };

        var top = await grouped.OrderByDescending(x => x.Count).Take(TopN).ToListAsync();
        var bibIds = top.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).ToList();
        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);

        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in top)
        {
            xmlMap.TryGetValue(x.BibId ?? 0, out var bx);
            rows.Add([(stt++).ToString(), bx?.Title ?? "", bx?.Author ?? "", x.Count.ToString()]);
        }
        return (headers, rows);
    }

    // ==================== 9. Thống kê tài liệu không được mượn ====================
    private async Task<(string[], List<string[]>)> BuildNeverBorrowedAsync(long? tenantId)
    {
        var headers = new[] { "STT", "Đăng ký cá biệt", "Nhan đề", "Tác giả", "Kho" };

        var query = NeverBorrowedBarcodes();
        if (tenantId.HasValue) query = query.Where(bc => bc.TenantId == tenantId);

        var list = await query.OrderBy(bc => bc.BarcodeValue).Take(RowCap)
            .Select(bc => new { bc.BarcodeValue, bc.BibId, bc.Store }).ToListAsync();

        var bibIds = list.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var xmlMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);
        var storeIds = list.Where(x => x.Store.HasValue).Select(x => (long)x.Store!.Value).Distinct().ToList();
        var storeMap = await db.Stores.Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);

        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in list)
        {
            string? title = null, author = null;
            if (x.BibId.HasValue && xmlMap.TryGetValue(x.BibId.Value, out var bx)) { title = bx.Title; author = bx.Author; }
            string? storeName = x.Store.HasValue && storeMap.TryGetValue(x.Store.Value, out var sn) ? sn : null;
            rows.Add([(stt++).ToString(), x.BarcodeValue ?? "", title ?? "", author ?? "", storeName ?? ""]);
        }
        return (headers, rows);
    }

    // ==================== 10. Tài liệu đã trả ====================
    private async Task<(string[], List<string[]>)> BuildReturnedAsync(CirculationReportRequest r, long? tenantId)
    {
        var headers = new[] { "STT", "Đăng ký cá biệt", "Nhan đề", "Người trả", "Ngày mượn", "Ngày trả" };

        var query = db.BookIns.Where(bi => bi.IsDelete != 2);
        if (tenantId.HasValue) query = query.Where(bi => bi.TenantId == tenantId);
        if (r.CircPlaceId.HasValue) query = query.Where(bi => bi.CircPlace == r.CircPlaceId);
        if (r.DateFrom.HasValue) query = query.Where(bi => bi.ReturnDate >= r.DateFrom);
        if (r.DateTo.HasValue)   query = query.Where(bi => bi.ReturnDate <= r.DateTo);
        if (HasReaderFilter(r))
        {
            var readerIds = FilterReaderIds(r);
            query = query.Where(bi => bi.ReaderId.HasValue && readerIds.Contains(bi.ReaderId.Value));
        }

        var joined =
            from bi in query
            join rd in db.Readers on bi.ReaderId equals rd.Id into rdj from rd in rdj.DefaultIfEmpty()
            join bc in db.Barcodes
                on new { K = bi.Barcode, T = bi.TenantId ?? 0 } equals new { K = bc.BarcodeValue, T = bc.TenantId ?? 0 } into bcj from bc in bcj.DefaultIfEmpty()
            join bx in db.BibXmls on bc.BibId equals bx.BibId into bxj from bx in bxj.DefaultIfEmpty()
            orderby bi.ReturnDate descending
            select new
            {
                bi.Barcode, bi.BorrowDate, bi.ReturnDate,
                Title = bx != null ? bx.Title : null,
                ReaderName = rd != null ? rd.FirstName + " " + rd.LastName : null
            };

        var list = await joined.Take(RowCap).ToListAsync();
        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in list)
            rows.Add([
                (stt++).ToString(), x.Barcode ?? "", x.Title ?? "", (x.ReaderName ?? "").Trim(),
                x.BorrowDate?.ToString("dd/MM/yyyy") ?? "", x.ReturnDate?.ToString("dd/MM/yyyy") ?? ""
            ]);
        return (headers, rows);
    }

    // ==================== 11. Tài liệu mất ====================
    private async Task<(string[], List<string[]>)> BuildLostAsync(CirculationReportRequest r, long? tenantId)
    {
        var headers = new[] { "STT", "Đăng ký cá biệt", "Nhan đề", "Quản lý Kho", "Ngày mất", "Lý do mất" };

        var query = db.LostBooks.Where(x => x.IsDelete != 2);
        if (tenantId.HasValue) query = query.Where(x => x.TenantId == tenantId);
        if (r.DateFrom.HasValue) query = query.Where(x => x.Submited >= r.DateFrom);
        if (r.DateTo.HasValue)   query = query.Where(x => x.Submited <= r.DateTo);

        var items = await query.OrderByDescending(x => x.Submited).Take(RowCap).ToListAsync();

        var barcodes = items.Where(x => x.Barcode != null).Select(x => x.Barcode!).Distinct().ToList();
        // Khoá (mã, đơn vị): 2 đơn vị có thể cùng mã ĐKCB (Đợt 20) — ToDictionary theo riêng mã sẽ lỗi trùng khoá.
        var barcodeToBibId = (await db.Barcodes.Where(x => barcodes.Contains(x.BarcodeValue!))
                .Select(x => new { x.Id, x.BarcodeValue, x.TenantId, x.BibId }).ToListAsync())
            .GroupBy(x => (x.BarcodeValue!, x.TenantId ?? 0))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().BibId);
        var bibIds = barcodeToBibId.Values.Where(v => v.HasValue).Select(v => v!.Value).Distinct().ToList();
        var titleMap = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId, x => x.Title);
        var storeIds = items.Where(x => x.Store.HasValue).Select(x => x.Store!.Value).Distinct().ToList();
        var storeMap = await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in items)
        {
            string? title = x.Barcode != null && barcodeToBibId.TryGetValue((x.Barcode, x.TenantId ?? 0), out var bibId)
                && bibId.HasValue && titleMap.TryGetValue(bibId.Value, out var t) ? t : null;
            string? storeName = x.Store.HasValue && storeMap.TryGetValue(x.Store.Value, out var sn) ? sn : null;
            rows.Add([
                (stt++).ToString(), x.Barcode ?? "", title ?? "", storeName ?? "",
                x.Submited?.ToString("dd/MM/yyyy") ?? "", x.Reason ?? ""
            ]);
        }
        return (headers, rows);
    }

    // ==================== Helpers ====================
    private static bool HasReaderFilter(CirculationReportRequest r) =>
        r.ClassId.HasValue || r.CourseId.HasValue || r.OrgId.HasValue || r.ReaderTypeId.HasValue;

    private IQueryable<long> FilterReaderIds(CirculationReportRequest r) =>
        db.Readers.Where(rd => rd.IsDelete != 2
            && (!r.ClassId.HasValue || rd.ClassId == r.ClassId)
            && (!r.CourseId.HasValue || rd.CourseId == r.CourseId)
            && (!r.OrgId.HasValue || rd.OrgId == r.OrgId)
            && (!r.ReaderTypeId.HasValue || rd.ReaderTypeId == r.ReaderTypeId))
        .Select(rd => rd.Id);

    /// <summary>Lượt đã trả (mỗi lượt trả, kiểu cũ hay mới, có đúng 1 dòng BookIn) theo đơn vị/điểm lưu thông/bạn đọc.</summary>
    private IQueryable<BookIn> FilterReturns(CirculationReportRequest r, long? tenantId)
    {
        var query = db.BookIns.Where(bi => bi.IsDelete != 2);
        if (tenantId.HasValue) query = query.Where(bi => bi.TenantId == tenantId);
        if (r.CircPlaceId.HasValue) query = query.Where(bi => bi.CircPlace == r.CircPlaceId);
        if (HasReaderFilter(r))
        {
            var readerIds = FilterReaderIds(r);
            query = query.Where(bi => bi.ReaderId.HasValue && readerIds.Contains(bi.ReaderId.Value));
        }
        return query;
    }

    private IQueryable<BookOut> OpenLoans => db.BookOuts.Where(bo => bo.IsDelete != 2 && bo.Status != ReturnedStatus);

    /// <summary>ĐKCB chưa từng được mượn: không còn phiếu BookOut (đang mượn hoặc "R") và không có lượt trả BookIn (dữ liệu
    /// cũ xoá BookOut khi trả — trước đây chỉ xét BookOut nên sách đã mượn kiểu cũ bị coi là "chưa từng mượn").
    /// So khớp (mã, đơn vị) vì mã ĐKCB chỉ duy nhất trong 1 đơn vị. Kiểm tra <c>Barcode != null</c> đặt TRƯỚC phép so sánh
    /// để EF bỏ nhánh bù NULL — nhánh OR đó khiến Postgres không dùng được index/anti-join (dashboard quá 60 giây).</summary>
    private IQueryable<Barcode> NeverBorrowedBarcodes() =>
        db.Barcodes.Where(bc => bc.IsDelete != 2 && bc.BarcodeValue != null
            && !db.BookOuts.Any(bo => bo.Barcode != null && bo.Barcode == bc.BarcodeValue && bo.IsDelete != 2
                                      && (bo.TenantId ?? 0) == (bc.TenantId ?? 0))
            && !db.BookIns.Any(bi => bi.Barcode != null && bi.Barcode == bc.BarcodeValue && bi.IsDelete != 2
                                     && (bi.TenantId ?? 0) == (bc.TenantId ?? 0)));

    /// <summary>
    /// Phiếu mượn CHƯA TRẢ theo đơn vị/điểm lưu thông/bạn đọc. Quy ước dữ liệu: trả qua hệ thống hiện tại đặt
    /// BookOut.Status = "R" và GIỮ dòng (kèm 1 dòng BookIn); dữ liệu cũ thì xoá dòng BookOut khi trả; Status cũ "1"/"O"
    /// đều là đang mượn. Nên "đang mượn" = Status khác "R" (trước đây lọc Status == "O" → bỏ sót "1", và báo cáo 8/1
    /// đếm cả phiếu đã trả).
    /// </summary>
    private IQueryable<BookOut> FilterOpenLoans(CirculationReportRequest r, long? tenantId)
    {
        var query = OpenLoans;
        if (tenantId.HasValue) query = query.Where(bo => bo.TenantId == tenantId);
        if (r.CircPlaceId.HasValue) query = query.Where(bo => bo.CircPlace == r.CircPlaceId);
        if (HasReaderFilter(r))
        {
            var readerIds = FilterReaderIds(r);
            query = query.Where(bo => bo.ReaderId.HasValue && readerIds.Contains(bo.ReaderId.Value));
        }
        return query;
    }
}
