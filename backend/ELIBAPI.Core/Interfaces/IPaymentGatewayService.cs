using ELIBAPI.Core.Entities.Payment;

namespace ELIBAPI.Core.Interfaces;

public record PaymentQrResult(string QrContent, string? PayUrl);

/// <summary>Sinh nội dung QR/link thanh toán cho 1 giao dịch — 1 implementation cho mỗi Provider
/// ("VietQR"/"VNPAY"), lựa chọn qua IPaymentGatewayResolver. Cấu hình lấy theo txn.TenantId.</summary>
public interface IPaymentGatewayService
{
    string Provider { get; }
    /// <summary>Đơn vị đã khai báo đủ thông tin cho cổng này chưa (tài khoản nhận / mã merchant).</summary>
    Task<bool> IsConfiguredAsync(long? tenantId);
    Task<PaymentQrResult> BuildQrAsync(PaymentTransaction txn);
}

public interface IPaymentGatewayResolver
{
    IPaymentGatewayService Resolve(string provider);
    /// <summary>Các cổng đơn vị đã cấu hình — để giao diện chỉ hiện lựa chọn dùng được.</summary>
    Task<List<string>> AvailableAsync(long? tenantId);
}

public interface IPaymentService
{
    /// <summary>Tạo giao dịch cho bạn đọc. <paramref name="scopeTenantId"/> = đơn vị của người thao tác (null = tài khoản hệ
    /// thống, không giới hạn); bạn đọc khác đơn vị → ArgumentException "không tìm thấy".</summary>
    Task<PaymentTransaction> CreateAsync(long readerId, string targetType, long? targetId, string provider, long? createdBy, long? scopeTenantId);
    Task<PaymentTransaction?> GetByPublicIdAsync(Guid publicId);
    Task<PaymentTransaction> SettleAsync(long transactionId, string? providerRef);
    Task<PaymentTransaction?> FindPendingByCodeAsync(string transactionCode);
    /// <summary>Khớp giao dịch VietQR Pending có TransactionCode xuất hiện trong nội dung chuyển khoản
    /// ngân hàng (Sepay chỉ trả nội dung thô, không tách riêng mã) và đúng số tiền. <paramref name="tenantId"/> có giá trị
    /// thì chỉ khớp giao dịch của đơn vị đó (webhook Sepay riêng của đơn vị).</summary>
    Task<PaymentTransaction?> FindPendingByTransferContentAsync(string transferContent, double amount, long? tenantId);
    /// <summary>Khoản phạt / phí sao chụp chưa thanh toán của bạn đọc — tab "Phí / Nợ của tôi" trên OPAC.</summary>
    Task<object> MyDebtsAsync(long readerId);
}
