using Elib.BuildingBlocks.Domain;

namespace Elib.Acquisition.Domain;

/// <summary>Tình trạng đơn đặt (monolith: bảng Order_Status theo đơn vị — nay cố định trong code).</summary>
public static class OrderStatus
{
    public const int Draft = 1;
    public const int Sent = 2;
    public const int Cancelled = 3;

    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [Draft] = "Mới lập",
        [Sent] = "Đã gửi đặt",
        [Cancelled] = "Đã huỷ",
    };

    public static int Validate(int status) => Names.ContainsKey(status)
        ? status
        : throw new BusinessRuleException("ORDER_STATUS_INVALID", $"Tình trạng đơn đặt '{status}' không hợp lệ.");
}

/// <summary>Trạng thái thanh toán của phiếu nhận (monolith: Payment_Status — 0 chưa thanh toán, 2 đã thanh toán).</summary>
public static class PaymentStatus
{
    public const int Unpaid = 0;
    public const int Paid = 2;

    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [Unpaid] = "Chưa thanh toán",
        [Paid] = "Đã thanh toán",
    };

    public static int Validate(int status) => Names.ContainsKey(status)
        ? status
        : throw new BusinessRuleException("PAYMENT_STATUS_INVALID", $"Trạng thái thanh toán '{status}' không hợp lệ.");
}

/// <summary>Phương thức thanh toán của đơn đặt / phiếu nhận (monolith: PaymentMethodId — 0 tiền mặt, 1 chuyển khoản).</summary>
public static class PaymentMethod
{
    public const int Cash = 0;
    public const int Transfer = 1;

    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [Cash] = "Tiền mặt",
        [Transfer] = "Chuyển khoản",
    };

    public static int Validate(int method) => Names.ContainsKey(method)
        ? method
        : throw new BusinessRuleException("PAYMENT_METHOD_INVALID", $"Phương thức thanh toán '{method}' không hợp lệ.");
}

/// <summary>Nguồn/nhà cung cấp/kinh phí/quỹ chung của đầu đơn đặt và phiếu nhận.</summary>
public sealed record AcquisitionRefs(long? SourceId = null, long? SupplierId = null, long? BudgetId = null, long? FundId = null);

/// <summary>
/// Đơn đặt bổ sung (monolith: ab_order + ab_order_detail, quyền AB_ORDERS). Mã đơn = Id. Đơn đã huỷ không thêm/sửa/bỏ dòng;
/// số đã nhận của từng dòng tính từ các dòng phiếu nhận "từ đơn đặt".
/// </summary>
public sealed class PurchaseOrder : TenantEntity
{
    private PurchaseOrder() { }

    public string Name { get; private set; } = "";
    public DateOnly? OrderedOn { get; private set; }

    /// <summary>Hạn giao (monolith: Duedate).</summary>
    public DateOnly? DueOn { get; private set; }

    public long? SourceId { get; private set; }
    public long? SupplierId { get; private set; }
    public long? BudgetId { get; private set; }
    public long? FundId { get; private set; }
    public string? Note { get; private set; }
    public int Status { get; private set; } = OrderStatus.Draft;
    public int PaymentMethod { get; private set; } = Domain.PaymentMethod.Cash;

    public static PurchaseOrder Create(string name, DateOnly? orderedOn, DateOnly? dueOn, AcquisitionRefs refs, string? note, int status, int paymentMethod)
    {
        var order = new PurchaseOrder();
        order.Update(name, orderedOn, dueOn, refs, note, status, paymentMethod);
        return order;
    }

