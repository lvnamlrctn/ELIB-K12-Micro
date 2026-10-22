using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

/// <summary>Loại vật dụng đặt trên sơ đồ tầng (monolith: map.MapObject.ObjectType, trừ ROOM/STUDY_SPACE — đã có <see cref="BookableSpace"/>).</summary>
public static class FixtureType
{
    public const string Shelf = "SHELF";
    public const string Pc = "PC";
    public const string Counter = "COUNTER";
    public const string ServerRoom = "SERVER_ROOM";
    public const string Office = "OFFICE";
    public const string Door = "DOOR";
    public const string Elevator = "ELEVATOR";
    public const string Restroom = "RESTROOM";
    public const string Other = "OTHER";

    public static readonly string[] All = [Shelf, Pc, Counter, ServerRoom, Office, Door, Elevator, Restroom, Other];

    public static bool IsValid(string value) => Array.IndexOf(All, value) >= 0;
}

/// <summary>
/// Vật dụng đặt trên sơ đồ tầng, không đặt được (monolith: map.MapObject, các ObjectType khác ROOM/STUDY_SPACE — những
/// loại đó dùng <see cref="BookableSpace"/>, có cấu hình đặt phòng). Trường chỉ dùng khi <see cref="ObjectType"/> =
/// <see cref="FixtureType.Shelf"/> gộp thẳng vào đây (như <see cref="BookableSpace"/> gộp cấu hình đặt phòng) thay vì tách
/// bảng map.MapShelfDetail riêng như bản gốc. StorePublicId tham chiếu lỏng tới StoreReplica (như RoomBooking.ReaderPublicId
/// tham chiếu PatronReplica) — chỉ là thông tin mô tả kho vật lý, KHÔNG phải cơ chế dẫn đường định sách thật (cái đó dùng
/// Barcode.MapObjectId/MapShelfRowId ở Holdings, ngoài phạm vi).
/// </summary>
public sealed class MapFixture : TenantEntity, IHasStatus
{
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 250;
    public const int MaxShelfTextLength = 250;
    public const int MaxShelfDescriptionLength = 1000;

    private MapFixture() { }

    public long FloorId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string ObjectType { get; private set; } = FixtureType.Other;

    public double? PositionX { get; private set; }
    public double? PositionY { get; private set; }
    public double? Width { get; private set; }
    public double? Height { get; private set; }
    public string? ColorHex { get; private set; }
    public string? IconName { get; private set; }

    /// <summary>Chỉ dùng khi <see cref="ObjectType"/> = SHELF.</summary>
    public string? ShelfCategoryRange { get; private set; }
    public string? ShelfSubjectName { get; private set; }
    public int? ShelfCapacity { get; private set; }
    public string? ShelfDescription { get; private set; }
    public Guid? StorePublicId { get; private set; }

    public int Status { get; private set; } = IHasStatus.Active;

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public static MapFixture Create(long floorId, string code, string name, string objectType, string? colorHex, string? iconName,
        string? shelfCategoryRange, string? shelfSubjectName, int? shelfCapacity, string? shelfDescription, Guid? storePublicId)
    {
        var fixture = new MapFixture();
        fixture.Update(floorId, code, name, objectType, colorHex, iconName, shelfCategoryRange, shelfSubjectName, shelfCapacity, shelfDescription, storePublicId);
        return fixture;
    }

