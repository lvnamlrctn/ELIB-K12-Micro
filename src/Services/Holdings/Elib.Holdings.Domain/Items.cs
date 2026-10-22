using System.Globalization;
using Elib.BuildingBlocks.Domain;

namespace Elib.Holdings.Domain;

/// <summary>Trạng thái vật lý của bản sách — giữ mã một ký tự của monolith (cột Barcode.Status). "Đang mượn" do circulation sở hữu.</summary>
public static class ItemStatus
{
    /// <summary>Mới đăng ký, còn trong kho chưa xếp giá — chưa cho mượn.</summary>
    public const string Unshelved = "I";

    /// <summary>Đã xếp giá, sẵn sàng phục vụ.</summary>
    public const string Available = "R";

    public const string Lost = "L";
    public const string Liquidated = "S";
    public const string OutOfStore = "X";

    /// <summary>Mã lọc "đang mượn" (monolith: Barcode.Status = "B") — không phải trạng thái lưu, suy từ lượt mượn của circulation.</summary>
    public const string OnLoan = "B";

    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Unshelved] = "Chưa xếp giá",
        [Available] = "Sẵn sàng",
        [Lost] = "Mất",
        [Liquidated] = "Đã thanh lý",
        [OutOfStore] = "Xuất kho",
    };

    public static string Validate(string? status) => status is not null && Names.ContainsKey(status)
        ? status
        : throw new BusinessRuleException("ITEM_STATUS_INVALID", $"Trạng thái bản sách '{status}' không hợp lệ.");
}

/// <summary>
/// Bản sách — số đăng ký cá biệt (monolith: PrintBook.Barcode). Mã ĐKCB duy nhất trong đơn vị, không phân biệt hoa/thường
/// (<see cref="BarcodeKey"/>). <see cref="Prefix"/>/<see cref="Number"/> tách từ mã để đánh số tiếp theo lô.
/// <see cref="Version"/> tăng ở mỗi thay đổi — bản sao ở circulation/search bỏ qua event cũ hơn.
/// </summary>
public sealed class Item : TenantEntity
{
    public const int MaxBarcodeLength = 50;

    private Item() { }

    public string Barcode { get; private set; } = "";

    /// <summary>Mã viết hoa — khoá duy nhất trong đơn vị.</summary>
    public string BarcodeKey { get; private set; } = "";

    public string Prefix { get; private set; } = "";
    public long? Number { get; private set; }

    /// <summary>MFN của biểu ghi (monolith: Barcode.BibId).</summary>
    public long BibId { get; private set; }

    public Guid BibPublicId { get; private set; }
    public long? StoreId { get; private set; }

    /// <summary>Giá sách (ShelfReplica) và ngăn theo DDC mà bản sách đang nằm — null = chưa xếp vào giá/ngăn nào (monolith: Barcode.MapObjectId/MapShelfRowId).</summary>
    public Guid? ShelfPublicId { get; private set; }

    public Guid? ShelfRowPublicId { get; private set; }
    public DateTimeOffset? ShelvedAt { get; private set; }

    public ItemLocation Location => new(StoreId, ShelfPublicId, ShelfRowPublicId);
    public string Status { get; private set; } = ItemStatus.Unshelved;
    public string? Note { get; private set; }
    public long Version { get; private set; }

    /// <summary>Phiếu nhận bổ sung đã đăng ký bản này (monolith: Barcode.Receipt_Id). Null = đăng ký thẳng ở biên mục / dữ liệu cũ.</summary>
    public Guid? ReceiptPublicId { get; private set; }

    /// <summary>Dòng phiếu nhận — số ĐKCB của một dòng không vượt số bản nhận của dòng đó.</summary>
    public Guid? ReceiptLinePublicId { get; private set; }

    /// <summary>Lượt mượn gần nhất (circulation sở hữu, nhận qua LoanChanged) — chỉ để hiển thị "đang mượn", không tăng Version.</summary>
    public Guid? LoanPublicId { get; private set; }

    public long LoanVersion { get; private set; }
    public DateTimeOffset? LoanedAt { get; private set; }
    public DateTimeOffset? LoanDueAt { get; private set; }
    public string? LoanCardNo { get; private set; }
    public bool OnLoan { get; private set; }

    public static Item Register(BibSnapshot bib, string barcode, long? storeId)
    {
        ArgumentNullException.ThrowIfNull(bib);
        var value = NormalizeBarcode(barcode);
        var (prefix, number) = Split(value);
        return new Item
        {
            Barcode = value, BarcodeKey = Key(value), Prefix = prefix, Number = number,
            BibId = bib.Mfn, BibPublicId = bib.BibPublicId, StoreId = storeId,
        };
    }

