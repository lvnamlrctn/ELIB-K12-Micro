using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

/// <summary>Toà nhà (monolith: map.MapBuilding) — danh mục đơn giản, không cây, Tầng tham chiếu theo BuildingId.</summary>
public sealed class MapBuilding : TenantEntity
{
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 250;
    public const int MaxDescriptionLength = 1000;

    private MapBuilding() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }

    public static MapBuilding Create(string code, string name, string? description)
    {
        var building = new MapBuilding();
        building.Update(code, name, description);
        return building;
    }

    public void Update(string code, string name, string? description)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= MaxCodeLength ? c : throw new BusinessRuleException("CODE_INVALID", $"Mã toà nhà bắt buộc, tối đa {MaxCodeLength} ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên toà nhà bắt buộc, tối đa {MaxNameLength} ký tự.");
        var d = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Description = d is null || d.Length <= MaxDescriptionLength ? d : throw new BusinessRuleException("DESCRIPTION_TOO_LONG", $"Mô tả tối đa {MaxDescriptionLength} ký tự.");
    }
}

/// <summary>Tầng (monolith: map.MapFloor) — thuộc một Toà nhà. LayoutImageUrl/CanvasWidth/Height dùng cho sơ đồ trực quan.</summary>
public sealed class MapFloor : TenantEntity
{
    public const int MaxNameLength = 250;

    private MapFloor() { }

    public long BuildingId { get; private set; }
    public string Name { get; private set; } = "";
    public int? FloorNumber { get; private set; }

    /// <summary>URL ảnh nền sơ đồ, đã upload qua media (mục đích <see cref="FloorLayout.Code"/>) — xác thực qua <see cref="SetLayout"/>, đúng khuôn Digital's EbookCover.</summary>
    public string? LayoutImageUrl { get; private set; }
    public double CanvasWidth { get; private set; } = 1000;
    public double CanvasHeight { get; private set; } = 700;

    public static MapFloor Create(long buildingId, string name, int? floorNumber)
    {
        var floor = new MapFloor { BuildingId = buildingId };
        floor.Update(buildingId, name, floorNumber);
        return floor;
    }

    public void Update(long buildingId, string name, int? floorNumber)
    {
        BuildingId = buildingId > 0 ? buildingId : throw new BusinessRuleException("BUILDING_REQUIRED", "Chọn toà nhà.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên tầng bắt buộc, tối đa {MaxNameLength} ký tự.");
        FloorNumber = floorNumber;
    }

    /// <summary>Ảnh nền + kích thước canvas tham chiếu cho sơ đồ trực quan — <paramref name="layoutUrl"/> xác thực qua <see cref="FloorLayout.Validate"/> trước khi gọi.</summary>
    public void SetLayout(string? layoutUrl, double width, double height)
    {
        LayoutImageUrl = layoutUrl;
        CanvasWidth = width > 0 ? width : 1000;
        CanvasHeight = height > 0 ? height : 700;
    }
}

/// <summary>
/// Ảnh nền sơ đồ tầng đã upload qua media (mục đích <see cref="Code"/>) — lưu URL đã xác thực thuộc đúng đơn vị/mục đích,
/// đúng khuôn Digital's EbookCover (tránh phải gọi lại media để tra ngược Guid → URL khi hiển thị trên OPAC).
/// </summary>
public static partial class FloorLayout
{
    public const string Code = "space-floorplan";
    public const int MaxUrlLength = 1000;

    /// <summary><paramref name="mediaPublicPrefix"/>: tiền tố công khai của media (gateway /s3 → bucket công khai), xem SpaceOptions.</summary>
    public static string? Validate(string? url, string mediaPublicPrefix, long tenantId)
    {
        var value = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        if (value is null) return null;
        var prefix = $"{(mediaPublicPrefix ?? "").TrimEnd('/')}/{tenantId}/{Code}/";
        if (value.Length > MaxUrlLength || value.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '<' or '>' or '\\')
            || !value.StartsWith(prefix, StringComparison.Ordinal) || !MediaPathPattern().IsMatch(value[prefix.Length..]))
            throw new BusinessRuleException("LAYOUT_URL_INVALID", "Ảnh nền sơ đồ phải là ảnh đã upload của đơn vị này.");
        return value;
    }

    /// <summary>Phần sau tiền tố của key media: yyyy/MM/{guid N}.{png|jpg|webp}.</summary>
    [System.Text.RegularExpressions.GeneratedRegex("^[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\\.(png|jpg|webp)$")]
    private static partial System.Text.RegularExpressions.Regex MediaPathPattern();
}
