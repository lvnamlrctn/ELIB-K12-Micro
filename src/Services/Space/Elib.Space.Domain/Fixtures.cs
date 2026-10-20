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