    /// <summary>Gắn bản mới đăng ký với dòng phiếu nhận (đăng ký cá biệt từ màn phiếu nhận của phân hệ bổ sung).</summary>
    public void AttachReceipt(Guid receiptPublicId, Guid linePublicId) => (ReceiptPublicId, ReceiptLinePublicId) = (receiptPublicId, linePublicId);

    public void Update(long? storeId, string? note)
    {
        if (StoreId != storeId) (ShelfPublicId, ShelfRowPublicId) = (null, null); // giá thuộc kho cũ — không còn đúng
        StoreId = storeId;
        var n = note?.Trim();
        Note = string.IsNullOrEmpty(n) ? null : n.Length <= 500 ? n : throw new BusinessRuleException("NOTE_INVALID", "Ghi chú tối đa 500 ký tự.");
        Version++;
    }

    /// <summary>Xếp giá (monolith: Shelve): chưa xếp giá → sẵn sàng; chọn kho thì chuyển luôn kho.</summary>
    public void Shelve(long? storeId)
    {
        if (Status != ItemStatus.Unshelved)
            throw new BusinessRuleException("ITEM_NOT_UNSHELVED", $"ĐKCB {Barcode} không ở trạng thái chưa xếp giá.");
        Status = ItemStatus.Available;
        if (storeId is not null) StoreId = storeId;
        Version++;
    }

    /// <summary>
    /// Xếp vào giá/ngăn (xếp giá trực quan và xếp giá lại): chưa xếp giá → sẵn sàng; đã sẵn sàng thì đổi vị trí. Mất/thanh lý/xuất kho/đang mượn
    /// không xếp được. Không đổi kho (giá thuộc kho nào là việc của nơi gọi kiểm tra).
    /// </summary>
    public void Place(Guid shelfPublicId, Guid? rowPublicId, DateTimeOffset at)
    {
        if (OnLoan) throw OnLoanError("trả sách trước khi xếp lại giá");
        if (Status == ItemStatus.OutOfStore) throw OutOfStore("nhập kho trước khi xếp giá");
        if (Status == ItemStatus.Lost) throw new BusinessRuleException("ITEM_LOST", $"ĐKCB {Barcode} đã mất.");
        if (Status == ItemStatus.Liquidated) throw Liquidated();
        if (shelfPublicId == Guid.Empty) throw new BusinessRuleException("SHELF_REQUIRED", "Chọn giá sách.");
        (ShelfPublicId, ShelfRowPublicId, ShelvedAt) = (shelfPublicId, rowPublicId, at);
        Status = ItemStatus.Available;
        Version++;
    }

    /// <summary>
    /// Áp trạng thái lượt mượn từ circulation. Cùng lượt: chỉ nhận version lớn hơn; lượt khác: chỉ nhận lượt mượn sau lượt đang giữ
    /// (event tới lệch thứ tự không ghi đè). Lượt đóng vì mất tài liệu (<paramref name="closedItemStatus"/> = L) → bản sách sang "Mất".
    /// Trả về true khi trạng thái bản sách đổi (cần phát ItemChanged).
    /// </summary>
    public bool ApplyLoan(Guid loanPublicId, long version, string cardNo, DateTimeOffset loanedAt, DateTimeOffset dueAt, bool returned,
        string? closedItemStatus, bool reopened = false)
    {
        if (LoanPublicId == loanPublicId ? version <= LoanVersion : LoanedAt is { } current && loanedAt <= current) return false;
        LoanPublicId = loanPublicId;
        LoanVersion = version;
        LoanCardNo = cardNo;
        LoanedAt = loanedAt;
        LoanDueAt = dueAt;
        OnLoan = !returned;
        if (reopened && !returned && Status == ItemStatus.Lost)
        {
            // Lượt đóng vì mất được mở lại (đóng nhầm): bản sách không còn mất, đang được bạn đọc mượn.
            Status = ItemStatus.Available;
            Version++;
            return true;
        }
        if (!returned || closedItemStatus is null || closedItemStatus == Status || !ItemStatus.Names.ContainsKey(closedItemStatus)) return false;
        Status = closedItemStatus;
        Version++;
        return true;
    }

    /// <summary>Đánh dấu xoá — tăng version để event xoá thắng mọi event trước đó.</summary>
    public void MarkDeleted() => Version++;

