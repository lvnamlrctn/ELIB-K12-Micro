using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>Trạng thái lượt mượn tài liệu số (LRC: EbookItemLoan.Status).</summary>
public static class EbookLoanStatus
{
    public const int Active = 1;

    /// <summary>Nhân viên thu hồi quyền đọc (RecalledBy có giá trị).</summary>
    public const int Recalled = 2;

    /// <summary>Bạn đọc tự trả (RecalledBy null, RecallReason ghi "tự trả" — LRC dùng chung cột Recalled* cho cả hai trường hợp đóng lượt).</summary>
    public const int Returned = 3;

    /// <summary>Tự hết hạn offline, job dọn (LRC: EbookLoanExpiryJob).</summary>
    public const int AutoExpired = 4;

    public static bool IsOpen(int status) => status == Active;
}

/// <summary>
/// Lượt mượn tài liệu số (LRC: EbookItemLoan). Không có cột ReturnedAt riêng — LRC dùng chung RecalledAt/RecalledBy/RecallReason
/// cho cả trả tự nguyện (RecalledBy null) và thu hồi (RecalledBy = nhân viên); tự hết hạn (AutoExpired) không ghi các cột này.
/// </summary>
public sealed class EbookItemLoan : TenantEntity
{
    public const int MaxReasonLength = 500;

    private EbookItemLoan() { }

    public long EbookItemId { get; private set; }
    public Guid ReaderPublicId { get; private set; }
    public DateTimeOffset CheckedOutAt { get; private set; }

    /// <summary>CheckedOutAt + OfflineDays hiệu lực; null = không giới hạn.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? LastAccessAt { get; private set; }
    public int Status { get; private set; } = EbookLoanStatus.Active;
    public DateTimeOffset? RecalledAt { get; private set; }
    public long? RecalledBy { get; private set; }
    public string? RecallReason { get; private set; }

    public bool IsOpen => EbookLoanStatus.IsOpen(Status);

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } e && e < now;

    public static EbookItemLoan Open(long ebookItemId, Guid readerPublicId, DateTimeOffset now, DateTimeOffset? expiresAt) => new()
    {
        EbookItemId = ebookItemId, ReaderPublicId = readerPublicId, CheckedOutAt = now, ExpiresAt = expiresAt, LastAccessAt = now,
    };

    public void RecordAccess(DateTimeOffset now) => LastAccessAt = now;

    /// <summary>Bạn đọc tự trả qua OPAC (LRC: ReturnAsync — RecalledBy null phân biệt với thu hồi).</summary>
    public void Return(DateTimeOffset now)
    {
        if (!IsOpen) throw new BusinessRuleException("LOAN_NOT_OPEN", "Lượt mượn không ở trạng thái đang mượn.");
        Status = EbookLoanStatus.Returned;
        RecalledAt = now;
        RecalledBy = null;
        RecallReason = "Bạn đọc tự trả qua OPAC.";
    }

    /// <summary>Nhân viên thu hồi quyền đọc trước hạn — lý do bắt buộc.</summary>
    public void Recall(DateTimeOffset now, long? staffId, string reason)
    {
        if (!IsOpen) throw new BusinessRuleException("LOAN_NOT_OPEN", "Lượt mượn không ở trạng thái đang mượn.");
        Status = EbookLoanStatus.Recalled;
        RecalledAt = now;
        RecalledBy = staffId;
        RecallReason = RequireReason(reason);
    }

    /// <summary>Job hết hạn tự động — không ghi Recalled* (LRC: EbookLoanExpiryJob chỉ đổi Status).</summary>
    public void AutoExpire()
    {
        if (IsOpen) Status = EbookLoanStatus.AutoExpired;
    }

    private static string RequireReason(string? reason)
    {
        var r = reason?.Trim();
        return r is { Length: > 0 and <= MaxReasonLength }
            ? r
            : throw new BusinessRuleException("REASON_REQUIRED", $"Nhập lý do thu hồi (tối đa {MaxReasonLength} ký tự).");
    }
}

/// <summary>Trạng thái đặt trước tài liệu số (LRC: EbookItemReservation.Status).</summary>
public static class EbookReservationStatus
{
    public const int Pending = 1;
    public const int Ready = 2;
    public const int Fulfilled = 3;
    public const int Cancelled = 4;
    public const int Expired = 5;

    public static bool IsActive(int status) => status is Pending or Ready;
}

/// <summary>
/// Đặt trước tài liệu số khi hết "bản đọc đồng thời" (LRC: EbookItemReservation) — xếp hàng FIFO theo Id, thăng hạng Ready khi có
/// chỗ trống (xem EbookReservationAllocator).
/// </summary>
public sealed class EbookItemReservation : TenantEntity
{
    private EbookItemReservation() { }

    public long EbookItemId { get; private set; }
    public Guid ReaderPublicId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public int Status { get; private set; } = EbookReservationStatus.Pending;
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? ReadyExpiresAt { get; private set; }
    public DateTimeOffset? FulfilledAt { get; private set; }

    /// <summary>Thời điểm đóng không do mượn được (huỷ hoặc hết hạn giữ chỗ) — LRC dùng một cột cho cả hai trường hợp.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    public bool IsActive => EbookReservationStatus.IsActive(Status);

    public static EbookItemReservation Place(long ebookItemId, Guid readerPublicId, DateTimeOffset now) =>
        new() { EbookItemId = ebookItemId, ReaderPublicId = readerPublicId, RequestedAt = now };

    public void MakeReady(DateTimeOffset now, DateTimeOffset readyExpiresAt)
    {
        if (Status != EbookReservationStatus.Pending) throw new BusinessRuleException("RESERVATION_NOT_PENDING", "Đặt trước không ở trạng thái chờ.");
        Status = EbookReservationStatus.Ready;
        ReadyAt = now;
        ReadyExpiresAt = readyExpiresAt;
    }

    public void Fulfil(DateTimeOffset now)
    {
        Status = EbookReservationStatus.Fulfilled;
        FulfilledAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (!IsActive) throw new BusinessRuleException("RESERVATION_CLOSED", "Đặt trước đã kết thúc, không huỷ được.");
        Status = EbookReservationStatus.Cancelled;
        CancelledAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        Status = EbookReservationStatus.Expired;
        CancelledAt = now;
    }
}