    public void Update(long floorId, string code, string name, string objectType, string? colorHex, string? iconName,
        string? shelfCategoryRange, string? shelfSubjectName, int? shelfCapacity, string? shelfDescription, Guid? storePublicId)
    {
        FloorId = floorId > 0 ? floorId : throw new BusinessRuleException("FLOOR_REQUIRED", "Chọn tầng.");
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= MaxCodeLength ? c : throw new BusinessRuleException("CODE_INVALID", $"Mã vật dụng bắt buộc, tối đa {MaxCodeLength} ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên vật dụng bắt buộc, tối đa {MaxNameLength} ký tự.");
        ObjectType = FixtureType.IsValid(objectType) ? objectType : throw new BusinessRuleException("OBJECT_TYPE_INVALID", "Loại vật dụng không hợp lệ.");
        var hex = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim();
        ColorHex = hex is null || hex.Length <= 20 ? hex : throw new BusinessRuleException("COLOR_INVALID", "Mã màu tối đa 20 ký tự.");
        var icon = string.IsNullOrWhiteSpace(iconName) ? null : iconName.Trim();
        IconName = icon is null || icon.Length <= 50 ? icon : throw new BusinessRuleException("ICON_INVALID", "Tên biểu tượng tối đa 50 ký tự.");

        var isShelf = ObjectType == FixtureType.Shelf;
        var range = isShelf && !string.IsNullOrWhiteSpace(shelfCategoryRange) ? shelfCategoryRange.Trim() : null;
        ShelfCategoryRange = range is null || range.Length <= MaxShelfTextLength ? range : throw new BusinessRuleException("SHELF_RANGE_TOO_LONG", $"Nhóm DDC tối đa {MaxShelfTextLength} ký tự.");
        var subject = isShelf && !string.IsNullOrWhiteSpace(shelfSubjectName) ? shelfSubjectName.Trim() : null;
        ShelfSubjectName = subject is null || subject.Length <= MaxShelfTextLength ? subject : throw new BusinessRuleException("SHELF_SUBJECT_TOO_LONG", $"Chủ đề tối đa {MaxShelfTextLength} ký tự.");
        ShelfCapacity = isShelf ? shelfCapacity : null;
        var desc = isShelf && !string.IsNullOrWhiteSpace(shelfDescription) ? shelfDescription.Trim() : null;
        ShelfDescription = desc is null || desc.Length <= MaxShelfDescriptionLength ? desc : throw new BusinessRuleException("SHELF_DESCRIPTION_TOO_LONG", $"Mô tả giá sách tối đa {MaxShelfDescriptionLength} ký tự.");
        StorePublicId = isShelf ? storePublicId : null;
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

/// <summary>Tình trạng thiết bị/tiện ích (monolith: 2 từ vựng khác nhau cho MapEquipment/MapFloorUtility — gộp về 1 ở đây).</summary>
public static class ConditionStatusRules
{
    public const int Ready = 1;
    public const int Working = 2;
    public const int Maintenance = 3;

    public static readonly int[] All = [Ready, Working, Maintenance];

    public static int Validate(int value) => Array.IndexOf(All, value) >= 0 ? value : throw new BusinessRuleException("CONDITION_STATUS_INVALID", "Tình trạng không hợp lệ.");
}

/// <summary>Thiết bị gắn theo một vật dụng trên sơ đồ (monolith: map.MapEquipment) — máy chiếu, bàn ghế... Áp dụng cho mọi loại <see cref="MapFixture"/>.</summary>
public sealed class MapFixtureEquipment : TenantEntity
{
    public const int MaxNameLength = 250;
    public const int MaxDescriptionLength = 1000;

    private MapFixtureEquipment() { }

    public long FixtureId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public int? Quantity { get; private set; }
    public int ConditionStatus { get; private set; } = ConditionStatusRules.Ready;

    public static MapFixtureEquipment Create(long fixtureId, string name, string? description, int? quantity, int conditionStatus)
    {
        var equipment = new MapFixtureEquipment { FixtureId = fixtureId };
        equipment.Update(name, description, quantity, conditionStatus);
        return equipment;
    }

    public void Update(string name, string? description, int? quantity, int conditionStatus)
    {
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên thiết bị bắt buộc, tối đa {MaxNameLength} ký tự.");
        var d = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Description = d is null || d.Length <= MaxDescriptionLength ? d : throw new BusinessRuleException("DESCRIPTION_TOO_LONG", $"Mô tả tối đa {MaxDescriptionLength} ký tự.");
        Quantity = quantity is null or >= 0 ? quantity : throw new BusinessRuleException("QUANTITY_INVALID", "Số lượng không được âm.");
        ConditionStatus = ConditionStatusRules.Validate(conditionStatus);
    }
}

/// <summary>Tiện ích theo tầng (monolith: map.MapFloorUtility) — bình chữa cháy, camera... gắn theo cả tầng, không theo vật cụ thể.</summary>
public sealed class MapFloorUtility : TenantEntity
{
    public const int MaxNameLength = 250;
    public const int MaxDescriptionLength = 1000;

    private MapFloorUtility() { }

    public long FloorId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public int? Quantity { get; private set; }
    public int ConditionStatus { get; private set; } = ConditionStatusRules.Ready;
    public string? IconName { get; private set; }

    public static MapFloorUtility Create(long floorId, string name, string? description, int? quantity, int conditionStatus, string? iconName)
    {
        var utility = new MapFloorUtility { FloorId = floorId };
        utility.Update(name, description, quantity, conditionStatus, iconName);
        return utility;
    }

    public void Update(string name, string? description, int? quantity, int conditionStatus, string? iconName)
    {
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên tiện ích bắt buộc, tối đa {MaxNameLength} ký tự.");
        var d = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Description = d is null || d.Length <= MaxDescriptionLength ? d : throw new BusinessRuleException("DESCRIPTION_TOO_LONG", $"Mô tả tối đa {MaxDescriptionLength} ký tự.");
        Quantity = quantity is null or >= 0 ? quantity : throw new BusinessRuleException("QUANTITY_INVALID", "Số lượng không được âm.");
        ConditionStatus = ConditionStatusRules.Validate(conditionStatus);
        var icon = string.IsNullOrWhiteSpace(iconName) ? null : iconName.Trim();
        IconName = icon is null || icon.Length <= 50 ? icon : throw new BusinessRuleException("ICON_INVALID", "Tên biểu tượng tối đa 50 ký tự.");
    }
}

/// <summary>
/// Ngăn của một giá sách theo khoảng DDC (monolith: map.MapShelfRow, quyền SPACE_FIXTURES). Bản sách có DDC trong khoảng
/// [<see cref="DdcStart"/>, <see cref="DdcEnd"/>] — hoặc bắt đầu bằng <see cref="DdcEnd"/> (ví dụ "339.5" thuộc khoảng 330–339) — thuộc ngăn này.
/// Các ngăn cùng giá không chồng khoảng DDC.
/// </summary>
public sealed class ShelfRow : TenantEntity
{
    public const int MaxDdcLength = 30;
    public const int MaxDescriptionLength = 500;

    private ShelfRow() { }

    public long FixtureId { get; private set; }
    public int RowIndex { get; private set; }
    public string DdcStart { get; private set; } = "";
    public string DdcEnd { get; private set; } = "";
    public string? Description { get; private set; }

    public static ShelfRow Create(long fixtureId, int rowIndex, string ddcStart, string ddcEnd, string? description)
    {
        var row = new ShelfRow { FixtureId = fixtureId };
        row.Update(rowIndex, ddcStart, ddcEnd, description);
        return row;
    }

    public void Update(int rowIndex, string ddcStart, string ddcEnd, string? description)
    {
        RowIndex = rowIndex >= 1 ? rowIndex : throw new BusinessRuleException("ROW_INDEX_INVALID", "Số thứ tự ngăn phải từ 1.");
        var start = (ddcStart ?? "").Trim();
        var end = (ddcEnd ?? "").Trim();
        if (start.Length is 0 or > MaxDdcLength || end.Length is 0 or > MaxDdcLength)
            throw new BusinessRuleException("DDC_INVALID", $"DDC từ/đến bắt buộc, tối đa {MaxDdcLength} ký tự.");
        if (string.CompareOrdinal(start, end) > 0)
            throw new BusinessRuleException("DDC_RANGE_INVALID", "DDC từ phải nhỏ hơn hoặc bằng DDC đến.");
        (DdcStart, DdcEnd) = (start, end);
        var d = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Description = d is null || d.Length <= MaxDescriptionLength ? d : throw new BusinessRuleException("DESCRIPTION_TOO_LONG", $"Mô tả tối đa {MaxDescriptionLength} ký tự.");
    }

    /// <summary>Hai khoảng chồng nhau? Khoảng đến được mở rộng theo tiền tố ("339" gồm cả "339.5").</summary>
    public static bool Overlaps(string startA, string endA, string startB, string endB) =>
        string.CompareOrdinal(startA, endB + '\uffff') <= 0 && string.CompareOrdinal(startB, endA + '\uffff') <= 0;
}
