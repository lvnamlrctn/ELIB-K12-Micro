using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Payment;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELIBAPI.Infrastructure.Services.Payment;

/// <summary>Tạo / đối soát giao dịch thanh toán QR (port ELIB-LRC 09-25). K12: giao dịch mang TenantId của bạn đọc — cổng
/// thanh toán lấy tài khoản nhận tiền của đơn vị đó; thủ thư chỉ tạo được cho bạn đọc thuộc đơn vị mình.</summary>
public class PaymentService(
    ELIBAPIDbContext db,
    IPaymentGatewayResolver gateways,
    IFineSettlementService fineSettlement,
    PaymentConfig config,
    IOptions<PaymentOptions> opts) : IPaymentService
{
    public const string TargetFineTicket  = "FINE_TICKET";
    public const string TargetPhotocopy   = "PHOTOCOPY";
    public const string TargetCardReissue = "CARD_REISSUE";

    public async Task<PaymentTransaction> CreateAsync(long readerId, string targetType, long? targetId, string provider, long? createdBy, long? scopeTenantId)
    {
        var reader = await db.Readers.AsNoTracking()
            .Where(r => r.Id == readerId && r.IsDelete != 2 && (scopeTenantId == null || r.TenantId == scopeTenantId))
            .Select(r => new { r.Id, r.TenantId }).FirstOrDefaultAsync();
        if (reader == null) throw new ArgumentException("Không tìm thấy bạn đọc.");

        var amount = await ResolveAmountAsync(readerId, targetType, targetId, reader.TenantId);
        if (amount <= 0)
            throw new ArgumentException("Không xác định được số tiền cần thanh toán (đã thanh toán đủ hoặc khoản phí không hợp lệ).");

        // Một khoản phí chỉ có 1 QR đang chờ: QR cũ còn hạn (cùng số tiền, cùng cổng) thì trả lại — tránh 2 giao dịch cùng
        // trỏ 1 phiếu được đối soát 2 lần nếu bạn đọc quét cả 2.
        var now = LibraryClock.Now;
        var existing = await db.PaymentTransactions.FirstOrDefaultAsync(x => x.IsDelete != 2 && x.Status == "Pending"
            && x.ReaderId == readerId && x.TargetType == targetType && x.TargetId == targetId && x.ExpiresAt > now);
        if (existing != null)
        {
            if (existing.Amount == amount && string.Equals(existing.Provider, provider, StringComparison.OrdinalIgnoreCase)) return existing;
            existing.Status = "Cancelled"; existing.UpdatedRowDate = now;
        }

        var txn = new PaymentTransaction
        {
            TransactionCode = GenerateCode(),
            ReaderId        = readerId,
            TargetType      = targetType,
            TargetId        = targetId,
            Amount          = amount,
            Provider        = provider,
            Status          = "Pending",
            ExpiresAt       = now.AddMinutes(opts.Value.ExpireMinutes),
            CreatedRowBy    = createdBy,
            CreatedRowDate  = now,
            TenantId        = reader.TenantId,
            PublicId        = Guid.NewGuid()
        };

        var gateway = gateways.Resolve(provider);
        var qr = await gateway.BuildQrAsync(txn);
        txn.QrContent = qr.QrContent;
        txn.Provider  = gateway.Provider;

        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();
        return txn;
    }

    public Task<PaymentTransaction?> GetByPublicIdAsync(Guid publicId) =>
        db.PaymentTransactions.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2);

    public Task<PaymentTransaction?> FindPendingByCodeAsync(string transactionCode) =>
        db.PaymentTransactions.FirstOrDefaultAsync(x =>
            x.TransactionCode == transactionCode && x.Status == "Pending" && x.IsDelete != 2);

    public async Task<PaymentTransaction?> FindPendingByTransferContentAsync(string transferContent, double amount, long? tenantId)
    {
        if (string.IsNullOrWhiteSpace(transferContent)) return null;

        // VietQR only — VNPAY đã đối soát qua IPN (chữ ký + vnp_TxnRef chính xác), không cần suy đoán
        // theo nội dung chuyển khoản như ở đây.
        var candidates = await db.PaymentTransactions
            .Where(x => x.Status == "Pending" && x.Provider == "VietQR" && x.Amount == amount && x.IsDelete != 2
                && (tenantId == null || x.TenantId == tenantId))
            .ToListAsync();

        // Ngân hàng thường bỏ khoảng trắng/ký tự đặc biệt trong nội dung — so khớp trên chuỗi chỉ còn chữ số và chữ cái.
        var normalized = new string(transferContent.Where(char.IsAsciiLetterOrDigit).ToArray());
        return candidates.FirstOrDefault(x => normalized.Contains(x.TransactionCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PaymentTransaction> SettleAsync(long transactionId, string? providerRef)
    {
        var txn = await db.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == transactionId && x.IsDelete != 2);
        if (txn == null) throw new KeyNotFoundException("Không tìm thấy giao dịch thanh toán.");

        // Idempotent — webhook có thể gọi lại nhiều lần cho cùng 1 giao dịch (retry của gateway).
        if (txn.Status == "Paid") return txn;

        var now = LibraryClock.Now;
        txn.Status         = "Paid";
        txn.PaidAt         = now;
        txn.ProviderRef    = providerRef;
        txn.UpdatedRowDate = now;

        switch (txn.TargetType)
        {
            case TargetFineTicket when txn.TargetId.HasValue:
                await fineSettlement.SettleTicketAsync(txn.TargetId.Value, now);
                break;
            case TargetPhotocopy when txn.TargetId.HasValue:
                var photo = await db.CPhotos.FirstOrDefaultAsync(x => x.Id == txn.TargetId.Value && x.IsDelete != 2);
                if (photo != null) { photo.IsPaid = 2; photo.UpdatedRowDate = now; }
                break;
            case TargetCardReissue:
                // Không có bảng nghiệp vụ — chỉ đánh dấu giao dịch Paid ở trên.
                break;
        }

        await db.SaveChangesAsync();
        return txn;
    }

    public async Task<object> MyDebtsAsync(long readerId)
    {
        var tickets = await db.CFineTickets.AsNoTracking()
            .Where(x => x.ReaderId == readerId && x.IsDelete != 2 && x.Status != 2)
            .Select(x => new
            {
                targetType = TargetFineTicket, targetId = x.Id, x.PublicId, x.Code, x.FineDate,
                remaining = (x.TotalAmount ?? 0) - (x.DiscountAmount ?? 0) - (x.PaidAmount ?? 0)
            })
            .Where(x => x.remaining > 0).ToListAsync();
        var photos = await db.CPhotos.AsNoTracking()
            .Where(x => x.Reader_Id == readerId && x.IsDelete != 2 && x.IsPaid != 2 && (x.TotalAmount ?? 0) > 0)
            .Select(x => new { targetType = TargetPhotocopy, targetId = x.Id, x.PublicId, x.PhotoDate, remaining = x.TotalAmount ?? 0 })
            .ToListAsync();
        return new { tickets, photos };
    }

    private async Task<double> ResolveAmountAsync(long readerId, string targetType, long? targetId, long? tenantId)
    {
        switch (targetType)
        {
            case TargetFineTicket:
                var ticket = await db.CFineTickets.FirstOrDefaultAsync(x => x.Id == targetId && x.ReaderId == readerId && x.IsDelete != 2);
                if (ticket == null) throw new ArgumentException("Không tìm thấy phiếu phạt.");
                if (ticket.Status == 2) return 0;
                var remaining = (ticket.TotalAmount ?? 0) - (ticket.DiscountAmount ?? 0) - (ticket.PaidAmount ?? 0);
                return Math.Max(0, remaining);

            case TargetPhotocopy:
                var photo = await db.CPhotos.FirstOrDefaultAsync(x => x.Id == targetId && x.Reader_Id == readerId && x.IsDelete != 2);
                if (photo == null) throw new ArgumentException("Không tìm thấy phiếu phí sao chụp.");
                return photo.IsPaid == 2 ? 0 : photo.TotalAmount ?? 0;

            case TargetCardReissue:
                return await config.CardReissueFeeAsync(tenantId);

            default:
                throw new ArgumentException($"Loại phí không hợp lệ: {targetType}");
        }
    }

    private static string GenerateCode()
    {
        var rand = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..4];
        return $"PT{LibraryClock.Now:yyMMddHHmmss}{rand}";
    }
}
