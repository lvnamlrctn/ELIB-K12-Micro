using Elib.BuildingBlocks.Domain;

namespace Elib.Holdings.Domain;

/// <summary>
/// Bản sao giá sách từ space (event ShelfChanged) — xếp giá trực quan chọn giá/ngăn cho bản sách, gợi ý theo DDC. Ghi đè theo lần phát
/// gần nhất (<see cref="SourceAt"/>). Giá bị xoá ở space (hoặc đổi loại) chỉ đánh dấu <see cref="Deleted"/> — bản sách vẫn giữ mã giá, hiển thị "giá đã xoá".
/// </summary>
public sealed class ShelfReplica : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid ShelfPublicId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid FloorPublicId { get; set; }

    /// <summary>Kho (Store.PublicId) mà giá thuộc về; null = giá chưa gắn kho.</summary>
    public Guid? StorePublicId { get; set; }

    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public string? ColorHex { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset SourceAt { get; set; }
}

/// <summary>Ngăn của giá theo khoảng DDC (bản sao map.MapShelfRow). Thay toàn bộ ngăn của giá mỗi lần nhận <c>ShelfChanged</c>.</summary>
public sealed class ShelfRowReplica : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid ShelfPublicId { get; set; }
    public Guid RowPublicId { get; set; }
    public int RowIndex { get; set; }
    public string DdcStart { get; set; } = "";
    public string DdcEnd { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>
    /// Bản sách có DDC trong ngăn? DDC &gt;= <see cref="DdcStart"/> và (&lt;= <see cref="DdcEnd"/> hoặc bắt đầu bằng <see cref="DdcEnd"/>) — "339.5" thuộc khoảng 330–339
    /// (so sánh ordinal sau khi cắt khoảng trắng, như ELIB-LRC <c>DdcInRange</c>). DDC trống → không thuộc ngăn nào.
    /// </summary>
    public static bool Contains(string? ddc, string start, string end)
    {
        var d = ddc?.Trim();
        if (string.IsNullOrEmpty(d) || string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end)) return false;
        return string.CompareOrdinal(d, start) >= 0 && (string.CompareOrdinal(d, end) <= 0 || d.StartsWith(end, StringComparison.Ordinal));
    }
}

/// <summary>Bản sao tầng từ space (event FloorChanged): tỉ lệ khung, ảnh nền, tên toà/tầng để vẽ sơ đồ giá.</summary>
public sealed class FloorReplica : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid FloorPublicId { get; set; }
    public string Name { get; set; } = "";
    public int? FloorNumber { get; set; }
    public string? BuildingName { get; set; }
    public double CanvasWidth { get; set; } = 1000;
    public double CanvasHeight { get; set; } = 700;
    public string? LayoutImageUrl { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset SourceAt { get; set; }
}

/// <summary>Vị trí hiện tại của một bản sách: kho, giá và ngăn (giá/ngăn null = chưa xếp vào giá nào).</summary>
public readonly record struct ItemLocation(long? StoreId, Guid? ShelfPublicId, Guid? ShelfRowPublicId);

public static class MovementKind
{
    public const string Shelve = "SHELVE";
    public const string Place = "PLACE";
    public const string Reshelve = "RESHELVE";
    public const string Move = "MOVE";
    public const string ReRegister = "REREGISTER";

    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [Shelve] = "Xếp giá", [Place] = "Xếp vào giá/ngăn", [Reshelve] = "Xếp giá lại", [Move] = "Chuyển kho", [ReRegister] = "Đăng ký lại mã vạch",
    };
}

/// <summary>
/// Lịch sử di chuyển của từng bản sách (xếp giá, xếp vào giá/ngăn, xếp giá lại, chuyển kho, đăng ký lại): từ đâu → đến đâu, ai làm, lúc nào.
/// Chỉ thêm, không sửa. Ghi cùng transaction với thay đổi bản sách.
/// </summary>
public sealed class ItemMovement : Entity, ITenantOwned
{
    public const int MaxReasonLength = 500;

    public long TenantId { get; set; }
    public long ItemId { get; set; }
    public string Barcode { get; set; } = "";
    public string Kind { get; set; } = MovementKind.Shelve;
    public long? FromStoreId { get; set; }
    public long? ToStoreId { get; set; }
    public Guid? FromShelfPublicId { get; set; }
    public Guid? ToShelfPublicId { get; set; }
    public Guid? FromRowPublicId { get; set; }
    public Guid? ToRowPublicId { get; set; }

    /// <summary>Trạng thái trước → sau (mã I/R/…): xếp giá lần đầu là I → R.</summary>
    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }
    public string? Reason { get; set; }
    public long? ActorId { get; set; }

    /// <summary>"Họ tên (tên đăng nhập)" của người thao tác lúc đó — holdings không giữ bản sao người dùng.</summary>
    public string? ActorName { get; set; }

    public DateTimeOffset At { get; set; }

    public static ItemMovement Between(Item item, string kind, ItemLocation from, string fromStatus, long? actorId, string? actorName, DateTimeOffset at, string? reason)
    {
        ArgumentNullException.ThrowIfNull(item);
        var to = item.Location;
        var r = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return new ItemMovement
        {
            ItemId = item.Id, Barcode = item.Barcode, Kind = kind, FromStoreId = from.StoreId, ToStoreId = to.StoreId,
            FromShelfPublicId = from.ShelfPublicId, ToShelfPublicId = to.ShelfPublicId, FromRowPublicId = from.ShelfRowPublicId, ToRowPublicId = to.ShelfRowPublicId,
            FromStatus = fromStatus, ToStatus = item.Status, Reason = r is null || r.Length <= MaxReasonLength ? r : r[..MaxReasonLength],
            ActorId = actorId, ActorName = actorName is { Length: > 250 } ? actorName[..250] : actorName, At = at,
        };
    }
}
