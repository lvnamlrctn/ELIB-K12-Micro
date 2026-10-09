using System.Globalization;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Giờ mở cửa đặt phòng học nhóm (ROOM_BOOKING_OPEN_TIME / ROOM_BOOKING_CLOSE_TIME, "HH:mm") và bước
/// lưới giờ cố định 30 phút. Thiếu/sai tham số thì dùng mặc định 07:00–21:00, không cần seed.
/// RoomBooking.StartAt/EndAt lưu theo UTC (OPAC gửi ISO có "Z", container chạy UTC) nên mọi so sánh với giờ
/// mở cửa phải quy về giờ thư viện (Asia/Ho_Chi_Minh).
/// Tenant (K12): tham số, giờ theo thứ và ngày đặc biệt đọc theo đơn vị của phòng/bạn đọc — dòng của đơn vị được ưu tiên, không có
/// thì dùng dòng dùng chung (TenantId null); tenantId null chỉ đọc dòng dùng chung.</summary>
public static class RoomBookingHours
{
    public const int StepMinutes = 30;
    /// <summary>Ân hạn check-in khi phòng chưa có cấu hình (khớp mặc định RoomBookingConfig.CheckInGraceMinutes).</summary>
    public const int DefaultCheckInGraceMinutes = 15;
    /// <summary>Mở check-in (và cửa phòng cho thẻ) trước giờ bắt đầu bao nhiêu phút.</summary>
    public const int CheckInOpenMinutes = 15;
    private static readonly TimeSpan DefaultOpen  = new(7, 0, 0);
    private static readonly TimeSpan DefaultClose = new(21, 0, 0);

    public static readonly TimeZoneInfo LibraryTimeZone = ELIBAPI.Core.Common.LibraryClock.TimeZone;

    public static async Task<(TimeSpan Open, TimeSpan Close)> GetAsync(ELIBAPIDbContext db, long? tenantId)
    {
        var open  = Parse(await RoomBookingParams.ValueAsync(db, "ROOM_BOOKING_OPEN_TIME", tenantId))  ?? DefaultOpen;
        var close = Parse(await RoomBookingParams.ValueAsync(db, "ROOM_BOOKING_CLOSE_TIME", tenantId)) ?? DefaultClose;
        return open < close ? (open, close) : (DefaultOpen, DefaultClose);
    }

