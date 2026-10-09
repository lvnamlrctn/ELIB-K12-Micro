using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>Quét PaymentTransaction Pending quá ExpiresAt → Expired, để QR hết hạn không còn hiện là
/// "đang chờ" mãi trên UI (và tránh SettleAsync khớp nhầm 1 giao dịch cũ nếu ngân hàng trả chậm). Chạy chung mọi đơn vị —
/// chỉ đổi trạng thái theo hạn của chính giao dịch, không đọc cấu hình đơn vị.</summary>
public class PaymentExpiryJob(ELIBAPIDbContext db, ILogger<PaymentExpiryJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var now = LibraryClock.Now;
        var changed = await db.PaymentTransactions
            .Where(x => x.IsDelete != 2 && x.Status == "Pending" && x.ExpiresAt < now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Expired").SetProperty(x => x.UpdatedRowDate, now));
        if (changed > 0)
            logger.LogInformation("PaymentExpiryJob: chuyển {Count} giao dịch Pending quá hạn sang Expired", changed);
    }
}
