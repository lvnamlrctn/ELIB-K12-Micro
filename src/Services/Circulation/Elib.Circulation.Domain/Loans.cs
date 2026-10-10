using Elib.BuildingBlocks.Domain;

namespace Elib.Circulation.Domain;

/// <summary>
/// Lượt mượn (monolith: BookOut + BookIn gộp làm một — trả là điền <see cref="ReturnedAt"/>). Bạn đọc/bản sách tham chiếu theo
/// PublicId của service gốc, kèm số thẻ/ĐKCB/MFN chụp lúc mượn để tra cứu lịch sử không cần bản sao.
/// <see cref="Version"/> tăng ở mỗi thay đổi — bản sao ở holdings/search bỏ qua event cũ hơn.
/// </summary>
public sealed class Loan : TenantEntity
{
    public const int MaxNoteLength = 1000;

    private Loan() { }

    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public Guid ItemPublicId { get; private set; }
    public string Barcode { get; private set; } = "";
    public long Mfn { get; private set; }
    public long? CircPlaceId { get; private set; }
    public long? StoreId { get; private set; }
    public DateTimeOffset LoanedAt { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public long? LoanedBy { get; private set; }
    public long? ReturnedBy { get; private set; }
    public long? ReturnCircPlaceId { get; private set; }
    public int RenewCount { get; private set; }
    public string? Note { get; private set; }
    public long Version { get; private set; }

    public bool IsOpen => ReturnedAt is null;

    public static Loan Open(PatronReplica reader, ItemReplica item, long? circPlaceId, DateTimeOffset now, int loanDays, long? staffId)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(item);
        return new Loan
        {
            ReaderPublicId = reader.ReaderPublicId, CardNo = reader.CardNo,
            ItemPublicId = item.ItemPublicId, Barcode = item.Barcode, Mfn = item.Mfn, StoreId = item.StoreId,
            CircPlaceId = circPlaceId, LoanedAt = now, DueAt = now.AddDays(loanDays), LoanedBy = staffId,
        };
    }

    public void Return(DateTimeOffset now, long? circPlaceId, long? staffId)
    {
        if (!IsOpen) throw new BusinessRuleException("LOAN_ALREADY_RETURNED", $"ĐKCB {Barcode} đã trả lúc {ReturnedAt:dd/MM/yyyy HH:mm}.");
        ReturnedAt = now;
        ReturnCircPlaceId = circPlaceId;
        ReturnedBy = staffId;
        Version++;
    }

    /// <summary>
    /// Gia hạn (monolith: Renew, C_RENEW_DATE = 0): hạn mới = hạn cũ + số ngày gia hạn. Lượt đã quá hạn thì tính từ hôm nay
    /// để không gia hạn xong vẫn quá hạn.
    /// </summary>
    public void Renew(DateTimeOffset now, LoanPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!IsOpen) throw new BusinessRuleException("LOAN_ALREADY_RETURNED", $"ĐKCB {Barcode} đã trả, không gia hạn được.");
        if (policy.MaxRenewals is { } max && RenewCount >= max)
            throw new BusinessRuleException("RENEW_LIMIT", max == 0
                ? "Chính sách lưu thông không cho gia hạn."
                : $"Bạn đọc đã gia hạn đủ số lần cho phép ({max} lần).");
        DueAt = (DueAt > now ? DueAt : now).AddDays(policy.RenewDays);
        RenewCount++;
        Version++;
    }

    public void SetNote(string? note)
    {
        var n = note?.Trim();
        Note = string.IsNullOrEmpty(n) ? null : n.Length <= MaxNoteLength ? n : throw new BusinessRuleException("NOTE_INVALID", $"Ghi chú tối đa {MaxNoteLength} ký tự.");
        Version++;
    }

    public bool IsOverdue(DateTimeOffset now) => IsOpen && DueAt < now;

    /// <summary>Số ngày quá hạn tính theo ngày lịch Việt Nam (trả trong ngày hết hạn = không quá hạn).</summary>
    public int OverdueDays(DateTimeOffset now) => Math.Max(0, LocalDate(ReturnedAt ?? now).DayNumber - LocalDate(DueAt).DayNumber);

    public static DateOnly LocalDate(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime.AddHours(7));
}

/// <summary>Bản sao bạn đọc từ patron (ReaderChanged) — docs 04: PatronReplica.</summary>
public sealed class PatronReplica : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid ReaderPublicId { get; set; }
    public string CardNo { get; set; } = "";
    public string FullName { get; set; } = "";
    public long? ReaderTypeId { get; set; }
    public string? ReaderTypeName { get; set; }
    public string? ClassName { get; set; }
    public string? CourseName { get; set; }
    public Guid? PhotoId { get; set; }

    /// <summary>2 = hoạt động, 1 = bị khoá.</summary>
    public int Status { get; set; }

    public DateOnly? ExpireDate { get; set; }
    public bool Deleted { get; set; }
    public long Version { get; set; }

    public static string Key(string cardNo) => (cardNo ?? "").Trim().ToUpperInvariant();
}

/// <summary>Bản sao bản sách từ holdings (ItemChanged) — docs 04: ItemReplica.</summary>
public sealed class ItemReplica : Entity, ITenantOwned
{
    /// <summary>Trạng thái holdings cho mượn được (R — đã xếp giá, sẵn sàng).</summary>
    public const string Available = "R";

    public long TenantId { get; set; }
    public Guid ItemPublicId { get; set; }
    public string Barcode { get; set; } = "";
    public string BarcodeKey { get; set; } = "";
    public Guid BibPublicId { get; set; }
    public long Mfn { get; set; }
    public long? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string Status { get; set; } = "";
    public bool Deleted { get; set; }
    public long Version { get; set; }

    public static string Key(string barcode) => (barcode ?? "").Trim().ToUpperInvariant();
}

/// <summary>Bản sao biểu ghi từ catalog (BibChanged) — nhan đề/tác giả hiển thị ở quầy mượn trả.</summary>
public sealed class BibSnapshot : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid BibPublicId { get; set; }
    public long Mfn { get; set; }
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public string? Ddc { get; set; }
    public bool Deleted { get; set; }
    public long Version { get; set; }
}