    public void Update(string name, DateOnly? orderedOn, DateOnly? dueOn, AcquisitionRefs refs, string? note, int status, int paymentMethod)
    {
        ArgumentNullException.ThrowIfNull(refs);
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên đơn đặt");
        if (orderedOn is { } o && dueOn is { } d && d < o)
            throw new BusinessRuleException("DATE_RANGE_INVALID", "Hạn giao không được trước ngày đặt.");
        (OrderedOn, DueOn) = (orderedOn, dueOn);
        (SourceId, SupplierId, BudgetId, FundId) = (refs.SourceId, refs.SupplierId, refs.BudgetId, refs.FundId);
        Note = TextRules.Optional(note, 500, "NOTE_INVALID", "Ghi chú");
        Status = OrderStatus.Validate(status);
        PaymentMethod = Domain.PaymentMethod.Validate(paymentMethod);
    }

    public void EnsureLinesEditable()
    {
        if (Status == OrderStatus.Cancelled)
            throw new BusinessRuleException("ORDER_CANCELLED", $"Đơn đặt số {Id} đã huỷ — không thêm/sửa dòng được.");
    }
}

/// <summary>Mô tả tài liệu của dòng đơn đặt — có thể chưa có biểu ghi trong catalog (sách chưa về, chưa biên mục).</summary>
public sealed record OrderItemData(
    string Title, string? Author = null, string? Publisher = null, string? PublishYear = null, string? Isbn = null);

/// <summary>
/// Dòng đơn đặt. Monolith tạo biểu ghi MARC "nháp" (BibOrder) rồi "thăng cấp" thành biểu ghi thật khi nhận; ở đây dòng giữ mô tả
/// thư mục (nhan đề, tác giả, NXB, năm, ISBN) và tuỳ chọn trỏ biểu ghi đã có ở catalog (<see cref="Mfn"/>) — khi nhận sách, dòng
/// phiếu nhận luôn trỏ biểu ghi thật.
/// </summary>
public sealed class OrderLine : TenantEntity
{
    public const int MaxQuantity = 100_000;

    private OrderLine() { }

    public long OrderId { get; private set; }
    public Guid? BibPublicId { get; private set; }
    public long? Mfn { get; private set; }
    public string Title { get; private set; } = "";
    public string? Author { get; private set; }
    public string? Publisher { get; private set; }
    public string? PublishYear { get; private set; }
    public string? Isbn { get; private set; }
    public int Quantity { get; private set; }
    public decimal? Price { get; private set; }
    public string? Currency { get; private set; }

    /// <summary>Tỷ giá của <see cref="Currency"/> lúc đặt (monolith: Rate) — thành tiền = số lượng × đơn giá × tỷ giá.</summary>
    public decimal Rate { get; private set; } = 1m;

    /// <summary>Lý do huỷ dòng (monolith: Cancel_Reson) — dòng có lý do huỷ không nhận thêm.</summary>
    public string? CancelReason { get; private set; }

    public static OrderLine Create(long orderId, BibSnapshot? bib, OrderItemData item, int quantity, decimal? price, string? currency, decimal? rate,
        string? cancelReason)
    {
        var line = new OrderLine { OrderId = orderId };
        line.Update(bib, item, quantity, price, currency, rate, cancelReason);
        return line;
    }

    public void Update(BibSnapshot? bib, OrderItemData item, int quantity, decimal? price, string? currency, decimal? rate, string? cancelReason)
    {
        ArgumentNullException.ThrowIfNull(item);
        (BibPublicId, Mfn) = (bib?.BibPublicId, bib?.Mfn);
        Title = TextRules.Required(bib?.Title ?? item.Title, 1000, "TITLE_INVALID", "Nhan đề");
        Author = TextRules.Optional(bib?.Author ?? item.Author, 500, "AUTHOR_INVALID", "Tác giả");
        Publisher = TextRules.Optional(bib?.Publisher ?? item.Publisher, 500, "PUBLISHER_INVALID", "Nhà xuất bản");
        PublishYear = TextRules.Optional(bib?.PublishYear ?? item.PublishYear, 4, "YEAR_INVALID", "Năm xuất bản");
        Isbn = TextRules.Optional(item.Isbn ?? bib?.Isbns, 100, "ISBN_INVALID", "ISBN");
        Quantity = quantity is >= 1 and <= MaxQuantity
            ? quantity
            : throw new BusinessRuleException("QUANTITY_INVALID", $"Số lượng đặt từ 1 đến {MaxQuantity:N0}.");
        Price = TextRules.Money(price, "Đơn giá");
        Currency = TextRules.Optional(currency, 10, "CURRENCY_INVALID", "Tiền tệ")?.ToUpperInvariant();
        Rate = TextRules.Rate(rate);
        CancelReason = TextRules.Optional(cancelReason, 500, "REASON_INVALID", "Lý do huỷ");
    }
}

