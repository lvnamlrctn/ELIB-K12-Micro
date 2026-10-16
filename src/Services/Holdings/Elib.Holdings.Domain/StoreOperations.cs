using Elib.BuildingBlocks.Domain;

namespace Elib.Holdings.Domain;

/// <summary>Ngày lịch Việt Nam (UTC+7) — mọi lọc "từ ngày/đến ngày" của kho tính theo giờ VN.</summary>
public static class VietnamDate
{
    public static DateOnly Of(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime.AddHours(7));

    /// <summary>0 giờ ngày <paramref name="date"/> giờ VN, biểu diễn ở UTC (Npgsql chỉ nhận timestamptz offset 0).</summary>
    public static DateTimeOffset Start(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
}

internal static class TextRules
{
    public static string? Optional(string? value, int max, string code, string label)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} tối đa {max} ký tự.");
    }
}

/// <summary>Lý do xuất kho (monolith: DExportReason, quyền EXPORT_REASON) — vd Photo, Triển lãm, Đóng gáy.</summary>
public sealed class ExportReason : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Đơn vị nhận sách khi xuất kho (monolith: DBookOutUnit, quyền BOOK_OUT_UNIT).</summary>
public sealed class BookOutUnit : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Địa điểm triển lãm (monolith: DExhibitionLocation, quyền EXHIBITION_LOCATION).</summary>
public sealed class ExhibitionLocation : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>
/// Bản ghi báo mất (monolith: LostBook). Mỗi lần bản sách sang "Mất" có một bản ghi — kể cả mất khi đang mượn (phiếu phạt ở
/// circulation), nên danh sách sách mất không phải gộp thêm ĐKCB "L" không có bản ghi như monolith. Khôi phục = xoá mềm.
/// </summary>
public sealed class LostRecord : TenantEntity
{
    private LostRecord() { }

    public long ItemId { get; private set; }

    /// <summary>Mã lúc báo mất (đăng ký lại mã sau đó không đổi bản ghi).</summary>
    public string Barcode { get; private set; } = "";

    public long? StoreId { get; private set; }
    public DateOnly LostOn { get; private set; }
    public string? Reason { get; private set; }

    /// <summary>Thẻ bạn đọc làm mất (mất khi đang mượn).</summary>
    public string? CardNo { get; private set; }

    public static LostRecord For(Item item, DateOnly lostOn, string? reason, string? cardNo = null) => new()
    {
        ItemId = item.Id, Barcode = item.Barcode, StoreId = item.StoreId, LostOn = lostOn,
        Reason = TextRules.Optional(reason, 500, "REASON_INVALID", "Lý do"), CardNo = cardNo,
    };
}

/// <summary>Bản ghi thanh lý (monolith: Thanhly). Huỷ thanh lý = xoá mềm.</summary>
public sealed class Liquidation : TenantEntity
{
    private Liquidation() { }

    public long ItemId { get; private set; }
    public string Barcode { get; private set; } = "";
    public long? StoreId { get; private set; }
    public DateOnly LiquidatedOn { get; private set; }
    public string? Reason { get; private set; }

    public static Liquidation For(Item item, DateOnly on, string? reason) => new()
    {
        ItemId = item.Id, Barcode = item.Barcode, StoreId = item.StoreId, LiquidatedOn = on,
        Reason = TextRules.Optional(reason, 500, "REASON_INVALID", "Lý do"),
    };
}

/// <summary>
/// Một vòng xuất kho → nhập kho của một bản sách (monolith: BookOutStore — đi photo, triển lãm, cho đơn vị khác mượn…).
/// Khác lượt mượn của bạn đọc (circulation) và điều chuyển kho. Đang mở = chưa có <see cref="InAt"/>.
/// </summary>
public sealed class StoreOut : TenantEntity
{
    private StoreOut() { }

    public long ItemId { get; private set; }
    public string Barcode { get; private set; } = "";
    public long? StoreId { get; private set; }
    public long? ReasonId { get; private set; }
    public long? UnitId { get; private set; }
    public long? ExhibitionLocationId { get; private set; }
    public string? DelivererName { get; private set; }
    public string? ReceiverName { get; private set; }

    /// <summary>Đơn vị nhận trả sách tại thư viện (monolith: ReturnBarcodeAtLibrary).</summary>
    public bool ReturnAtLibrary { get; private set; }

