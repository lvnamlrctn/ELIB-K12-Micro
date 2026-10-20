using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

/// <summary>Giờ mở cửa hàng tuần theo nhóm không gian (monolith: map.RoomOpeningHour). Category null = áp cho mọi nhóm.</summary>
public sealed class RoomOpeningHour : TenantEntity
{
    private RoomOpeningHour() { }

    public int? Category { get; private set; }

    /// <summary>0=Chủ nhật .. 6=Thứ bảy.</summary>
    public int Weekday { get; private set; }

    public TimeOnly OpenTime { get; private set; }
    public TimeOnly CloseTime { get; private set; }
    public bool IsClosed { get; private set; }

    public static RoomOpeningHour Create(int? category, int weekday, TimeOnly openTime, TimeOnly closeTime, bool isClosed)
    {
        var hour = new RoomOpeningHour();
        hour.Update(category, weekday, openTime, closeTime, isClosed);
        return hour;
    }

    public void Update(int? category, int weekday, TimeOnly openTime, TimeOnly closeTime, bool isClosed)
    {
        Category = category is >= 1 and <= 4 ? category : null;
        Weekday = weekday is >= 0 and <= 6 ? weekday : throw new BusinessRuleException("WEEKDAY_INVALID", "Thứ trong tuần từ 0 (Chủ nhật) đến 6 (Thứ bảy).");
        if (!isClosed && closeTime <= openTime)
            throw new BusinessRuleException("HOURS_INVALID", "Giờ đóng phải sau giờ mở.");
        OpenTime = openTime;
        CloseTime = closeTime;
        IsClosed = isClosed;
    }
}

/// <summary>Ngày đặc biệt (lễ/nghỉ/mở thêm giờ, monolith: map.RoomSpecialDay). Mỗi (Date, Category) chỉ một dòng/đơn vị.</summary>
public sealed class RoomSpecialDay : TenantEntity
{
    public const int MaxReasonLength = 500;

    private RoomSpecialDay() { }

    public DateOnly Date { get; private set; }
    public int? Category { get; private set; }
    public bool IsClosed { get; private set; }
    public TimeOnly? OpenTime { get; private set; }
    public TimeOnly? CloseTime { get; private set; }
    public string? Reason { get; private set; }

    public static RoomSpecialDay Create(DateOnly date, int? category, bool isClosed, TimeOnly? openTime, TimeOnly? closeTime, string? reason)
    {
        var day = new RoomSpecialDay();
        day.Update(date, category, isClosed, openTime, closeTime, reason);
        return day;
    }

    public void Update(DateOnly date, int? category, bool isClosed, TimeOnly? openTime, TimeOnly? closeTime, string? reason)
    {
        Date = date;
        Category = category is >= 1 and <= 4 ? category : null;
        IsClosed = isClosed;
        if (!isClosed)
        {
            if (openTime is null || closeTime is null) throw new BusinessRuleException("HOURS_REQUIRED", "Nhập giờ mở/đóng, hoặc chọn đóng cả ngày.");
            if (closeTime <= openTime) throw new BusinessRuleException("HOURS_INVALID", "Giờ đóng phải sau giờ mở.");
        }
        OpenTime = isClosed ? null : openTime;
        CloseTime = isClosed ? null : closeTime;
        var r = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Reason = r is null || r.Length <= MaxReasonLength ? r : throw new BusinessRuleException("REASON_TOO_LONG", $"Lý do tối đa {MaxReasonLength} ký tự.");
    }
}

/// <summary>Tham số đặt phòng của đơn vị (monolith: SystemParameter ROOM_BOOKING_*) — 1 dòng/đơn vị, tự seed khi đọc lần đầu.</summary>
public sealed class SpaceBookingSettings : TenantEntity
{
    public const int DefaultMaxAdvanceWindowHours = 0;

    private SpaceBookingSettings() { }

    public TimeOnly DefaultOpenTime { get; private set; } = new(7, 0);
    public TimeOnly DefaultCloseTime { get; private set; } = new(21, 0);

    /// <summary>0 = không giới hạn.</summary>
    public int MaxPerDay { get; private set; }

    public int MaxActive { get; private set; }
    public int MaxSessionMinutes { get; private set; }

    /// <summary>0 = tắt cấm tự động.</summary>
    public int NoShowLimit { get; private set; }

    public int NoShowWindowDays { get; private set; } = 30;
    public int BanDays { get; private set; } = 7;

    public static SpaceBookingSettings CreateDefault() => new();

    public void Update(TimeOnly defaultOpenTime, TimeOnly defaultCloseTime, int maxPerDay, int maxActive, int maxSessionMinutes, int noShowLimit,
        int noShowWindowDays, int banDays)
    {
        if (defaultCloseTime <= defaultOpenTime) throw new BusinessRuleException("HOURS_INVALID", "Giờ đóng phải sau giờ mở.");
        DefaultOpenTime = defaultOpenTime;
        DefaultCloseTime = defaultCloseTime;
        MaxPerDay = maxPerDay is >= 0 and <= 1000 ? maxPerDay : throw new BusinessRuleException("MAX_PER_DAY_INVALID", "Giới hạn lượt đặt/ngày từ 0 đến 1000.");
        MaxActive = maxActive is >= 0 and <= 1000 ? maxActive : throw new BusinessRuleException("MAX_ACTIVE_INVALID", "Giới hạn lượt đang hoạt động từ 0 đến 1000.");
        MaxSessionMinutes = maxSessionMinutes is >= 0 and <= 10_000 ? maxSessionMinutes : throw new BusinessRuleException("MAX_SESSION_INVALID", "Giới hạn thời lượng một lượt từ 0 đến 10000 phút.");
        NoShowLimit = noShowLimit is >= 0 and <= 100 ? noShowLimit : throw new BusinessRuleException("NOSHOW_LIMIT_INVALID", "Số lần vắng mặt từ 0 (tắt) đến 100.");
        NoShowWindowDays = noShowWindowDays is >= 1 and <= 365 ? noShowWindowDays : throw new BusinessRuleException("NOSHOW_WINDOW_INVALID", "Khoảng ngày tính vắng mặt từ 1 đến 365.");
        BanDays = banDays is >= 1 and <= 365 ? banDays : throw new BusinessRuleException("BAN_DAYS_INVALID", "Số ngày cấm đặt từ 1 đến 365.");
    }
}