/// <summary>
/// Phiếu nhận / đơn nhận (monolith: ab_receipt + ab_receipt_detail, quyền AB_RECEIPTS). Mã phiếu = Id. Mỗi dòng một biểu ghi với số
/// bản nhận — số ĐKCB đăng ký cho dòng không vượt số bản này (holdings khoá khi đăng ký). "Hoàn tất" khoá phiếu: không sửa, không
/// thêm/bỏ dòng, không đăng ký/xoá ĐKCB; giao nhận chỉ chọn được đơn đã hoàn tất. <see cref="Version"/> tăng ở mọi thay đổi của
/// phiếu và dòng — holdings bỏ qua trạng thái cũ hơn.
/// </summary>
public sealed class Receipt : TenantEntity
{
    private Receipt() { }

    public string? Name { get; private set; }

    /// <summary>Ngày nhận — báo cáo kho lọc theo ngày này.</summary>
    public DateOnly ReceivedOn { get; private set; }

    public long? SourceId { get; private set; }
    public long? SupplierId { get; private set; }
    public long? BudgetId { get; private set; }
    public long? FundId { get; private set; }

    /// <summary>Kho nhận (id kho của holdings) — mặc định cho ĐKCB đăng ký theo phiếu.</summary>
    public long? StoreId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Trạng thái thanh toán (<see cref="Domain.PaymentStatus"/>) — không ảnh hưởng khoá phiếu; đổi được cả khi phiếu đã hoàn tất (<see cref="SetPayment"/>).</summary>
    public int PaymentStatus { get; private set; } = Domain.PaymentStatus.Unpaid;

    public int PaymentMethod { get; private set; } = Domain.PaymentMethod.Cash;

    public DateTimeOffset? CompletedAt { get; private set; }
    public long Version { get; private set; }

    public bool IsCompleted => CompletedAt is not null;

    public static Receipt Create(string? name, DateOnly receivedOn, AcquisitionRefs refs, long? storeId, string? note, int paymentStatus, int paymentMethod)
    {
        var receipt = new Receipt();
        receipt.Update(name, receivedOn, refs, storeId, note, paymentStatus, paymentMethod);
        return receipt;
    }

    public void Update(string? name, DateOnly receivedOn, AcquisitionRefs refs, long? storeId, string? note, int paymentStatus, int paymentMethod)
    {
        ArgumentNullException.ThrowIfNull(refs);
        EnsureEditable();
        Name = TextRules.Optional(name, 250, "NAME_INVALID", "Tên phiếu nhận");
        ReceivedOn = receivedOn;
        (SourceId, SupplierId, BudgetId, FundId) = (refs.SourceId, refs.SupplierId, refs.BudgetId, refs.FundId);
        StoreId = storeId;
        Note = TextRules.Optional(note, 500, "NOTE_INVALID", "Ghi chú");
        PaymentStatus = Domain.PaymentStatus.Validate(paymentStatus);
        PaymentMethod = Domain.PaymentMethod.Validate(paymentMethod);
        Touch();
    }

