using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

/// <summary>
/// Không gian/phòng đặt được (monolith: map.MapObject ObjectType ROOM/STUDY_SPACE + map.RoomBookingConfig gộp một — khác bản gốc
/// tách hai bảng, ở đây một không gian luôn có đủ cấu hình đặt phòng ngay trên entity, như circulation's Loan gộp
/// BookOut/BookIn của monolith). Vị trí trên sơ đồ (PositionX/Y/Width/Height) đặt riêng qua <see cref="SetPosition"/> (trang sơ đồ
/// trực quan, lát sau) — không nằm trong form cấu hình thường.
/// </summary>
public sealed class BookableSpace : TenantEntity, IHasStatus
{
    public const int KindRoom = 1;
    public const int KindStudySpace = 2;

    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 250;
    public const int MaxRulesLength = 4000;
    public const int MaxMaintenanceNoteLength = 500;
    public const int DefaultCheckInGraceMinutes = 15;

    private BookableSpace() { }

    public long FloorId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";

    /// <summary><see cref="KindRoom"/> (phòng học nhóm) hoặc <see cref="KindStudySpace"/> (không gian cá nhân).</summary>
    public int Kind { get; private set; } = KindRoom;

    /// <summary>1=Không gian học tập, 2=Phòng học nhóm thảo luận, 3=Công nghệ và Stem, 4=Khác — dùng tra giờ mở cửa theo nhóm.</summary>
    public int Category { get; private set; } = 1;

    public double? PositionX { get; private set; }
    public double? PositionY { get; private set; }
    public double? Width { get; private set; }
    public double? Height { get; private set; }
    public string? ColorHex { get; private set; }
    public string? IconName { get; private set; }

    public int Capacity { get; private set; } = 1;
    public int MinAdvanceMinutes { get; private set; }
    public int MaxAdvanceDays { get; private set; } = 7;

    /// <summary>&gt;0 thì thay cho <see cref="MaxAdvanceDays"/> (đặt trước tối đa tính theo giờ).</summary>
    public int? MaxAdvanceHours { get; private set; }

    public int MaxBookingMinutesPerReader { get; private set; }
    public int CheckInGraceMinutes { get; private set; } = DefaultCheckInGraceMinutes;
    public bool AutoApprove { get; private set; }
    public string? Rules { get; private set; }
    public List<long> AllowedReaderTypeIds { get; private set; } = [];
    public int? MinGroupSize { get; private set; }
    public bool Maintenance { get; private set; }
    public string? MaintenanceNote { get; private set; }

    public int Status { get; private set; } = IHasStatus.Active;

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public static BookableSpace Create(long floorId, string code, string name, int kind, int category, int capacity, int minAdvanceMinutes,
        int maxAdvanceDays, int? maxAdvanceHours, int maxBookingMinutesPerReader, int checkInGraceMinutes, bool autoApprove, string? rules,
        IEnumerable<long>? allowedReaderTypeIds, int? minGroupSize, string? colorHex, string? iconName)
    {
        var space = new BookableSpace();
        space.Update(floorId, code, name, kind, category, capacity, minAdvanceMinutes, maxAdvanceDays, maxAdvanceHours, maxBookingMinutesPerReader,
            checkInGraceMinutes, autoApprove, rules, allowedReaderTypeIds, minGroupSize, colorHex, iconName);
        return space;
    }

