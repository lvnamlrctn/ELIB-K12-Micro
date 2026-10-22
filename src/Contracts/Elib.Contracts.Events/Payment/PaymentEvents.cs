namespace Elib.Contracts.Events.Payment;

/// <summary>
/// Khoản cần thu do service nguồn phát (circulation: phiếu phạt, sao chụp; patron: cấp lại thẻ). Khoá theo (<see cref="SourceService"/>,
/// <see cref="SourceId"/>); chỉ nhận khi <see cref="Version"/> lớn hơn bản đang có. <see cref="Amount"/> = số còn phải thu (0 = không còn gì phải thu).
/// Số tiền luôn do nguồn quyết định — payment không nhận số tiền từ client.
/// </summary>
public sealed record ChargeIssued : IntegrationEvent
{
    public required string SourceService { get; init; }
    public required Guid SourceId { get; init; }

    /// <summary>FINE_TICKET | PHOTOCOPY | CARD_REISSUE.</summary>
    public required string Kind { get; init; }

    public required Guid ReaderPublicId { get; init; }
    public required string CardNo { get; init; }

    /// <summary>Nhãn hiển thị cho cán bộ / bạn đọc ("Phiếu phạt PT012").</summary>
    public required string Label { get; init; }

    public required decimal Amount { get; init; }
    public long Version { get; init; }

    /// <summary>
    /// Mã giao dịch QR gần nhất mà nguồn đã GHI NHẬN vào sổ. Payment giữ khoản "đã thu" cho tới khi gặp event xác nhận đúng mã này — nhờ vậy
    /// event cũ còn đang bay (chưa kịp phản ánh khoản vừa thu) không mở lại khoản đã thanh toán, còn phiếu được mở lại / tăng tiền SAU đó thì có mã mới.
    /// </summary>
    public string? LastPaymentCode { get; init; }
}

/// <summary>Khoản thu bị huỷ / miễn (xoá phiếu, miễn toàn bộ) — mã thanh toán đang chờ phải đóng.</summary>
public sealed record ChargeWaived : IntegrationEvent
{
    public required string SourceService { get; init; }
    public required Guid SourceId { get; init; }
    public long Version { get; init; }
}

/// <summary>Khoản thu đã thu bằng cách khác (tiền mặt tại quầy) — mã thanh toán đang chờ phải đóng để không thu hai lần.</summary>
public sealed record ChargeSettled : IntegrationEvent
{
    public required string SourceService { get; init; }
    public required Guid SourceId { get; init; }
    public long Version { get; init; }
}

/// <summary>
/// Tiền đã về qua cổng thanh toán. Service nguồn ghi nhận khoản thu (khoá chống trùng: <see cref="TransactionCode"/>).
/// <see cref="Late"/>: tiền về sau khi mã đã hết hạn — payment đã đánh dấu cần đối soát.
/// </summary>
public sealed record PaymentSucceeded : IntegrationEvent
{
    public required string SourceService { get; init; }
    public required Guid SourceId { get; init; }
    public required string Kind { get; init; }
    public required string TransactionCode { get; init; }
    public required string Provider { get; init; }
    public required decimal Amount { get; init; }
    public required DateTimeOffset PaidAt { get; init; }
    public required Guid ReaderPublicId { get; init; }
    public bool Late { get; init; }
}

/// <summary>Mã thanh toán hết hạn mà chưa thu.</summary>
public sealed record PaymentExpired : IntegrationEvent
{
    public required string SourceService { get; init; }
    public required Guid SourceId { get; init; }
    public required string TransactionCode { get; init; }
}
