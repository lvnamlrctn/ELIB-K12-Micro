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
    public string Status { get; private set; } = ItemStatus.Unshelved;
    public string? Note { get; private set; }
    public long Version { get; private set; }

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

    public void Update(long? storeId, string? note)
    {
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
    /// Áp trạng thái lượt mượn từ circulation. Cùng lượt: chỉ nhận version lớn hơn; lượt khác: chỉ nhận lượt mượn sau lượt đang giữ
    /// (event tới lệch thứ tự không ghi đè). Lượt đóng vì mất tài liệu (<paramref name="closedItemStatus"/> = L) → bản sách sang "Mất".
    /// Trả về true khi trạng thái bản sách đổi (cần phát ItemChanged).
    /// </summary>
    public bool ApplyLoan(Guid loanPublicId, long version, string cardNo, DateTimeOffset loanedAt, DateTimeOffset dueAt, bool returned,
        string? closedItemStatus)
    {
        if (LoanPublicId == loanPublicId ? version <= LoanVersion : LoanedAt is { } current && loanedAt <= current) return false;
        LoanPublicId = loanPublicId;
        LoanVersion = version;
        LoanCardNo = cardNo;
        LoanedAt = loanedAt;
        LoanDueAt = dueAt;
        OnLoan = !returned;
        if (!returned || closedItemStatus is null || closedItemStatus == Status || !ItemStatus.Names.ContainsKey(closedItemStatus)) return false;
        Status = closedItemStatus;
        Version++;
        return true;
    }

    /// <summary>Đánh dấu xoá — tăng version để event xoá thắng mọi event trước đó.</summary>
    public void MarkDeleted() => Version++;

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
    public int Status { get; set; }
    public bool Deleted { get; set; }
    public long Version { get; set; }
}