    public static string Format(TimeSpan t) => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>Giờ lưu trong DB (UTC, Kind thường là Unspecified khi đọc lên) → giờ thư viện.</summary>
    public static DateTime ToLibraryLocal(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => TimeZoneInfo.ConvertTime(value, LibraryTimeZone),
        _                  => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), LibraryTimeZone),
    };

    /// <summary>Giờ bạn đọc gửi lên → UTC để lưu/so sánh. "…Z" (Utc) và không kèm múi giờ (Unspecified) coi là UTC;
    /// kèm offset (vd "+07:00", JSON đọc ra Kind=Local theo múi giờ máy chủ) thì đổi về UTC.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime()
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>Khoảng UTC [start, end) phủ trọn 1 ngày theo giờ thư viện.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) LibraryDayUtcRange(DateTime date)
    {
        var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), LibraryTimeZone);
        return (start, start.AddDays(1));
    }

    /// <summary>Lỗi nghiệp vụ (null = hợp lệ): đúng mốc 30 phút, trong cùng 1 ngày, nằm trong giờ mở cửa.</summary>
    public static string? Validate(DateTime startAt, DateTime endAt, TimeSpan open, TimeSpan close)
    {
        var start = ToLibraryLocal(startAt);
        var end   = ToLibraryLocal(endAt);
        if (!OnStep(start) || !OnStep(end))
            return $"Giờ bắt đầu và kết thúc phải theo mốc {StepMinutes} phút (vd 08:00, 08:30).";
        if (start.Date != end.Date)
            return "Mỗi lượt đặt phải nằm trong cùng một ngày.";
        if (start.TimeOfDay < open || end.TimeOfDay > close)
            return $"Chỉ đặt được trong giờ mở cửa {Format(open)} - {Format(close)}.";
        return null;
    }

    /// <summary>Giờ mở cửa của 1 ngày (giờ thư viện) cho từng loại cơ sở. Ưu tiên: ngày đặc biệt của đúng loại → ngày đặc biệt
    /// mọi loại → giờ theo thứ của đúng loại → giờ theo thứ mọi loại → ROOM_BOOKING_OPEN/CLOSE_TIME. Chưa khai báo gì thì
    /// giống hệt trước đây (1 khung chung cho mọi ngày). Dòng có giờ sai/mở ≥ đóng bị bỏ qua, xét mức kế tiếp.</summary>
    public static async Task<Func<int?, RoomDayHours>> DayResolverAsync(ELIBAPIDbContext db, long? tenantId, DateTime libraryDate)
    {
        var (open, close) = await GetAsync(db, tenantId);
        var day = libraryDate.Date;
        var weekday = (int)day.DayOfWeek;
        var specials = await db.RoomSpecialDays.AsNoTracking()
            .Where(x => x.Date >= day && x.Date < day.AddDays(1) && (x.TenantId == null || x.TenantId == tenantId)).ToListAsync();
        var weekly = await db.RoomOpeningHours.AsNoTracking()
            .Where(x => x.Weekday == weekday && (x.TenantId == null || x.TenantId == tenantId)).ToListAsync();
        return category => Resolve(specials, weekly, category, open, close);
    }

    /// <summary>Như <see cref="DayResolverAsync"/> nhưng cho cả khoảng ngày (báo cáo) — đọc tham số và bảng giờ 1 lần.</summary>
    public static async Task<Func<DateTime, int?, RoomDayHours>> RangeResolverAsync(ELIBAPIDbContext db, long? tenantId, DateTime fromDate, DateTime toDate)
    {
        var (open, close) = await GetAsync(db, tenantId);
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1);
        var specials = await db.RoomSpecialDays.AsNoTracking()
            .Where(x => x.Date >= from && x.Date < to && (x.TenantId == null || x.TenantId == tenantId)).ToListAsync();
        var weekly = await db.RoomOpeningHours.AsNoTracking().Where(x => x.TenantId == null || x.TenantId == tenantId).ToListAsync();
        return (day, category) => Resolve(specials.Where(x => x.Date.Date == day.Date).ToList(),
            weekly.Where(x => x.Weekday == (int)day.DayOfWeek).ToList(), category, open, close);
    }

    public static async Task<RoomDayHours> ResolveAsync(ELIBAPIDbContext db, long? tenantId, DateTime libraryDate, int? category) =>
        (await DayResolverAsync(db, tenantId, libraryDate))(category);

    private static RoomDayHours Resolve(List<RoomSpecialDay> specials, List<RoomOpeningHour> weekly, int? category, TimeSpan open, TimeSpan close)
    {
        foreach (var s in Candidates(specials, x => x.Category, x => x.TenantId, category))
        {
            if (s.IsClosed) return new RoomDayHours(true, open, close, s.Reason);
            if (Window(s.OpenTime, s.CloseTime) is { } w) return new RoomDayHours(false, w.Open, w.Close, s.Reason);
        }
        foreach (var h in Candidates(weekly, x => x.Category, x => x.TenantId, category))
        {
            if (h.IsClosed) return new RoomDayHours(true, open, close, null);
            if (Window(h.OpenTime, h.CloseTime) is { } w) return new RoomDayHours(false, w.Open, w.Close, null);
        }
        return new RoomDayHours(false, open, close, null);
    }

    /// <summary>Thứ tự xét: đơn vị + đúng loại → đơn vị + mọi loại → dùng chung + đúng loại → dùng chung + mọi loại. Dòng đã lọc sẵn
    /// (TenantId = đơn vị hoặc null), nên cấu hình của đơn vị luôn thắng cấu hình dùng chung.</summary>
    private static IEnumerable<T> Candidates<T>(List<T> rows, Func<T, int?> categoryOf, Func<T, long?> tenantOf, int? category)
    {
        IEnumerable<T> Level(IEnumerable<T> level) =>
            (category == null ? [] : level.Where(r => categoryOf(r) == category)).Concat(level.Where(r => categoryOf(r) == null));
        return Level(rows.Where(r => tenantOf(r) != null)).Concat(Level(rows.Where(r => tenantOf(r) == null)));
    }

    /// <summary>Khoảng "HH:mm" hợp lệ (mở &lt; đóng) hoặc null.</summary>
    public static (TimeSpan Open, TimeSpan Close)? Window(string? openTime, string? closeTime) =>
        Parse(openTime) is { } o && Parse(closeTime) is { } c && o < c ? (o, c) : null;

    public static bool IsValidTime(string? raw) => Parse(raw) is { } t && t < TimeSpan.FromDays(1);

    private static bool OnStep(DateTime t) => t.Second == 0 && t.Millisecond == 0 && t.Minute % StepMinutes == 0;

    private static TimeSpan? Parse(string? raw)
    {
        var s = System.Text.RegularExpressions.Regex.Replace(raw ?? "", "<.*?>", "").Trim();
        return TimeSpan.TryParseExact(s, [@"h\:mm", @"hh\:mm"], CultureInfo.InvariantCulture, out var t) ? t : null;
    }
}

/// <summary>Giờ mở cửa đã quy ra cho 1 ngày + loại cơ sở. Closed = đóng cả ngày (Reason: lý do ngày đặc biệt nếu có).</summary>
public sealed record RoomDayHours(bool Closed, TimeSpan Open, TimeSpan Close, string? Reason);

