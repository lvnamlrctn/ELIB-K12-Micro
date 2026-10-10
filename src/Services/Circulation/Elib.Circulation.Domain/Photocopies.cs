using Elib.BuildingBlocks.Domain;

namespace Elib.Circulation.Domain;

/// <summary>
/// Phiếu sao chụp tài liệu (monolith: C_photo, quyền C_PHOTO). Thành tiền = (trang cuối − trang đầu + 1) × số bản × đơn giá, luôn
/// tính ở server.
/// </summary>
public sealed class Photocopy : TenantEntity
{
    public const int MaxPages = 100_000;

    private Photocopy() { }

    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public Guid ItemPublicId { get; private set; }
    public string Barcode { get; private set; } = "";
    public long Mfn { get; private set; }
    public int FromPage { get; private set; }
    public int ToPage { get; private set; }
    public int Copies { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Total { get; private set; }
    public bool Paid { get; private set; }
    public DateOnly PhotoDate { get; private set; }
    public string? Note { get; private set; }

    public static Photocopy Create(PatronReplica reader, ItemReplica item, int fromPage, int toPage, int copies, decimal unitPrice, DateOnly date,
        bool paid, string? note)
    {
        var photo = new Photocopy();
        photo.Update(reader, item, fromPage, toPage, copies, unitPrice, date, paid, note);
        return photo;
    }

    public void Update(PatronReplica reader, ItemReplica item, int fromPage, int toPage, int copies, decimal unitPrice, DateOnly date, bool paid,
        string? note)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(item);
        if (fromPage < 1) throw new BusinessRuleException("PAGES_INVALID", "Trang bắt đầu phải lớn hơn hoặc bằng 1.");
        if (toPage < fromPage || toPage > MaxPages) throw new BusinessRuleException("PAGES_INVALID", "Trang kết thúc phải lớn hơn hoặc bằng trang bắt đầu.");
        if (copies is < 1 or > 1000) throw new BusinessRuleException("COPIES_INVALID", "Số bản sao từ 1 đến 1000.");
        ReaderPublicId = reader.ReaderPublicId;
        CardNo = reader.CardNo;
        ItemPublicId = item.ItemPublicId;
        Barcode = item.Barcode;
        Mfn = item.Mfn;
        FromPage = fromPage;
        ToPage = toPage;
        Copies = copies;
        UnitPrice = FineMoney.Validate(unitPrice, "Đơn giá");
        Total = (toPage - fromPage + 1) * copies * unitPrice;
        if (Total > FineMoney.Max) throw new BusinessRuleException("AMOUNT_INVALID", "Thành tiền quá lớn.");
        PhotoDate = date;
        Paid = paid;
        var n = note?.Trim();
        Note = string.IsNullOrEmpty(n) ? null : n.Length <= 500 ? n : throw new BusinessRuleException("NOTE_INVALID", "Ghi chú tối đa 500 ký tự.");
    }

    public void SetPaid(bool paid) => Paid = paid;
}
