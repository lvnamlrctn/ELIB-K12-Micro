using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

/// <summary>1=Chờ duyệt, 2=Đã duyệt, 3=Đã check-in, 4=Hoàn tất, 5=Đã huỷ (bạn đọc), 6=Từ chối (thủ thư hoặc tự hết hạn), 7=Không tới.</summary>
public static class RoomBookingStatus
{
    public const int Pending = 1;
    public const int Approved = 2;
    public const int CheckedIn = 3;
    public const int Completed = 4;
    public const int Cancelled = 5;
    public const int Rejected = 6;
    public const int NoShow = 7;
}

/// <summary>Lượt đặt phòng/không gian (monolith: map.RoomBooking, quyền SPACE_BOOKINGS). Validate nghiệp vụ nằm ở BookingResource
/// (cần tra bản sao bạn đọc/không gian/giờ mở cửa) — entity chỉ giữ bất biến chuyển trạng thái.</summary>
public sealed class RoomBooking : TenantEntity
{
    public const int MaxNoteLength = 1000;
    public const int MaxRejectReasonLength = 500;

    private RoomBooking() { }

    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public long SpaceId { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public int PartySize { get; private set; }
    public int Status { get; private set; } = RoomBookingStatus.Pending;
    public long? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? RejectReason { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? CheckedInAt { get; private set; }
    public long? CheckedInBy { get; private set; }
    public DateTimeOffset? CheckedOutAt { get; private set; }
    public long? CheckedOutBy { get; private set; }
    public string? Note { get; private set; }

    public bool IsOpen => Status is RoomBookingStatus.Pending or RoomBookingStatus.Approved or RoomBookingStatus.CheckedIn;

    public static RoomBooking Open(Guid readerPublicId, string cardNo, long spaceId, DateTimeOffset startAt, DateTimeOffset endAt, int partySize,
        string? note, bool autoApprove, DateTimeOffset now, long? approvedBy = null)
    {
        var card = (cardNo ?? "").Trim().ToUpperInvariant();
        var n = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var booking = new RoomBooking
        {
            ReaderPublicId = readerPublicId, CardNo = card, SpaceId = spaceId, StartAt = startAt, EndAt = endAt, PartySize = partySize,
            Note = n is null || n.Length <= MaxNoteLength ? n : n[..MaxNoteLength],
            Status = autoApprove ? RoomBookingStatus.Approved : RoomBookingStatus.Pending,
        };
        if (autoApprove)
        {
            booking.ApprovedBy = approvedBy;
            booking.ApprovedAt = now;
        }
        return booking;
    }

    public void Approve(long? staffId, DateTimeOffset now)
    {
        if (Status != RoomBookingStatus.Pending) throw new BusinessRuleException("BOOKING_NOT_PENDING", "Lượt đặt không ở trạng thái chờ duyệt.");
        Status = RoomBookingStatus.Approved;
        ApprovedBy = staffId;
        ApprovedAt = now;
    }

    /// <summary>Thủ thư từ chối — nhận cả lượt đang Chờ duyệt hoặc đã Đã duyệt (đúng bản gốc).</summary>
    public void Reject(string reason, long? staffId, DateTimeOffset now)
    {
        if (Status is not (RoomBookingStatus.Pending or RoomBookingStatus.Approved))
            throw new BusinessRuleException("BOOKING_NOT_REJECTABLE", "Lượt đặt không ở trạng thái chờ duyệt hoặc đã duyệt.");
        var r = (reason ?? "").Trim();
        RejectReason = r.Length is > 0 and <= MaxRejectReasonLength ? r : throw new BusinessRuleException("REASON_REQUIRED", $"Nhập lý do từ chối (tối đa {MaxRejectReasonLength} ký tự).");
        Status = RoomBookingStatus.Rejected;
        ApprovedBy = staffId;
        ApprovedAt = now;
    }

    /// <summary>Job tự hết hạn (Chờ duyệt mà đến giờ vẫn chưa duyệt) — không cần lý do, khác <see cref="Reject"/> của thủ thư.</summary>
    public void AutoExpireToRejected(DateTimeOffset now)
    {
        if (Status != RoomBookingStatus.Pending) return;
        RejectReason = "Quá hạn duyệt.";
        Status = RoomBookingStatus.Rejected;
        ApprovedAt = now;
    }

    public void CheckIn(long? staffId, DateTimeOffset now)
    {
        if (Status != RoomBookingStatus.Approved) throw new BusinessRuleException("BOOKING_NOT_APPROVED", "Lượt đặt chưa được duyệt hoặc đã check-in.");
        Status = RoomBookingStatus.CheckedIn;
        CheckedInAt = now;
        CheckedInBy = staffId;
    }

    public void CheckOut(long? staffId, DateTimeOffset now)
    {
        if (Status != RoomBookingStatus.CheckedIn) throw new BusinessRuleException("BOOKING_NOT_CHECKED_IN", "Lượt đặt chưa check-in.");
        Status = RoomBookingStatus.Completed;
        CheckedOutAt = now;
        CheckedOutBy = staffId;
    }

    public void AutoComplete(DateTimeOffset now)
    {
        if (Status != RoomBookingStatus.CheckedIn) return;
        Status = RoomBookingStatus.Completed;
    }

    public void MarkNoShow(DateTimeOffset now)
    {
        if (Status != RoomBookingStatus.Approved) return;
        Status = RoomBookingStatus.NoShow;
    }

    /// <summary>Chủ phòng tự huỷ — chỉ khi chưa tới giờ bắt đầu (kiểm now &lt; StartAt ở caller cùng quyền chủ phòng).</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status is not (RoomBookingStatus.Pending or RoomBookingStatus.Approved))
            throw new BusinessRuleException("BOOKING_NOT_CANCELLABLE", "Lượt đặt không ở trạng thái chờ duyệt hoặc đã duyệt.");
        if (now >= StartAt) throw new BusinessRuleException("BOOKING_ALREADY_STARTED", "Không thể huỷ sau khi lượt đặt đã bắt đầu.");
        Status = RoomBookingStatus.Cancelled;
        CancelledAt = now;
    }

    /// <summary>Ngày theo giờ Việt Nam (UTC+7, cố định như circulation's Loan.LocalDate) — dùng tính giới hạn theo "ngày".</summary>
    public static DateOnly LocalDate(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime.AddHours(7));
}

/// <summary>Thành viên nhóm của một lượt đặt phòng (monolith: map.RoomBookingMember) — bất biến sau khi tạo.</summary>
public sealed class RoomBookingMember : TenantEntity
{
    private RoomBookingMember() { }

    public long BookingId { get; private set; }
    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";

    public static RoomBookingMember Create(long bookingId, Guid readerPublicId, string cardNo) => new()
    {
        BookingId = bookingId, ReaderPublicId = readerPublicId, CardNo = (cardNo ?? "").Trim().ToUpperInvariant(),
    };
}