    /// <summary>null: bản "X" của dữ liệu cũ không có lượt xuất — lượt chỉ có ngày nhập.</summary>
    public DateTimeOffset? OutAt { get; private set; }

    public DateTimeOffset? InAt { get; private set; }

    public bool IsOpen => InAt is null;

    public static StoreOut Open(Item item, DateTimeOffset at, long? reasonId, long? unitId, long? locationId, string? deliverer, string? receiver,
        bool returnAtLibrary) => new()
    {
        ItemId = item.Id, Barcode = item.Barcode, StoreId = item.StoreId, ReasonId = reasonId, UnitId = unitId, ExhibitionLocationId = locationId,
        DelivererName = TextRules.Optional(deliverer, 250, "NAME_INVALID", "Người giao"),
        ReceiverName = TextRules.Optional(receiver, 250, "NAME_INVALID", "Người nhận"),
        ReturnAtLibrary = returnAtLibrary, OutAt = at,
    };

    /// <summary>Bản đang "X" mà không có lượt xuất mở (dữ liệu cũ) — ghi một lượt chỉ có ngày nhập để có lịch sử.</summary>
    public static StoreOut ReturnOnly(Item item) => new() { ItemId = item.Id, Barcode = item.Barcode, StoreId = item.StoreId };

    public void Close(DateTimeOffset at) => InAt = at;
}

/// <summary>
/// Đợt kiểm kê (monolith: Inventory; bảng KiemKe cũ gộp vào đây). <see cref="StoreId"/>: kho được kiểm — có thì "nghi mất" là bản
/// của kho đó chưa quét; trống thì như monolith: bản thuộc các kho đã có mã quét trong đợt. Đã kết thúc thì không nhận thêm mã.
/// </summary>
public sealed class Inventory : TenantEntity
{
    private Inventory() { }

    public string Name { get; private set; } = "";
    public DateOnly StartedOn { get; private set; }
    public long? StoreId { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsClosed => ClosedAt is not null;

    public static Inventory Create(string name, DateOnly startedOn, long? storeId, string? note)
    {
        var inventory = new Inventory();
        inventory.Update(name, startedOn, storeId, note, hasScans: false);
        return inventory;
    }

    public void Update(string name, DateOnly startedOn, long? storeId, string? note, bool hasScans)
    {
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 250 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên đợt kiểm kê bắt buộc, tối đa 250 ký tự.");
        StartedOn = startedOn;
        if (hasScans && storeId != StoreId)
            throw new BusinessRuleException("INVENTORY_HAS_SCANS", "Đợt kiểm kê đã có mã quét — không đổi được kho kiểm kê.");
        StoreId = storeId;
        Note = TextRules.Optional(note, 500, "NOTE_INVALID", "Ghi chú");
    }

    public void Close(DateTimeOffset at)
    {
        if (IsClosed) throw new BusinessRuleException("INVENTORY_CLOSED", $"Đợt kiểm kê '{Name}' đã kết thúc.");
        ClosedAt = at;
    }

    public void Reopen() => ClosedAt = null;

    public void EnsureOpen()
    {
        if (IsClosed) throw new BusinessRuleException("INVENTORY_CLOSED", $"Đợt kiểm kê '{Name}' đã kết thúc, không nhận thêm mã.");
    }
}

/// <summary>
/// Một mã quét trong đợt kiểm kê (monolith: InventoryBarcode, cờ 1/2 thay bằng bool). Mã không có trong CSDL vẫn ghi nhận
/// (<see cref="Registered"/> = false). Cờ chụp lúc quét.
/// </summary>
public sealed class InventoryScan : TenantEntity
{
    private InventoryScan() { }

    public long InventoryId { get; private set; }
    public string Barcode { get; private set; } = "";
    public string BarcodeKey { get; private set; } = "";
    public long? ItemId { get; private set; }

    /// <summary>Kho của bản sách lúc quét.</summary>
    public long? StoreId { get; private set; }

    /// <summary>Kho cán bộ đang kiểm khi quét (chọn trên màn hình hoặc kho của đợt) — để đối chiếu sai kho.</summary>
    public long? ScanStoreId { get; private set; }

    public bool Registered { get; private set; }
    public bool WrongStore { get; private set; }

    /// <summary>Còn lượt mượn mở mà sách lại nằm trên giá.</summary>
    public bool OnLoan { get; private set; }

