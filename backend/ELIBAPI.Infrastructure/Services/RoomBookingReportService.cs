using ClosedXML.Excel;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Báo cáo đặt phòng cho thủ thư (yêu cầu kỹ thuật 1.2 "Báo cáo" / "Quản lý tài nguyên"): xuất danh sách lượt đặt theo bộ lọc ra Excel,
/// và báo cáo tổng hợp theo khoảng ngày — trạng thái, tỷ lệ vắng mặt/huỷ, tỷ lệ sử dụng theo phòng (giờ đã dùng / giờ mở cửa), giờ
/// cao điểm, thứ trong tuần, bạn đọc dùng nhiều nhất. Ngày/giờ tính theo giờ thư viện.
/// Port ELIB-LRC 10-04. Tenant (K12): xuất Excel đi qua IRoomBookingRepository.ExportRowsAsync (lọc như màn danh sách); báo cáo tổng hợp
/// nhận <see cref="TenantScope"/> — phòng, lượt đặt lọc theo phạm vi, giờ mở cửa tính theo đơn vị của từng phòng.
/// </summary>
public class RoomBookingReportService(ELIBAPIDbContext db, IRoomBookingRepository bookings)
{
    public const int MaxExportRows = 20000;
    public const int MaxReportDays = 366;

    private static readonly Dictionary<int, string> StatusNames = new()
    {
        [1] = "Chờ duyệt", [2] = "Đã duyệt", [3] = "Đang sử dụng", [4] = "Hoàn tất", [5] = "Đã huỷ", [6] = "Bị từ chối", [7] = "Vắng mặt",
    };