/// <summary>Tham số chung về đặt phòng (SystemParameter). Thiếu/sai → mặc định = không giới hạn / tắt, nên thư viện chưa cấu
/// hình giữ nguyên hành vi cũ.</summary>
public sealed record RoomBookingRuleSet(int MaxPerDay, int MaxActive, int MaxSessionMinutes, int NoShowLimit, int NoShowWindowDays, int BanDays);

public static class RoomBookingRules
{
    public const string MaxPerDayKey         = "ROOM_BOOKING_MAX_PER_DAY";
    public const string MaxActiveKey         = "ROOM_BOOKING_MAX_ACTIVE";
    public const string MaxSessionMinutesKey = "ROOM_BOOKING_MAX_SESSION_MINUTES";
    public const string NoShowLimitKey       = "ROOM_BOOKING_NOSHOW_LIMIT";
    public const string NoShowWindowDaysKey  = "ROOM_BOOKING_NOSHOW_WINDOW_DAYS";
    public const string BanDaysKey           = "ROOM_BOOKING_BAN_DAYS";

    public const int DefaultNoShowWindowDays = 30;
    public const int DefaultBanDays = 7;

    public static async Task<RoomBookingRuleSet> GetAsync(ELIBAPIDbContext db, long? tenantId) => new(
        await IntAsync(db, tenantId, MaxPerDayKey, 0),
        await IntAsync(db, tenantId, MaxActiveKey, 0),
        await IntAsync(db, tenantId, MaxSessionMinutesKey, 0),
        await IntAsync(db, tenantId, NoShowLimitKey, 0),
        await IntAsync(db, tenantId, NoShowWindowDaysKey, DefaultNoShowWindowDays, positive: true),
        await IntAsync(db, tenantId, BanDaysKey, DefaultBanDays, positive: true));

    /// <summary>"1, 3,x,3" → [1, 3]: bỏ giá trị không phải số, trùng lặp.</summary>
    public static List<long> ParseIds(string? raw) =>
        (raw ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => long.TryParse(x, out var v) ? v : 0).Where(v => v > 0).Distinct().ToList();

    private static async Task<int> IntAsync(ELIBAPIDbContext db, long? tenantId, string key, int fallback, bool positive = false)
    {
        var raw = System.Text.RegularExpressions.Regex.Replace(await RoomBookingParams.ValueAsync(db, key, tenantId) ?? "", "<.*?>", "").Trim();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && (positive ? v > 0 : v >= 0) ? v : fallback;
    }
}

/// <summary>Đọc SystemParameter của đặt phòng theo đơn vị: dòng của đơn vị trước, không có thì dòng dùng chung (TenantId null).
/// tenantId null chỉ đọc dòng dùng chung — khác ISystemParameterService.GetValueAsync(code, null) (lấy dòng của bất kỳ đơn vị nào),
/// vì tài khoản hệ thống sửa cấu hình dùng chung và job chạy theo từng đơn vị.</summary>
public static class RoomBookingParams
{
    public static Task<string?> ValueAsync(ELIBAPIDbContext db, string code, long? tenantId) => TenantParams.ValueAsync(db, code, tenantId);

    /// <summary>Cờ bật/tắt ("1"; bỏ thẻ HTML do trang tham số dùng trình soạn thảo).</summary>
    public static Task<bool> IsEnabledAsync(ELIBAPIDbContext db, string code, long? tenantId) => TenantParams.IsEnabledAsync(db, code, tenantId);
}

/// <summary>
/// Che thông tin bạn đọc trên lịch đặt phòng công khai (FloorBoard): người xem không đăng nhập vẫn thấy
/// phòng đã có nhóm đặt nhưng không đọc được họ tên/mã thẻ đầy đủ của người khác.
/// </summary>
public static class RoomBookingPrivacy
{
    /// <summary>"Họ" + "Tên" gộp khoảng trắng thừa (dữ liệu cũ có tên bắt đầu bằng dấu cách).</summary>
    public static string FullName(string? firstName, string? lastName) =>
        string.Join(' ', $"{firstName} {lastName}".Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>Giữ chữ đầu (họ), các chữ sau thành chữ cái đầu: "Phạm Vũ Anh" → "Phạm V. A.".</summary>
    public static string MaskName(string? fullName)
    {
        var parts = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "";
        if (parts.Length == 1) return parts[0].Length <= 1 ? parts[0] : $"{parts[0][0]}.";
        return string.Join(" ", parts.Take(1).Concat(parts.Skip(1).Select(p => $"{p[0]}.")));
    }

    /// <summary>Giữ tối đa 4 ký tự đầu (luôn che ít nhất nửa mã): "SV20230551" → "SV20******".</summary>
    public static string MaskCardNo(string? cardNo)
    {
        var s = (cardNo ?? "").Trim();
        if (s.Length == 0) return "";
        var keep = Math.Min(4, s.Length / 2);
        return s[..keep] + new string('*', s.Length - keep);
    }
}