    /// <summary>Trạng thái bản sách lúc quét — khác thường khi là Mất/Thanh lý/Xuất kho mà sách vẫn ở trên giá.</summary>
    public string? ItemStatus { get; private set; }

    public bool Abnormal => ItemStatus is Domain.ItemStatus.Lost or Domain.ItemStatus.Liquidated or Domain.ItemStatus.OutOfStore;

    public static InventoryScan Of(long inventoryId, string barcode, Item? item, long? scanStoreId) => new()
    {
        InventoryId = inventoryId, Barcode = item?.Barcode ?? barcode, BarcodeKey = Item.Key(item?.Barcode ?? barcode), ItemId = item?.Id,
        StoreId = item?.StoreId, ScanStoreId = scanStoreId, Registered = item is not null,
        // Không chọn kho khi quét thì không đối chiếu kho; mã chưa đăng ký cũng vậy.
        WrongStore = item is not null && scanStoreId is not null && item.StoreId != scanStoreId,
        OnLoan = item?.OnLoan ?? false, ItemStatus = item?.Status,
    };
}

/// <summary>
/// Phiếu điều chuyển kho (monolith: ab_move + ab_move_detail). Thêm bản sách của kho nguồn; "Hoàn thành" chuyển các bản sang kho
/// nhận rồi khoá phiếu (không sửa/xoá/thêm bớt dòng).
/// </summary>
public sealed class StoreMove : TenantEntity
{
    private StoreMove() { }

    public long FromStoreId { get; private set; }
    public long ToStoreId { get; private set; }
    public string? DelivererName { get; private set; }
    public string? ReceiverName { get; private set; }
    public DateOnly? MovedOn { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int MovedCount { get; private set; }

    public bool IsCompleted => CompletedAt is not null;

    public static StoreMove Create(long fromStoreId, long toStoreId, string? deliverer, string? receiver, DateOnly? movedOn, string? note)
    {
        var move = new StoreMove();
        move.Update(fromStoreId, toStoreId, deliverer, receiver, movedOn, note, hasLines: false);
        return move;
    }

    public void Update(long fromStoreId, long toStoreId, string? deliverer, string? receiver, DateOnly? movedOn, string? note, bool hasLines)
    {
        EnsureEditable();
        if (fromStoreId == toStoreId) throw new BusinessRuleException("MOVE_SAME_STORE", "Kho nhận phải khác kho nguồn.");
        if (hasLines && fromStoreId != FromStoreId)
            throw new BusinessRuleException("MOVE_HAS_LINES", "Phiếu đã có tài liệu, không đổi được kho nguồn — xoá các dòng trước.");
        (FromStoreId, ToStoreId, MovedOn) = (fromStoreId, toStoreId, movedOn);
        DelivererName = TextRules.Optional(deliverer, 250, "NAME_INVALID", "Người giao");
        ReceiverName = TextRules.Optional(receiver, 250, "NAME_INVALID", "Người nhận");
        Note = TextRules.Optional(note, 500, "NOTE_INVALID", "Ghi chú");
    }

    public void Complete(DateTimeOffset at, int moved)
    {
        EnsureEditable();
        CompletedAt = at;
        MovedCount = moved;
        MovedOn ??= VietnamDate.Of(at);
    }

    public void EnsureEditable()
    {
        if (IsCompleted) throw new BusinessRuleException("MOVE_COMPLETED", "Phiếu điều chuyển đã hoàn thành, không sửa được.");
    }
}

public sealed class StoreMoveLine : TenantEntity
{
    private StoreMoveLine() { }

    public long MoveId { get; private set; }
    public long ItemId { get; private set; }

    /// <summary>Kết quả khi hoàn thành: null = đã chuyển, khác null = lý do bỏ qua.</summary>
    public string? SkipReason { get; private set; }

    public static StoreMoveLine Of(long moveId, long itemId) => new() { MoveId = moveId, ItemId = itemId };

    public void Skip(string reason) => SkipReason = reason.Length <= 250 ? reason : reason[..250];
}

/// <summary>Tình trạng phiếu giao nhận (monolith: bảng ab_deliverer_status theo đơn vị — nay cố định trong code).</summary>
public static class DeliveryStatus
{
    public const int Delivering = 1;
    public const int Received = 2;

    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [Delivering] = "Đang giao",
        [Received] = "Đã nhận đủ",
    };

