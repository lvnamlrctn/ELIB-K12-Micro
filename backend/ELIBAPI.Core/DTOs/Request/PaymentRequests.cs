namespace ELIBAPI.Core.DTOs.Request;

public class CreatePaymentRequest
{
    public long    ReaderId   { get; set; }
    public string  TargetType { get; set; } = null!; // FINE_TICKET | PHOTOCOPY | CARD_REISSUE
    public long?   TargetId   { get; set; }
    public string  Provider   { get; set; } = null!;  // VietQR | VNPAY
}

/// <summary>Thân request tạo giao dịch của độc giả tự phục vụ (OPAC) — không có ReaderId, resolve từ JWT.</summary>
public class CreateReaderPaymentRequest
{
    public string TargetType { get; set; } = null!;
    public long?  TargetId   { get; set; }
    public string Provider   { get; set; } = null!;
}
