using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

/// <summary>
/// Tủ đựng đồ trên sơ đồ tầng (monolith: PrintBook.Cabinet, quyền SPACE_LOCKERS) — lưới <see cref="Rows"/>×<see cref="Cols"/>
/// ngăn (<see cref="LockerCompartment"/>). Đặt theo FloorId, dùng CHUNG sơ đồ/kéo-thả/resize với phòng (BookableSpace) và
/// vật dụng (MapFixture) — khác bản gốc đặt trên mini-canvas riêng theo điểm lưu thông (CircPlaceId), có chủ đích để khớp
/// sơ đồ tầng thống nhất và tái dùng cơ chế kéo-thả đã có.
/// </summary>
public sealed class LockerCabinet : TenantEntity, IHasStatus
{
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 250;
    public const int MaxNoteLength = 500;
    public const int MinGridSize = 1;
    public const int MaxGridSize = 20;

    private LockerCabinet() { }

    public long FloorId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int Rows { get; private set; }
    public int Cols { get; private set; }

    public double? PositionX { get; private set; }
    public double? PositionY { get; private set; }
    public double? Width { get; private set; }
    public double? Height { get; private set; }
    public string? ColorHex { get; private set; }
    public string? IconName { get; private set; }
    public string? Note { get; private set; }

    public int Status { get; private set; } = IHasStatus.Active;

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public static LockerCabinet Create(long floorId, string code, string name, int rows, int cols, string? colorHex, string? iconName, string? note)
    {
        var cabinet = new LockerCabinet();
        cabinet.Update(floorId, code, name, rows, cols, colorHex, iconName, note);
        return cabinet;
    }

    public void Update(long floorId, string code, string name, int rows, int cols, string? colorHex, string? iconName, string? note)
    {
        FloorId = floorId > 0 ? floorId : throw new BusinessRuleException("FLOOR_REQUIRED", "Chọn tầng.");
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= MaxCodeLength ? c : throw new BusinessRuleException("CODE_INVALID", $"Mã tủ bắt buộc, tối đa {MaxCodeLength} ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên tủ bắt buộc, tối đa {MaxNameLength} ký tự.");
        Rows = rows is >= MinGridSize and <= MaxGridSize ? rows : throw new BusinessRuleException("ROWS_INVALID", $"Số dòng phải từ {MinGridSize} đến {MaxGridSize}.");
        Cols = cols is >= MinGridSize and <= MaxGridSize ? cols : throw new BusinessRuleException("COLS_INVALID", $"Số cột phải từ {MinGridSize} đến {MaxGridSize}.");
        var hex = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim();
        ColorHex = hex is null || hex.Length <= 20 ? hex : throw new BusinessRuleException("COLOR_INVALID", "Mã màu tối đa 20 ký tự.");
        var icon = string.IsNullOrWhiteSpace(iconName) ? null : iconName.Trim();
        IconName = icon is null || icon.Length <= 50 ? icon : throw new BusinessRuleException("ICON_INVALID", "Tên biểu tượng tối đa 50 ký tự.");
        var note2 = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Note = note2 is null || note2.Length <= MaxNoteLength ? note2 : throw new BusinessRuleException("NOTE_TOO_LONG", $"Ghi chú tối đa {MaxNoteLength} ký tự.");
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
}

/// <summary>
/// Ngăn trong tủ (monolith: PrintBook.CabinetCompartment) — sinh tự động theo Rows×Cols của tủ khi tạo/đổi lưới (xem
/// LockerCabinetResource), nhưng vẫn cho thêm/sửa/xoá riêng từng ngăn (đúng bản gốc). Code buộc duy nhất theo đơn vị —
/// cải tiến có chủ đích, bản gốc không ép.
/// </summary>
public sealed class LockerCompartment : TenantEntity, IHasStatus
{
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 250;
    public const int MaxNoteLength = 500;

    private LockerCompartment() { }

    public long CabinetId { get; private set; }
    public int RowIndex { get; private set; }
    public int ColIndex { get; private set; }
    public string Code { get; private set; } = "";
    public string? Name { get; private set; }
    public string? Note { get; private set; }

    public int Status { get; private set; } = IHasStatus.Active;

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public static LockerCompartment Create(long cabinetId, int rowIndex, int colIndex, string code, string? name, string? note)
    {
        var compartment = new LockerCompartment { CabinetId = cabinetId, RowIndex = rowIndex, ColIndex = colIndex };
        compartment.Update(code, name, note);
        return compartment;
    }

    public void Update(string code, string? name, string? note)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= MaxCodeLength ? c : throw new BusinessRuleException("CODE_INVALID", $"Mã ngăn bắt buộc, tối đa {MaxCodeLength} ký tự.");
        var n = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Name = n is null || n.Length <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên ngăn tối đa {MaxNameLength} ký tự.");
        var note2 = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Note = note2 is null || note2.Length <= MaxNoteLength ? note2 : throw new BusinessRuleException("NOTE_TOO_LONG", $"Ghi chú tối đa {MaxNoteLength} ký tự.");
    }
}

/// <summary>
/// Lượt mượn/trả ngăn tủ (monolith: PrintBook.KeyOut + KeyIn gộp một bảng, quyền SPACE_LOCKER_LOANS) — đóng bằng
/// <see cref="ReturnedAt"/> nullable (đúng khuôn <see cref="RoomBookingBan.LiftedAt"/>), không copy sang bảng khác và
/// không xoá dòng như bản gốc, giữ nguyên lịch sử. Không hạn trả/không phí trễ hạn, một bạn đọc có thể giữ nhiều ngăn
/// cùng lúc — đúng hành vi bản gốc.
/// </summary>
public sealed class LockerLoan : TenantEntity
{
    public const int MaxNoteLength = 500;

    private LockerLoan() { }

    public long CompartmentId { get; private set; }
    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public DateTimeOffset BorrowedAt { get; private set; }
    public long? BorrowedBy { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public long? ReturnedBy { get; private set; }
    public string? Note { get; private set; }

    public bool IsOpen => ReturnedAt is null;

    public static LockerLoan Open(long compartmentId, Guid readerPublicId, string cardNo, long? staffId, DateTimeOffset now, string? note = null)
    {
        var n = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return new LockerLoan
        {
            CompartmentId = compartmentId, ReaderPublicId = readerPublicId, CardNo = (cardNo ?? "").Trim().ToUpperInvariant(),
            BorrowedAt = now, BorrowedBy = staffId, Note = n is null || n.Length <= MaxNoteLength ? n : n[..MaxNoteLength],
        };
    }

    public void Return(long? staffId, DateTimeOffset now)
    {
        if (ReturnedAt is not null) throw new BusinessRuleException("LOAN_ALREADY_RETURNED", "Ngăn tủ đã được trả trước đó.");
        ReturnedAt = now;
        ReturnedBy = staffId;
    }
}
