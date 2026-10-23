namespace Elib.BuildingBlocks.Crud;

/// <summary>Một cột của biểu đồ theo tháng (giờ Việt Nam). <see cref="Label"/> = "MM/yyyy".</summary>
public sealed record MonthPoint(int Year, int Month, string Label, int Value);

/// <summary>Chuỗi theo tháng cho biểu đồ bảng điều khiển: đủ mọi tháng trong cửa sổ (tháng không có số liệu = 0).</summary>
public sealed record MonthlySeries(IReadOnlyList<MonthPoint> Points, int Total);

/// <summary>Một cột theo ngày (giờ Việt Nam); <see cref="Label"/> = "dd/MM".</summary>
public sealed record DayPoint(DateOnly Date, string Label, int Value);

/// <summary>Số liệu có tên (cơ cấu, xếp hạng).</summary>
public sealed record NamedCount(string Name, int Count);

/// <summary>
/// Gom mốc thời gian cho bảng điều khiển. Gom ở bộ nhớ trên các mốc đã lọc theo cửa sổ (không GROUP BY theo phần ngày trong SQL: Npgsql và SQLite
/// của test dịch khác nhau); mọi ngày/giờ/tháng tính theo giờ Việt Nam (UTC+7).
/// </summary>
public static class DashboardSeries
{
    /// <summary>Số dòng tối đa nạp để gom — chặn một đơn vị cực lớn làm nghẽn bảng điều khiển.</summary>
    public const int RowCap = 200_000;

    public static DateTime Vietnam(DateTimeOffset at) => at.UtcDateTime.AddHours(7);

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(Vietnam(clock.GetUtcNow()));

    /// <summary>0 giờ ngày <paramref name="date"/> giờ Việt Nam, biểu diễn UTC (Npgsql chỉ nhận timestamptz offset 0).</summary>
    public static DateTimeOffset StartOf(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();

    /// <summary>Ngày đầu tháng của tháng cách hiện tại <paramref name="monthsBack"/> tháng (0 = tháng này).</summary>
    public static DateOnly MonthStart(TimeProvider clock, int monthsBack)
    {
        var today = Today(clock);
        return new DateOnly(today.Year, today.Month, 1).AddMonths(-monthsBack);
    }

    /// <summary>Chuỗi <paramref name="months"/> tháng gần nhất (gồm tháng này) từ các mốc thời gian.</summary>
    public static MonthlySeries Months(IEnumerable<DateTimeOffset> stamps, TimeProvider clock, int months = 12)
    {
        var first = MonthStart(clock, months - 1);
        var counts = new int[months];
        foreach (var at in stamps)
        {
            var v = Vietnam(at);
            var index = (v.Year - first.Year) * 12 + v.Month - first.Month;
            if (index >= 0 && index < months) counts[index]++;
        }
        return Months(counts, first);
    }

    /// <summary>Chuỗi theo tháng từ số đếm đã gom sẵn (<paramref name="counts"/>[0] là tháng <paramref name="first"/>).</summary>
    public static MonthlySeries Months(IReadOnlyList<int> counts, DateOnly first)
    {
        var points = counts.Select((n, i) =>
        {
            var m = first.AddMonths(i);
            return new MonthPoint(m.Year, m.Month, $"{m.Month:00}/{m.Year}", n);
        }).ToList();
        return new MonthlySeries(points, counts.Sum());
    }

    /// <summary>Chuỗi <paramref name="days"/> ngày gần nhất (gồm hôm nay).</summary>
    public static IReadOnlyList<DayPoint> Days(IEnumerable<DateTimeOffset> stamps, TimeProvider clock, int days = 30)
    {
        var today = Today(clock);
        var first = today.AddDays(-(days - 1));
        var counts = new int[days];
        foreach (var at in stamps)
        {
            var index = DateOnly.FromDateTime(Vietnam(at)).DayNumber - first.DayNumber;
            if (index >= 0 && index < days) counts[index]++;
        }
        return counts.Select((n, i) => { var d = first.AddDays(i); return new DayPoint(d, $"{d.Day:00}/{d.Month:00}", n); }).ToList();
    }

    /// <summary>Chuỗi <paramref name="weeks"/> tuần gần nhất (gồm tuần này); mỗi cột ghi ngày thứ Hai đầu tuần (giờ Việt Nam), nhãn "dd/MM".</summary>
    public static IReadOnlyList<DayPoint> Weeks(IEnumerable<DateTimeOffset> stamps, TimeProvider clock, int weeks = 12)
    {
        var today = Today(clock);
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var first = thisMonday.AddDays(-7 * (weeks - 1));
        var counts = new int[weeks];
        foreach (var at in stamps)
        {
            var index = Math.DivRem(DateOnly.FromDateTime(Vietnam(at)).DayNumber - first.DayNumber, 7, out var rem);
            if (rem < 0) index--;
            if (index >= 0 && index < weeks) counts[index]++;
        }
        return counts.Select((n, i) => { var d = first.AddDays(7 * i); return new DayPoint(d, $"{d.Day:00}/{d.Month:00}", n); }).ToList();
    }

    /// <summary>24 cột theo giờ trong ngày <paramref name="day"/> (giờ Việt Nam).</summary>
    public static int[] Hours(IEnumerable<DateTimeOffset> stamps, DateOnly day)
    {
        var counts = new int[24];
        foreach (var at in stamps)
        {
            var v = Vietnam(at);
            if (DateOnly.FromDateTime(v) == day) counts[v.Hour]++;
        }
        return counts;
    }
}