    /// <summary>
    /// Đổi thanh toán — cho phép cả khi phiếu đã hoàn tất: thanh toán thường chốt sau khi nhận đủ sách và đối chiếu hoá đơn. Không đụng dòng / ĐKCB nên
    /// không phá khoá phiếu; holdings không dùng thanh toán nên không tăng Version, không phát ReceiptChanged.
    /// </summary>
    public void SetPayment(int paymentStatus, int paymentMethod)
    {
        PaymentStatus = Domain.PaymentStatus.Validate(paymentStatus);
        PaymentMethod = Domain.PaymentMethod.Validate(paymentMethod);
    }

    /// <summary>Phiếu hoặc dòng của phiếu đổi — tăng Version cho event ReceiptChanged.</summary>
    public void Touch() => Version++;

    public void Complete(DateTimeOffset at)
    {
        EnsureEditable();
        CompletedAt = at;
        Touch();
    }

    public void Reopen()
    {
        if (!IsCompleted) throw new BusinessRuleException("RECEIPT_NOT_COMPLETED", $"Phiếu nhận số {Id} chưa hoàn tất.");
        CompletedAt = null;
        Touch();
    }

    public void EnsureEditable()
    {
        if (IsCompleted) throw new BusinessRuleException("RECEIPT_COMPLETED", $"Phiếu nhận số {Id} đã hoàn tất — không chỉnh sửa được.");
    }
}

/// <summary>Dòng phiếu nhận: biểu ghi thật ở catalog, số bản nhận, đơn giá; <see cref="OrderLineId"/> khi nhận "từ đơn đặt".</summary>
public sealed class ReceiptLine : TenantEntity
{
    public const int MaxQuantity = 10_000;

    private ReceiptLine() { }

    public long ReceiptId { get; private set; }
    public Guid BibPublicId { get; private set; }
    public long Mfn { get; private set; }
    public string Title { get; private set; } = "";
    public string? Author { get; private set; }
    public int Quantity { get; private set; }
    public decimal? Price { get; private set; }
    public string? Currency { get; private set; }

    /// <summary>Tỷ giá của <see cref="Currency"/> lúc nhận (monolith: Rate) — thành tiền = số bản × đơn giá × tỷ giá.</summary>
    public decimal Rate { get; private set; } = 1m;

    public long? OrderLineId { get; private set; }

    public static ReceiptLine Create(long receiptId, BibSnapshot bib, int quantity, decimal? price, string? currency, decimal? rate, long? orderLineId)
    {
        ArgumentNullException.ThrowIfNull(bib);
        var line = new ReceiptLine
        {
            ReceiptId = receiptId, BibPublicId = bib.BibPublicId, Mfn = bib.Mfn, OrderLineId = orderLineId,
        };
        line.Update(bib, quantity, price, currency, rate);
        return line;
    }

    /// <summary>Chọn lại biểu ghi đã có trong phiếu "từ đơn đặt": dòng chưa gắn đơn đặt thì gắn; gắn dòng đặt khác thì từ chối.</summary>
    public void AttachOrderLine(long orderLineId)
    {
        if (OrderLineId is { } current && current != orderLineId)
            throw new ConflictException("LINE_OTHER_ORDER", $"Biểu ghi MFN {Mfn} đã có trong phiếu theo một dòng đơn đặt khác — sửa ở dòng đó.");
        OrderLineId = orderLineId;
    }

    public void Update(BibSnapshot? bib, int quantity, decimal? price, string? currency, decimal? rate)
    {
        if (bib is not null)
        {
            Title = bib.Title.Length <= 1000 ? bib.Title : bib.Title[..1000];
            Author = bib.Author is { Length: > 500 } a ? a[..500] : bib.Author;
        }
        Quantity = quantity is >= 1 and <= MaxQuantity
            ? quantity
            : throw new BusinessRuleException("QUANTITY_INVALID", $"Số bản nhận từ 1 đến {MaxQuantity:N0}.");
        Price = TextRules.Money(price, "Đơn giá");
        Currency = TextRules.Optional(currency, 10, "CURRENCY_INVALID", "Tiền tệ")?.ToUpperInvariant();
        Rate = TextRules.Rate(rate);
    }
}