    /// <summary>Báo mất (monolith: MarkLost). Bản bạn đọc đang mượn mà làm mất xử lý qua phiếu phạt ở circulation (đóng lượt mượn).</summary>
    public void MarkLost()
    {
        if (Status == ItemStatus.Lost) throw new BusinessRuleException("ITEM_ALREADY_LOST", $"ĐKCB {Barcode} đã ở trạng thái mất.");
        if (Status == ItemStatus.Liquidated) throw Liquidated();
        if (OnLoan) throw OnLoanError("xử lý mất qua phiếu phạt ở màn Mượn/Trả");
        SetStatus(ItemStatus.Lost);
    }

    /// <summary>Khôi phục bản đã báo mất (tìm thấy lại) → sẵn sàng phục vụ.</summary>
    public void RestoreFound(string? previousStatus = null)
    {
        if (Status != ItemStatus.Lost) throw new BusinessRuleException("ITEM_NOT_LOST", $"ĐKCB {Barcode} không ở trạng thái mất.");
        SetStatus(previousStatus == ItemStatus.Unshelved ? ItemStatus.Unshelved : ItemStatus.Available);
    }

    /// <summary>Thanh lý — chặn bản đang mượn, đang xuất kho (monolith sau ELIB-LRC 10-04). Bản đã báo mất thanh lý được.</summary>
    public void Liquidate()
    {
        if (Status == ItemStatus.Liquidated) throw Liquidated();
        if (OnLoan) throw OnLoanError("trả sách trước khi thanh lý");
        if (Status == ItemStatus.OutOfStore) throw OutOfStore("nhập kho trước khi thanh lý");
        SetStatus(ItemStatus.Liquidated);
    }

    /// <summary>Huỷ thanh lý: về "Mất" nếu bản còn bản ghi báo mất, ngược lại về sẵn sàng.</summary>
    public void CancelLiquidation(bool stillLost, string? previousStatus = null)
    {
        if (Status != ItemStatus.Liquidated) throw new BusinessRuleException("ITEM_NOT_LIQUIDATED", $"ĐKCB {Barcode} không ở trạng thái thanh lý.");
        // Biết trạng thái trước khi thanh lý thì trả đúng trạng thái đó; bản ghi cũ chưa lưu thì suy từ việc còn bản ghi báo mất không.
        SetStatus(previousStatus is ItemStatus.Lost or ItemStatus.Unshelved or ItemStatus.Available ? previousStatus : stillLost ? ItemStatus.Lost : ItemStatus.Available);
    }

    /// <summary>Xuất kho tạm (photo, triển lãm, chuyển đơn vị khác…) — không cho mượn tới khi nhập lại.</summary>
    public void TakeOut()
    {
        if (OnLoan) throw OnLoanError("không xuất kho được");
        if (Status == ItemStatus.Lost) throw new BusinessRuleException("ITEM_LOST", $"ĐKCB {Barcode} đang ở trạng thái mất.");
        if (Status == ItemStatus.Liquidated) throw Liquidated();
        if (Status == ItemStatus.OutOfStore) throw OutOfStore("quét Nhập kho trước khi xuất lại");
        SetStatus(ItemStatus.OutOfStore);
    }

    /// <summary>Nhập lại kho sau xuất kho → sẵn sàng phục vụ.</summary>
    public void BringBack()
    {
        if (Status != ItemStatus.OutOfStore) throw new BusinessRuleException("ITEM_NOT_OUT", $"ĐKCB {Barcode} không ở trạng thái xuất kho.");
        SetStatus(ItemStatus.Available);
    }

    /// <summary>Điều chuyển sang kho khác (hoàn thành phiếu điều chuyển). Bản đang mượn/xuất kho/mất/thanh lý không chuyển được.</summary>
    public void MoveTo(long storeId)
    {
        if (OnLoan) throw OnLoanError("không chuyển kho được");
        if (Status == ItemStatus.OutOfStore) throw OutOfStore("nhập kho trước khi chuyển");
        if (Status == ItemStatus.Lost) throw new BusinessRuleException("ITEM_LOST", $"ĐKCB {Barcode} đã mất.");
        if (Status == ItemStatus.Liquidated) throw Liquidated();
        if (StoreId != storeId) (ShelfPublicId, ShelfRowPublicId) = (null, null); // giá thuộc kho cũ — không còn đúng
        StoreId = storeId;
        Version++;
    }

    /// <summary>
    /// Đăng ký lại mã vạch (monolith: StoreReRegisterBarcode). Chặn bản đang mượn: lượt mượn ở circulation giữ mã cũ để trả sách
    /// bằng mã quét, đổi mã giữa chừng thì quầy không trả được. Lịch sử đã đóng giữ mã cũ (ảnh chụp lúc mượn).
    /// </summary>
    public void ReRegister(string barcode, long? storeId)
    {
        if (OnLoan) throw OnLoanError("trả sách trước khi đăng ký lại mã");
        var value = NormalizeBarcode(barcode);
        var (prefix, number) = Split(value);
        (Barcode, BarcodeKey, Prefix, Number) = (value, Key(value), prefix, number);
        if (storeId is not null && storeId != StoreId) (StoreId, ShelfPublicId, ShelfRowPublicId) = (storeId, null, null);
        Version++;
    }