    public void Update(long floorId, string code, string name, int kind, int category, int capacity, int minAdvanceMinutes, int maxAdvanceDays,
        int? maxAdvanceHours, int maxBookingMinutesPerReader, int checkInGraceMinutes, bool autoApprove, string? rules,
        IEnumerable<long>? allowedReaderTypeIds, int? minGroupSize, string? colorHex, string? iconName)
    {
        FloorId = floorId > 0 ? floorId : throw new BusinessRuleException("FLOOR_REQUIRED", "Chọn tầng.");
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= MaxCodeLength ? c : throw new BusinessRuleException("CODE_INVALID", $"Mã không gian bắt buộc, tối đa {MaxCodeLength} ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên không gian bắt buộc, tối đa {MaxNameLength} ký tự.");
        Kind = kind is KindRoom or KindStudySpace ? kind : throw new BusinessRuleException("KIND_INVALID", "Loại không gian là Phòng học nhóm hoặc Không gian cá nhân.");
        Category = category is >= 1 and <= 4 ? category : throw new BusinessRuleException("CATEGORY_INVALID", "Nhóm không gian từ 1 đến 4.");
        Capacity = capacity is >= 1 and <= 1000 ? capacity : throw new BusinessRuleException("CAPACITY_INVALID", "Sức chứa từ 1 đến 1000.");
        MinAdvanceMinutes = minAdvanceMinutes is >= 0 and <= 10_000 ? minAdvanceMinutes : throw new BusinessRuleException("MIN_ADVANCE_INVALID", "Thời gian đặt trước tối thiểu từ 0 đến 10000 phút.");
        MaxAdvanceDays = maxAdvanceDays is >= 1 and <= 365 ? maxAdvanceDays : throw new BusinessRuleException("MAX_ADVANCE_DAYS_INVALID", "Đặt trước tối đa từ 1 đến 365 ngày.");
        MaxAdvanceHours = maxAdvanceHours is null or (> 0 and <= 8760) ? maxAdvanceHours : throw new BusinessRuleException("MAX_ADVANCE_HOURS_INVALID", "Đặt trước tối đa (giờ) từ 1 đến 8760, hoặc để trống.");
        MaxBookingMinutesPerReader = maxBookingMinutesPerReader is >= 0 and <= 100_000 ? maxBookingMinutesPerReader : throw new BusinessRuleException("MAX_MINUTES_INVALID", "Giới hạn phút/ngày từ 0 (không giới hạn) đến 100000.");
        CheckInGraceMinutes = checkInGraceMinutes is >= 0 and <= 240 ? checkInGraceMinutes : throw new BusinessRuleException("GRACE_INVALID", "Thời gian cho phép check-in muộn từ 0 đến 240 phút.");
        AutoApprove = autoApprove;
        var r = string.IsNullOrWhiteSpace(rules) ? null : rules.Trim();
        Rules = r is null || r.Length <= MaxRulesLength ? r : throw new BusinessRuleException("RULES_TOO_LONG", $"Nội quy tối đa {MaxRulesLength} ký tự.");
        AllowedReaderTypeIds = [.. (allowedReaderTypeIds ?? []).Where(id => id > 0).Distinct()];
        MinGroupSize = minGroupSize is null or (>= 1 and <= 1000) ? minGroupSize : throw new BusinessRuleException("MIN_GROUP_INVALID", "Số người tối thiểu từ 1 đến 1000, hoặc để trống.");
        if (MinGroupSize > Capacity) throw new BusinessRuleException("MIN_GROUP_EXCEEDS_CAPACITY", "Số người tối thiểu không được vượt sức chứa.");
        var hex = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim();
        ColorHex = hex is null || hex.Length <= 20 ? hex : throw new BusinessRuleException("COLOR_INVALID", "Mã màu tối đa 20 ký tự.");
        var icon = string.IsNullOrWhiteSpace(iconName) ? null : iconName.Trim();
        IconName = icon is null || icon.Length <= 50 ? icon : throw new BusinessRuleException("ICON_INVALID", "Tên biểu tượng tối đa 50 ký tự.");
    }

    /// <summary>Vị trí trên sơ đồ tầng (% 0-100) — trang sơ đồ trực quan gọi riêng, không qua form cấu hình thường.</summary>
    public void SetPosition(double? x, double? y, double? width, double? height)
    {
        PositionX = Clamp(x);
        PositionY = Clamp(y);
        Width = Clamp(width);
        Height = Clamp(height);
    }

    private static double? Clamp(double? value) => value is { } v ? Math.Clamp(v, 0, 100) : null;

    /// <summary>Tạm ngưng (bảo trì) hoặc mở lại — không chặn hẳn như <see cref="Status"/>, chỉ tạm thời kèm lý do.</summary>
    public void SetMaintenance(bool on, string? note)
    {
        Maintenance = on;
        var n = on && !string.IsNullOrWhiteSpace(note) ? note.Trim() : null;
        MaintenanceNote = n is null || n.Length <= MaxMaintenanceNoteLength ? n : throw new BusinessRuleException("MAINTENANCE_NOTE_TOO_LONG", $"Lý do tạm ngưng tối đa {MaxMaintenanceNoteLength} ký tự.");
    }
}