    public static int Validate(int status) => Names.ContainsKey(status)
        ? status
        : throw new BusinessRuleException("DELIVERY_STATUS_INVALID", $"Tình trạng phiếu giao nhận '{status}' không hợp lệ.");
}

/// <summary>
/// Phiếu giao nhận sách mới về kho (monolith: ab_deliverer + ab_deliverer_detail, quyền AB_DELIVERERS): bộ phận bổ sung/biên mục
/// giao các bản chưa xếp giá cho kho. Ký nhận (khi đã nhận đủ) → các bản "chưa xếp giá" sang sẵn sàng phục vụ ở kho nhận, phiếu khoá.
/// Mỗi bản sách chỉ nằm ở một phiếu giao nhận.
/// </summary>
public sealed class Delivery : TenantEntity
{
    private Delivery() { }

    public string? DelivererName { get; private set; }
    public string? DelivererAddress { get; private set; }
    public string? ReceiverName { get; private set; }
    public string? ReceiverAddress { get; private set; }
    public DateOnly? DeliveredOn { get; private set; }
    public DateOnly? ReceivedOn { get; private set; }

    /// <summary>Kho nhận — ký nhận thì bản sách vào kho này.</summary>
    public long? StoreId { get; private set; }

    public int Status { get; private set; } = DeliveryStatus.Delivering;
    public string? Note { get; private set; }
    public DateTimeOffset? SignedAt { get; private set; }
    public int ShelvedCount { get; private set; }

    public bool IsSigned => SignedAt is not null;

    /// <summary>Đơn nhận (phiếu nhận của phân hệ bổ sung) mà phiếu giao nhận phân bổ sách — màn chọn sách liệt kê bản của đơn này.</summary>
    public Guid? ReceiptPublicId { get; private set; }

    public static Delivery Create(string? deliverer, string? delivererAddress, string? receiver, string? receiverAddress, DateOnly? deliveredOn,
        DateOnly? receivedOn, long? storeId, int status, string? note, Guid? receiptPublicId = null)
    {
        var delivery = new Delivery();
        delivery.Update(deliverer, delivererAddress, receiver, receiverAddress, deliveredOn, receivedOn, storeId, status, note, receiptPublicId);
        return delivery;
    }

    public void Update(string? deliverer, string? delivererAddress, string? receiver, string? receiverAddress, DateOnly? deliveredOn,
        DateOnly? receivedOn, long? storeId, int status, string? note, Guid? receiptPublicId = null)
    {
        EnsureEditable();
        ReceiptPublicId = receiptPublicId;
        DelivererName = TextRules.Optional(deliverer, 250, "NAME_INVALID", "Người giao");
        DelivererAddress = TextRules.Optional(delivererAddress, 500, "ADDRESS_INVALID", "Địa chỉ người giao");
        ReceiverName = TextRules.Optional(receiver, 250, "NAME_INVALID", "Người nhận");
        ReceiverAddress = TextRules.Optional(receiverAddress, 500, "ADDRESS_INVALID", "Địa chỉ người nhận");
        if (deliveredOn is { } d && receivedOn is { } r && r < d)
            throw new BusinessRuleException("DATE_RANGE_INVALID", "Ngày nhận không được trước ngày giao.");
        (DeliveredOn, ReceivedOn, StoreId) = (deliveredOn, receivedOn, storeId);
        Status = DeliveryStatus.Validate(status);
        Note = TextRules.Optional(note, 500, "NOTE_INVALID", "Ghi chú");
    }

    public void Sign(DateTimeOffset at, int shelved)
    {
        EnsureEditable();
        if (Status != DeliveryStatus.Received)
            throw new BusinessRuleException("DELIVERY_NOT_RECEIVED", "Phiếu chưa ở tình trạng \"Đã nhận đủ\" — chưa ký nhận được.");
        SignedAt = at;
        ShelvedCount = shelved;
        ReceivedOn ??= VietnamDate.Of(at);
    }

    public void EnsureEditable()
    {
        if (IsSigned) throw new BusinessRuleException("DELIVERY_SIGNED", "Phiếu giao nhận đã ký nhận, không sửa được.");
    }
}

public sealed class DeliveryLine : TenantEntity
{
    private DeliveryLine() { }

    public long DeliveryId { get; private set; }
    public long ItemId { get; private set; }

    public static DeliveryLine Of(long deliveryId, long itemId) => new() { DeliveryId = deliveryId, ItemId = itemId };
}
