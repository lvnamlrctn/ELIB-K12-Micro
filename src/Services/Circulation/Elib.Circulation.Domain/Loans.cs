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

    /// <summary>Trạng thái holdings "Mất" — lượt mượn đóng vì mất tài liệu.</summary>
    public const string LostStatus = "L";

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

    /// <summary>Lượt đóng không phải do trả sách: trạng thái bản sách holdings phải chuyển sang (L = mất). Null = trả bình thường.</summary>
    public string? ClosedItemStatus { get; private set; }

    /// <summary>Ngày (giờ Việt Nam) đã phát nhắc sắp đến hạn / quá hạn — job nhắc hạn không phát lại.</summary>
    public DateOnly? DueSoonNotifiedOn { get; private set; }

    public DateOnly? OverdueNotifiedOn { get; private set; }
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
    /// Đóng lượt vì mất tài liệu (monolith: lý do phạt có Status_Reg_Id — BookOut "R" + BookIn, ĐKCB sang "Mất"). Holdings nhận
    /// LoanChanged có <see cref="ClosedItemStatus"/> để đổi trạng thái bản sách.
    /// </summary>
    public void CloseAsLost(DateTimeOffset now, long? staffId)
    {
        if (!IsOpen) return;
        ClosedItemStatus = LostStatus;
        Return(now, null, staffId);
    }

    public void MarkDueSoonNotified(DateOnly day) => DueSoonNotifiedOn = day;

    public void MarkOverdueNotified(DateOnly day) => OverdueNotifiedOn = day;

    /// <summary>
    /// Gia hạn (monolith: Renew). Mặc định (C_RENEW_DATE = 0): hạn mới = hạn cũ + số ngày gia hạn; lượt đã quá hạn thì tính từ hôm
    /// nay để không gia hạn xong vẫn quá hạn. Chính sách "gia hạn tính từ hôm nay" (C_RENEW_DATE = 1): hạn mới = hôm nay + số ngày.
    /// </summary>
    public void Renew(DateTimeOffset now, LoanPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!IsOpen) throw new BusinessRuleException("LOAN_ALREADY_RETURNED", $"ĐKCB {Barcode} đã trả, không gia hạn được.");
        if (policy.MaxRenewals is { } max && RenewCount >= max)
            throw new BusinessRuleException("RENEW_LIMIT", max == 0
                ? "Chính sách lưu thông không cho gia hạn."
                : $"Bạn đọc đã gia hạn đủ số lần cho phép ({max} lần).");
        DueAt = (DueAt > now && !policy.RenewFromToday ? DueAt : now).AddDays(policy.RenewDays);
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

    /// <summary>0 giờ ngày <paramref name="date"/> giờ Việt Nam, biểu diễn ở UTC (Npgsql chỉ ghi/so sánh timestamptz offset 0).</summary>
    public static DateTimeOffset VietnamStart(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
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

    /// <summary>Để gửi nhắc hạn/thông báo đặt mượn qua notification.</summary>
    public string? Email { get; set; }

    public string? Phone { get; set; }

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

/// <summary>Một lần gia hạn (monolith: C_Renew) — báo cáo hoạt động phục vụ đếm lượt gia hạn theo ngày.</summary>
public sealed class LoanRenewal : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid LoanPublicId { get; set; }
    public Guid ReaderPublicId { get; set; }
    public long? CircPlaceId { get; set; }
    public DateTimeOffset RenewedAt { get; set; }
    public DateTimeOffset OldDueAt { get; set; }
    public DateTimeOffset NewDueAt { get; set; }
    public string Reason { get; set; } = "";
    public long? RenewedBy { get; set; }
}
