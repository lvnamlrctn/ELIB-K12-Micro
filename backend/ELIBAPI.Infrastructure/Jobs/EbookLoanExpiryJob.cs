using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// Quét EbookItemLoan quá hạn (Status=1 Active, ExpiresAt đã qua) → chuyển Status=4 (AutoExpired), rồi
/// với từng EbookItem bị ảnh hưởng, thử thăng hạng người tiếp theo trong hàng đợi đặt trước. Gửi email
/// thông báo hết hạn và email "đến lượt mượn" — mẫu email tra theo TenantId tường minh (không dùng
/// overload đọc HttpContext vì job không chạy trong ngữ cảnh HTTP request).
public class EbookLoanExpiryJob(
    ELIBAPIDbContext db,
    IEbookItemReservationRepository reservationRepo,
    IEmailService emailService,
    ISystemParameterService sysParam,
    ILogger<EbookLoanExpiryJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var now = DateTime.Now;
        var expired = await db.EbookItemLoans
            .Where(x => x.IsDelete != 2 && x.Status == 1 && x.ExpiresAt != null && x.ExpiresAt < now)
            .ToListAsync();

        if (expired.Count == 0)
        {
            logger.LogInformation("EbookLoanExpiryJob: không có khoản mượn nào hết hạn");
            return;
        }

        foreach (var loan in expired)
        {
            loan.Status = 4;
            loan.UpdateRowBy = null;
            loan.UpdatedRowDate = now;
        }
        await db.SaveChangesAsync();

        await SendExpiredEmailsAsync(expired);

        // Thăng hạng người tiếp theo trong hàng đợi đặt trước — PromoteNextIfSlotAvailableAsync tự gửi
        // email "đến lượt mượn" ngay bên trong (điểm chốt duy nhất, dùng chung với mọi luồng thăng hạng
        // khác), không cần job này tự truy vấn lại và gửi email riêng.
        var affectedItemIds = expired.Select(x => x.EbookItemId).Distinct().ToList();
        var promotedCount = 0;
        foreach (var itemId in affectedItemIds)
            promotedCount += await reservationRepo.PromoteNextIfSlotAvailableAsync(itemId);

        logger.LogInformation("EbookLoanExpiryJob: {ExpiredCount} khoản mượn hết hạn, {PromotedCount} đặt trước được thăng hạng",
            expired.Count, promotedCount);
    }

    private async Task SendExpiredEmailsAsync(List<EbookItemLoan> expired)
    {
        foreach (var group in expired.GroupBy(x => x.TenantId))
        {
            var template = await sysParam.GetValueAsync("EMAIL_EBOOK_EXPIRED", group.Key);
            if (string.IsNullOrWhiteSpace(template)) continue;

            foreach (var loan in group)
            {
                var reader = await db.Readers.FirstOrDefaultAsync(r => r.Id == loan.ReaderId);
                if (string.IsNullOrWhiteSpace(reader?.Email)) continue;
                var title = await db.EbookItemXmls.Where(x => x.Id == loan.EbookItemId).Select(x => x.Title).FirstOrDefaultAsync();

                var body = EmailTemplateHelper.Render(template, new Dictionary<string, string>
                {
                    ["ReaderName"] = $"{reader.LastName} {reader.FirstName}".Trim(),
                    ["EbookTitle"] = title ?? "",
                    ["ExpiredDate"] = loan.ExpiresAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
                });
                await TrySendAsync(reader.Email!, "Thông báo hết hạn mượn tài liệu số", body);
            }
        }
    }

    private async Task TrySendAsync(string toEmail, string subject, string body)
    {
        try { await emailService.SendAsync(toEmail, subject, body); }
        catch (Exception ex) { logger.LogWarning(ex, "EbookLoanExpiryJob: gửi email tới {Email} thất bại", toEmail); }
    }
}
