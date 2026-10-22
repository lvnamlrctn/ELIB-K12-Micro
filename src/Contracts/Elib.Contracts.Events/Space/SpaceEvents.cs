namespace Elib.Contracts.Events.Space;

/// <summary>Một ngăn của giá sách theo khoảng DDC (monolith: map.MapShelfRow).</summary>
public sealed record ShelfRowState
{
    public required Guid RowPublicId { get; init; }
    public required int RowIndex { get; init; }

    /// <summary>Khoảng DDC của ngăn: bản sách có DDC &gt;= <see cref="DdcStart"/> và &lt;= <see cref="DdcEnd"/> (hoặc bắt đầu bằng <see cref="DdcEnd"/>) thuộc ngăn này.</summary>
    public required string DdcStart { get; init; }
    public required string DdcEnd { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Giá sách trên sơ đồ tầng (vật dụng loại SHELF của space) kèm các ngăn. holdings giữ bản sao để xếp giá trực quan (chọn giá/ngăn
/// cho bản sách) và gợi ý vị trí theo DDC. Phát khi giá hoặc ngăn đổi, giá bị xoá, hoặc loại vật dụng đổi khỏi SHELF
/// (<see cref="Deleted"/> = true); space cũng phát lại toàn bộ khi khởi động. Ghi đè theo lần phát gần nhất (<c>OccurredAt</c>).
/// </summary>
public sealed record ShelfChanged : IntegrationEvent
{
    public required Guid ShelfPublicId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required Guid FloorPublicId { get; init; }

    /// <summary>Kho (Store.PublicId của holdings) mà giá thuộc về — để lọc giá theo kho khi xếp giá.</summary>
    public Guid? StorePublicId { get; init; }

    /// <summary>Vị trí/kích thước trên sơ đồ tầng, % của khung (0–100).</summary>
    public double? PositionX { get; init; }
    public double? PositionY { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public string? ColorHex { get; init; }
    public IReadOnlyList<ShelfRowState> Rows { get; init; } = [];
    public bool Deleted { get; init; }
}

/// <summary>
/// Tầng của toà nhà — holdings cần tỉ lệ khung, ảnh nền và tên toà/tầng để vẽ sơ đồ giá khi xếp giá trực quan.
/// Phát khi tầng/toà đổi tên hoặc đổi ảnh sơ đồ; space phát lại khi khởi động.
/// </summary>
public sealed record FloorChanged : IntegrationEvent
{
    public required Guid FloorPublicId { get; init; }
    public required string Name { get; init; }
    public int? FloorNumber { get; init; }
    public string? BuildingName { get; init; }
    public double CanvasWidth { get; init; } = 1000;
    public double CanvasHeight { get; init; } = 700;
    public string? LayoutImageUrl { get; init; }
    public bool Deleted { get; init; }
}