    private void SetStatus(string status)
    {
        Status = status;
        Version++;
    }

    private BusinessRuleException OnLoanError(string hint) =>
        new("ITEM_ON_LOAN", $"ĐKCB {Barcode} đang được bạn đọc mượn (thẻ {LoanCardNo}) — {hint}.");

    private BusinessRuleException Liquidated() => new("ITEM_LIQUIDATED", $"ĐKCB {Barcode} đã thanh lý.");

    private BusinessRuleException OutOfStore(string hint) => new("ITEM_OUT_OF_STORE", $"ĐKCB {Barcode} đang xuất kho — {hint}.");

    public static string NormalizeBarcode(string? barcode)
    {
        var value = (barcode ?? "").Trim();
        if (value.Length is 0 or > MaxBarcodeLength)
            throw new BusinessRuleException("BARCODE_INVALID", $"Số ĐKCB bắt buộc, tối đa {MaxBarcodeLength} ký tự.");
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new BusinessRuleException("BARCODE_INVALID", $"Số ĐKCB '{value}' không được chứa khoảng trắng.");
        return value;
    }

    public static string Key(string barcode) => barcode.Trim().ToUpperInvariant();

    /// <summary>Tách phần số cuối: "VV000123" → ("VV", 123); không có số cuối → (mã, null).</summary>
    public static (string Prefix, long? Number) Split(string barcode)
    {
        var i = barcode.Length;
        while (i > 0 && char.IsAsciiDigit(barcode[i - 1])) i--;
        var digits = barcode[i..];
        return digits.Length is > 0 and <= 18 && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? (barcode[..i].ToUpperInvariant(), n)
            : (barcode.ToUpperInvariant(), null);
    }

    /// <summary>Mã theo lô (monolith: BarcodeNumbering): tiền tố + số đệm 0 đủ <paramref name="digits"/> chữ số.</summary>
    public static string Format(string prefix, long number, int digits) =>
        prefix + number.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
}

/// <summary>
/// Bản sao biểu ghi từ catalog (docs 04: BibSnapshot) — nhan đề/tác giả để hiển thị bản sách, không gọi catalog đồng bộ.
/// Cập nhật từ event BibChanged, chỉ khi Version lớn hơn bản đang có.
/// </summary>
public sealed class BibSnapshot : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid BibPublicId { get; set; }
    public long Mfn { get; set; }
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public string? PublishYear { get; set; }
    public string? Isbns { get; set; }
    public string? Ddc { get; set; }

    /// <summary>Ký hiệu tác giả (Cutter) — dòng dưới của nhãn gáy/môn loại.</summary>
    public string? Cutter { get; set; }

    /// <summary>Bản sao đã có <see cref="Cutter"/> (bản ghi từ trước khi có cột này thì false — in nhãn hỏi lại catalog).</summary>
    public bool CutterKnown { get; set; }

    /// <summary>Loại biểu ghi (nhóm tài liệu) — thống kê kho theo loại tài liệu.</summary>
    public long? BibTypeId { get; set; }

    public string? BibTypeName { get; set; }
    public int Status { get; set; }
    public bool Deleted { get; set; }
    public long Version { get; set; }
}

/// <summary>
/// Bản sao đầu phiếu nhận từ acquisition (event ReceiptChanged, hoặc trạng thái acquisition gửi kèm khi đăng ký ĐKCB) — chọn sách theo
/// đơn nhận ở phiếu giao nhận, lọc báo cáo kho theo ngày nhận. Chỉ ghi khi Version lớn hơn bản đang có.
/// </summary>
public sealed class ReceiptSnapshot : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid ReceiptPublicId { get; set; }
    public long Code { get; set; }
    public string? Name { get; set; }
    public DateOnly? ReceivedOn { get; set; }
    public long? StoreId { get; set; }
    public bool Completed { get; set; }
    public bool Deleted { get; set; }
    public long Version { get; set; }

    /// <summary>
    /// Khoá lạc quan khi đăng ký ĐKCB theo phiếu (concurrency token): mỗi lần đăng ký tăng giá trị này cùng transaction với bản sách mới —
    /// hai phiên cùng đăng ký trên một phiếu thì phiên sau bị từ chối thay vì cùng vượt số bản nhận.
    /// </summary>
    public long RegistrationStamp { get; set; }
}
