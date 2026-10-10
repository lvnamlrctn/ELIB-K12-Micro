using Elib.BuildingBlocks.Domain;

namespace Elib.Circulation.Domain;

/// <summary>Trạng thái đặt mượn.</summary>
public static class HoldStatus
{
    /// <summary>Chờ có bản sách (mọi bản đang được mượn/giữ).</summary>
    public const int Waiting = 1;

    /// <summary>Đã giữ một bản ở quầy, chờ bạn đọc tới mượn trước <see cref="Hold.ExpiresAt"/>.</summary>
    public const int Ready = 2;

    /// <summary>Bạn đọc đã mượn.</summary>
    public const int Fulfilled = 3;

    public const int Cancelled = 4;

    /// <summary>Quá hạn giữ chỗ mà bạn đọc không tới.</summary>
    public const int Expired = 5;

    public static bool IsActive(int status) => status is Waiting or Ready;
}

/// <summary>
/// Đặt mượn (monolith: BookRequest, quyền REQUEST_BOOKS): bạn đọc đặt theo biểu ghi; còn bản sẵn sàng thì giữ ngay một bản
/// (monolith: chọn bản trống, giữ 48 giờ), hết bản thì xếp hàng — bản được trả sẽ giữ cho người đặt sớm nhất. Bản đang giữ chỉ
/// người đặt mượn được; quá <see cref="ExpiresAt"/> thì hết hạn và bản chuyển cho người kế tiếp (monolith: BookRequestExpiryJob).
/// </summary>
public sealed class Hold : TenantEntity
{
    public const int MaxNoteLength = 500;

    private Hold() { }

    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public long Mfn { get; private set; }
    public long? CircPlaceId { get; private set; }
    public int Status { get; private set; } = HoldStatus.Waiting;
    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>Bản đang giữ (khi Ready) — giữ lại sau khi đóng để tra cứu.</summary>
    public Guid? ItemPublicId { get; private set; }

    public string? Barcode { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? Note { get; private set; }

    public bool IsActive => HoldStatus.IsActive(Status);

    public static Hold Place(PatronReplica reader, long mfn, long? circPlaceId, DateTimeOffset now, string? note)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var hold = new Hold { ReaderPublicId = reader.ReaderPublicId, CardNo = reader.CardNo, Mfn = mfn, CircPlaceId = circPlaceId, RequestedAt = now };
        hold.SetNote(note);
        return hold;
    }

    /// <summary>Giữ một bản cho người đặt — hạn tới cuối ngày (giờ VN) thứ <paramref name="holdDays"/> kể từ hôm nay.</summary>
    public void Assign(ItemReplica item, DateTimeOffset now, int holdDays)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Status != HoldStatus.Waiting) throw new BusinessRuleException("HOLD_NOT_WAITING", "Đặt mượn không ở trạng thái chờ.");
        Status = HoldStatus.Ready;
        ItemPublicId = item.ItemPublicId;
        Barcode = item.Barcode;
        ReadyAt = now;
        var lastDay = Loan.LocalDate(now).AddDays(Math.Max(1, holdDays));
        ExpiresAt = new DateTimeOffset(lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddTicks(-1);
    }

    public void Fulfil(DateTimeOffset now, ItemReplica item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Close(HoldStatus.Fulfilled, now);
        ItemPublicId = item.ItemPublicId;
        Barcode = item.Barcode;
    }

    public void Cancel(DateTimeOffset now, string? reason)
    {
        if (!IsActive) throw new BusinessRuleException("HOLD_CLOSED", "Đặt mượn đã kết thúc, không huỷ được.");
        Close(HoldStatus.Cancelled, now);
        if (!string.IsNullOrWhiteSpace(reason)) SetNote(string.IsNullOrEmpty(Note) ? reason : $"{Note} — Huỷ: {reason.Trim()}");
    }

    public void Expire(DateTimeOffset now) => Close(HoldStatus.Expired, now);

    private void Close(int status, DateTimeOffset now)
    {
        Status = status;
        ClosedAt = now;
    }

    private void SetNote(string? note)
    {
        var n = note?.Trim();
        Note = string.IsNullOrEmpty(n) ? null : n.Length <= MaxNoteLength ? n : n[..MaxNoteLength];
    }
}
