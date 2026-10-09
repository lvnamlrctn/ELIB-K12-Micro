using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Payment;

/// <summary>Giao dịch thanh toán QR (VietQR/VNPAY) cho phạt quá hạn, phí sao chụp, phí cấp lại thẻ —
/// độc lập với loại phí (xem TargetType), tránh 3 luồng thanh toán riêng biệt (port ELIB-LRC 09-25).
/// K12: TenantId = đơn vị của bạn đọc — quyết định tài khoản nhận tiền (VietQR/VNPAY/Sepay theo đơn vị).
/// Bảng tạo lúc khởi động (PaymentSchema.PostgreSql).</summary>
[Table("PaymentTransaction", Schema = "payment")]
public class PaymentTransaction
{
    [Key] public long Id { get; set; }

    /// <summary>Mã ngắn duy nhất, nhúng vào nội dung chuyển khoản để đối soát (vd "PT260925143012AB3F").</summary>
    public string TransactionCode { get; set; } = null!;

    public long ReaderId { get; set; }

    /// <summary>"FINE_TICKET" | "PHOTOCOPY" | "CARD_REISSUE".</summary>
    public string TargetType { get; set; } = null!;

    /// <summary>CFineTicket.Id / CPhoto.Id — null khi TargetType = CARD_REISSUE (không có bảng nghiệp vụ).</summary>
    public long? TargetId { get; set; }

    public double Amount { get; set; }

    /// <summary>"VietQR" | "VNPAY".</summary>
    public string Provider { get; set; } = null!;

    /// <summary>"Pending" | "Paid" | "Expired" | "Cancelled".</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Payload QR đã sinh (cache — khỏi build lại khi client poll trạng thái).</summary>
    public string? QrContent { get; set; }

    /// <summary>Mã giao dịch phía ngân hàng/VNPAY khi đã khớp — dùng để chống xử lý trùng (idempotency).</summary>
    public string? ProviderRef { get; set; }

    /// <summary>Giờ thư viện (LibraryClock) — VNPAY yêu cầu vnp_ExpireDate theo GMT+7.</summary>
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }

    // Audit Trail — theo đúng convention các entity khác
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
}