    public async Task<(byte[] File, int Total, int Exported)> ExportAsync(RoomBookingSearchRequest request)
    {
        var (rows, total) = await bookings.ExportRowsAsync(request, MaxExportRows);
        var readerIds = rows.Select(r => r.ReaderId).Distinct().ToList();
        var types = await (from r in db.Readers.AsNoTracking()
                           where readerIds.Contains(r.Id)
                           join t in db.ReaderTypes.AsNoTracking() on r.ReaderTypeId equals t.Id into tj
                           from t in tj.DefaultIfEmpty()
                           select new { r.Id, Type = t != null ? t.Name : null }).ToDictionaryAsync(x => x.Id, x => x.Type);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Lượt đặt phòng");
        string[] headers = ["STT", "Phòng", "Bạn đọc", "Số thẻ", "Loại bạn đọc", "Ngày", "Từ", "Đến", "Số người", "Thành viên",
            "Trạng thái", "Check-in", "Trả phòng", "Ghi chú", "Đơn vị"];
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var row = 2;
        foreach (var b in rows)
        {
            var start = RoomBookingHours.ToLibraryLocal(b.StartAt);
            ws.Cell(row, 1).Value = row - 1;
            ws.Cell(row, 2).Value = b.RoomName ?? "";
            ws.Cell(row, 3).Value = b.ReaderName ?? "";
            ws.Cell(row, 4).Value = b.ReaderCardNo ?? "";
            ws.Cell(row, 5).Value = types.GetValueOrDefault(b.ReaderId) ?? "";
            ws.Cell(row, 6).Value = start.ToString("dd/MM/yyyy");
            ws.Cell(row, 7).Value = start.ToString("HH:mm");
            ws.Cell(row, 8).Value = RoomBookingHours.ToLibraryLocal(b.EndAt).ToString("HH:mm");
            ws.Cell(row, 9).Value = b.PartySize;
            ws.Cell(row, 10).Value = string.Join("; ", b.Members ?? []);
            ws.Cell(row, 11).Value = StatusNames.GetValueOrDefault(b.Status, b.Status.ToString());
            ws.Cell(row, 12).Value = b.CheckedInAt is { } ci ? RoomBookingHours.ToLibraryLocal(ci).ToString("dd/MM/yyyy HH:mm") : "";
            ws.Cell(row, 13).Value = b.CheckedOutAt is { } co ? RoomBookingHours.ToLibraryLocal(co).ToString("dd/MM/yyyy HH:mm") : "";
            ws.Cell(row, 14).Value = b.Note ?? "";
            ws.Cell(row, 15).Value = b.TenantName ?? "";
            row++;
        }
        Style(ws, headers.Length);
        if (total > rows.Count)
            ws.Cell(row + 1, 1).Value = $"Chỉ xuất {rows.Count:N0}/{total:N0} dòng đầu — vui lòng thu hẹp bộ lọc.";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), total, rows.Count);
    }

    private static void Style(IXLWorksheet ws, int columns)
    {
        var header = ws.Range(1, 1, 1, columns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
        ws.SheetView.FreezeRows(1);
        ws.Columns(1, columns).AdjustToContents(1, Math.Min(ws.LastRowUsed()?.RowNumber() ?? 1, 500));
        foreach (var c in ws.Columns(1, columns)) if (c.Width > 60) c.Width = 60;
    }

    // ── Báo cáo tổng hợp ─────────────────────────────────────────────────

    public async Task<ServiceResult<RoomBookingReport>> ReportAsync(RoomBookingReportRequest r, TenantScope scope)
    {
        if (r.From == null || r.To == null) return ServiceResult<RoomBookingReport>.BadRequest("Vui lòng chọn khoảng ngày.");
        var from = r.From.Value.Date;
        var to = r.To.Value.Date;
        if (to < from) return ServiceResult<RoomBookingReport>.BadRequest("Ngày kết thúc phải sau ngày bắt đầu.");
        if ((to - from).TotalDays + 1 > MaxReportDays) return ServiceResult<RoomBookingReport>.BadRequest($"Khoảng báo cáo tối đa {MaxReportDays} ngày.");

        var (startUtc, _) = RoomBookingHours.LibraryDayUtcRange(from);
        var (_, endUtc) = RoomBookingHours.LibraryDayUtcRange(to);

        var rooms = await (from c in db.RoomBookingConfigs.AsNoTracking()
                           where c.IsDelete != 2 && (scope.All || c.TenantId == scope.TenantId || (scope.IncludeShared && c.TenantId == null))
                           join o in db.MapObjects.AsNoTracking() on c.MapObjectId equals o.Id
                           where o.IsDelete != 2 && (r.MapObjectId == null || o.Id == r.MapObjectId) && (r.Category == null || o.Category == r.Category)
                           select new { o.Id, o.Name, o.Category, c.TenantId }).ToListAsync();
        rooms = rooms.DistinctBy(x => x.Id).OrderBy(x => x.Name).ToList();
        var roomIds = rooms.Select(x => x.Id).ToList();

        var list = await db.RoomBookings.AsNoTracking()
            .Where(b => b.IsDelete != 2 && roomIds.Contains(b.MapObjectId) && b.StartAt >= startUtc && b.StartAt < endUtc
                && (scope.All || b.TenantId == scope.TenantId || (scope.IncludeShared && b.TenantId == null)))
            .Select(b => new { b.MapObjectId, b.ReaderId, b.StartAt, b.EndAt, b.Status, b.CheckedInAt, b.CheckedOutAt, b.PartySize })
            .ToListAsync();

        static double UsedMinutes(DateTime start, DateTime end, DateTime? inAt, DateTime? outAt)
        {
            var s = inAt ?? start;
            var e = outAt ?? end;
            if (s < start) s = start;
            if (e > end) e = end;
            return Math.Max(0, (e - s).TotalMinutes);
        }

        // Giờ mở cửa theo từng ngày + loại cơ sở (ngày đặc biệt, giờ theo thứ, giờ chung).
        var openByRoom = roomIds.ToDictionary(id => id, _ => 0.0);
        var resolvers = new Dictionary<long, Func<DateTime, int?, RoomDayHours>>();
        foreach (var tenant in rooms.Select(x => x.TenantId).Distinct())
            resolvers[tenant ?? 0] = await RoomBookingHours.RangeResolverAsync(db, tenant, from, to);
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            foreach (var room in rooms)
            {
                var h = resolvers[room.TenantId ?? 0](day, room.Category);
                if (!h.Closed) openByRoom[room.Id] += (h.Close - h.Open).TotalMinutes;
            }
        }

        var used = list.Where(b => b.Status is 3 or 4).ToList();
        var approvedLike = list.Count(b => b.Status is 2 or 3 or 4 or 7);
        var byRoom = rooms.Select(room =>
        {
            var mine = list.Where(b => b.MapObjectId == room.Id).ToList();
            var usedMin = mine.Where(b => b.Status is 3 or 4).Sum(b => UsedMinutes(b.StartAt, b.EndAt, b.CheckedInAt, b.CheckedOutAt));
            var open = openByRoom[room.Id];
            return new RoomUsageRow(room.Id, room.Name, mine.Count, mine.Count(b => b.Status is 3 or 4), mine.Count(b => b.Status == 7),
                mine.Count(b => b.Status == 5), Math.Round(usedMin / 60, 1), Math.Round(open / 60, 1), open > 0 ? Math.Round(usedMin * 100 / open, 1) : 0);
        }).ToList();

        var active = list.Where(b => b.Status is not (5 or 6)).ToList();
        var byHour = Enumerable.Range(0, 24).Select(h => active.Count(b => RoomBookingHours.ToLibraryLocal(b.StartAt).Hour == h)).ToArray();
        var byWeekday = Enumerable.Range(0, 7).Select(d => active.Count(b => (int)RoomBookingHours.ToLibraryLocal(b.StartAt).DayOfWeek == d)).ToArray();

        var topIds = list.GroupBy(b => b.ReaderId).OrderByDescending(g => g.Count()).Take(10).Select(g => g.Key).ToList();
        var people = await db.Readers.AsNoTracking().Where(x => topIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Cardno }).ToDictionaryAsync(x => x.Id);
        var topReaders = topIds.Select(id =>
        {
            var mine = list.Where(b => b.ReaderId == id).ToList();
            var p = people.GetValueOrDefault(id);
            return new TopReaderRow(id, RoomBookingPrivacy.FullName(p?.FirstName, p?.LastName), p?.Cardno, mine.Count,
                Math.Round(mine.Where(b => b.Status is 3 or 4).Sum(b => UsedMinutes(b.StartAt, b.EndAt, b.CheckedInAt, b.CheckedOutAt)) / 60, 1),
                mine.Count(b => b.Status == 7));
        }).ToList();

        var totalUsed = used.Sum(b => UsedMinutes(b.StartAt, b.EndAt, b.CheckedInAt, b.CheckedOutAt));
        var totalOpen = openByRoom.Values.Sum();
        return ServiceResult<RoomBookingReport>.Ok(new RoomBookingReport(
            from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"), list.Count,
            Enumerable.Range(1, 7).ToDictionary(s => s, s => list.Count(b => b.Status == s)),
            approvedLike > 0 ? Math.Round(list.Count(b => b.Status == 7) * 100.0 / approvedLike, 1) : 0,
            list.Count > 0 ? Math.Round(list.Count(b => b.Status == 5) * 100.0 / list.Count, 1) : 0,
            Math.Round(totalUsed / 60, 1), totalOpen > 0 ? Math.Round(totalUsed * 100 / totalOpen, 1) : 0,
            list.Where(b => b.Status is 3 or 4).Sum(b => b.PartySize),
            byRoom, byHour, byWeekday, topReaders));
    }

    public async Task<ServiceResult<byte[]>> ReportExcelAsync(RoomBookingReportRequest r, TenantScope scope)
    {
        var res = await ReportAsync(r, scope);
        if (!res.IsOk) return ServiceResult<byte[]>.BadRequest(res.Error!);
        var rep = res.Value!;
        using var wb = new XLWorkbook();
        var s = wb.Worksheets.Add("Tổng hợp");
        var rows = new List<(string, object)>
        {
            ("Từ ngày", rep.From), ("Đến ngày", rep.To), ("Tổng lượt đặt", rep.Total),
        };
        rows.AddRange(rep.ByStatus.Select(kv => (StatusNames[kv.Key], (object)kv.Value)));
        rows.AddRange([("Tỷ lệ vắng mặt (%)", rep.NoShowRate), ("Tỷ lệ huỷ (%)", rep.CancelRate), ("Giờ sử dụng", rep.UsedHours),
            ("Tỷ lệ sử dụng (%)", rep.Utilization), ("Lượt người sử dụng", rep.Visitors)]);
        for (var i = 0; i < rows.Count; i++) { s.Cell(i + 1, 1).Value = rows[i].Item1; s.Cell(i + 1, 2).Value = XLCellValue.FromObject(rows[i].Item2); }
        s.Column(1).Style.Font.Bold = true;
        s.Columns(1, 2).AdjustToContents();

        var ws = wb.Worksheets.Add("Theo phòng");
        string[] h = ["Phòng", "Lượt đặt", "Đã sử dụng", "Vắng mặt", "Huỷ", "Giờ sử dụng", "Giờ mở cửa", "Tỷ lệ sử dụng (%)"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        var y = 2;
        foreach (var x in rep.ByRoom)
        {
            object[] v = [x.RoomName ?? "", x.Bookings, x.Used, x.NoShows, x.Cancelled, x.UsedHours, x.OpenHours, x.Utilization];
            for (var i = 0; i < v.Length; i++) ws.Cell(y, i + 1).Value = XLCellValue.FromObject(v[i]);
            y++;
        }
        Style(ws, h.Length);

        var hw = wb.Worksheets.Add("Theo giờ");
        hw.Cell(1, 1).Value = "Giờ"; hw.Cell(1, 2).Value = "Lượt đặt";
        for (var i = 0; i < 24; i++) { hw.Cell(i + 2, 1).Value = $"{i:00}:00"; hw.Cell(i + 2, 2).Value = rep.ByHour[i]; }
        Style(hw, 2);

        var tw = wb.Worksheets.Add("Bạn đọc");
        string[] th = ["Bạn đọc", "Số thẻ", "Lượt đặt", "Giờ sử dụng", "Vắng mặt"];
        for (var i = 0; i < th.Length; i++) tw.Cell(1, i + 1).Value = th[i];
        y = 2;
        foreach (var x in rep.TopReaders)
        {
            object[] v = [x.Name, x.CardNo ?? "", x.Bookings, x.UsedHours, x.NoShows];
            for (var i = 0; i < v.Length; i++) tw.Cell(y, i + 1).Value = XLCellValue.FromObject(v[i]);
            y++;
        }
        Style(tw, th.Length);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ServiceResult<byte[]>.Ok(ms.ToArray());
    }
}

public sealed class RoomBookingReportRequest
{
    public DateTime? From        { get; set; }
    public DateTime? To          { get; set; }
    public long?     MapObjectId { get; set; }
    public int?      Category    { get; set; }
    /// Tài khoản đặc quyền chọn đơn vị (Guid); user thường bị ép theo đơn vị JWT.
    public Guid?     TenantId    { get; set; }
}

public sealed record RoomUsageRow(long MapObjectId, string? RoomName, int Bookings, int Used, int NoShows, int Cancelled, double UsedHours, double OpenHours, double Utilization);
public sealed record TopReaderRow(long ReaderId, string Name, string? CardNo, int Bookings, double UsedHours, int NoShows);
public sealed record RoomBookingReport(string From, string To, int Total, Dictionary<int, int> ByStatus, double NoShowRate, double CancelRate,
    double UsedHours, double Utilization, int Visitors, List<RoomUsageRow> ByRoom, int[] ByHour, int[] ByWeekday, List<TopReaderRow> TopReaders);
