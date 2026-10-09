using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// Quét 3 nhóm bản ghi mượn sắp đến hạn / vừa quá hạn để gửi email nhắc nhở, mỗi ngày 1 lần:
/// (1) PrintBook.BookOut sắp đến hạn trả (DueDate = hôm nay + 2 ngày) → EMAIL_PRINT_DUE_SOON
/// (2) PrintBook.BookOut quá hạn 1 ngày (DueDate = hôm nay - 1 ngày) → EMAIL_PRINT_OVERDUE
/// (3) Ebook.EbookItemLoan sắp hết hạn (ExpiresAt = hôm nay + 2 ngày) → EMAIL_EBOOK_DUE_SOON
/// Đây là job chỉ đọc (không đổi Status như EbookLoanExpiryJob/BookRequestExpiryJob) — chỉ nhắc nhở.
/// Mẫu email tra theo TenantId tường minh (không dùng overload đọc HttpContext vì job không chạy
/// trong ngữ cảnh HTTP request) — cùng khuôn xử lý với EbookLoanExpiryJob/BookRequestExpiryJob.
public class DueSoonReminderJob(
    ELIBAPIDbContext db,
    IEmailService emailService,
    ISystemParameterService sysParam,
    INotificationDispatcher notificationDispatcher,
    ILogger<DueSoonReminderJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var today = DateTime.Now.Date;
        var dueSoonDate = today.AddDays(2);
        var overdueDate = today.AddDays(-1);

        var printDueSoon = await db.BookOuts
            .Where(x => x.IsDelete != 2 && x.Status != "R" && x.DueDate != null && x.DueDate.Value.Date == dueSoonDate)
            .ToListAsync();
        var printOverdue = await db.BookOuts
            .Where(x => x.IsDelete != 2 && x.Status != "R" && x.DueDate != null && x.DueDate.Value.Date == overdueDate)
            .ToListAsync();
        var ebookDueSoon = await db.EbookItemLoans
            .Where(x => x.IsDelete != 2 && x.Status == 1 && x.ExpiresAt != null && x.ExpiresAt.Value.Date == dueSoonDate)
            .ToListAsync();

        if (printDueSoon.Count > 0)
            await SendPrintReminderEmailsAsync(printDueSoon, "EMAIL_PRINT_DUE_SOON", "PRINT_DUE_SOON", "DueDate", "Nhắc nhở sách sắp đến hạn trả");
        if (printOverdue.Count > 0)
            await SendPrintReminderEmailsAsync(printOverdue, "EMAIL_PRINT_OVERDUE", "PRINT_OVERDUE", "ExpiredDate", "Thông báo sách quá hạn trả");
        if (ebookDueSoon.Count > 0)
            await SendEbookReminderEmailsAsync(ebookDueSoon);

        logger.LogInformation(
            "DueSoonReminderJob: {PrintDueSoon} sách in sắp đến hạn, {PrintOverdue} sách in quá hạn 1 ngày, {EbookDueSoon} tài liệu số sắp hết hạn",
            printDueSoon.Count, printOverdue.Count, ebookDueSoon.Count);
    }

    // Không "continue" cả nhóm khi thiếu template email nữa — SMS/Zalo có cờ/template riêng (đọc trong
    // NotificationDispatcher), phải bắn độc lập dù email chưa cấu hình.
    private async Task SendPrintReminderEmailsAsync(List<BookOut> loans, string templateCode, string eventCode, string dateTokenKey, string subject)
    {
        foreach (var group in loans.GroupBy(x => x.TenantId))
        {
            var template = await sysParam.GetValueAsync(templateCode, group.Key);

            foreach (var loan in group)
            {
                if (!loan.ReaderId.HasValue) continue;
                var reader = await db.Readers.FirstOrDefaultAsync(r => r.Id == loan.ReaderId.Value);
                if (reader == null) continue;

                var title = loan.Barcode == null ? null : await (
                    from bc in db.Barcodes
                    join x in db.BibXmls on bc.BibId equals x.BibId
                    // Mã ĐKCB chỉ duy nhất trong 1 đơn vị (Đợt 20) → khớp cả đơn vị của giao dịch.
                    where (bc.BarcodeValue == loan.Barcode && bc.TenantId == loan.TenantId) || bc.Id == loan.Reg_Seq_Id
                    select x.Title
                ).FirstOrDefaultAsync();
                var readerName = $"{reader.LastName} {reader.FirstName}".Trim();

                if (!string.IsNullOrWhiteSpace(template) && !string.IsNullOrWhiteSpace(reader.Email))
                {
                    var body = EmailTemplateHelper.Render(template, new Dictionary<string, string>
                    {
                        ["ReaderName"] = readerName,
                        ["BibTitle"] = title ?? "",
                        [dateTokenKey] = loan.DueDate?.ToString("dd/MM/yyyy HH:mm") ?? "",
                    });
                    await TrySendAsync(reader.Email!, subject, body);
                }

                var tokens = new Dictionary<string, string>
                {
                    ["readerName"] = readerName,
                    ["bibTitle"] = title ?? "",
                };
                await notificationDispatcher.DispatchSmsAsync(group.Key, reader.Phone, eventCode, tokens);
                await notificationDispatcher.DispatchZaloAsync(group.Key, reader.Phone, eventCode, tokens);
            }
        }
    }

    private async Task SendEbookReminderEmailsAsync(List<EbookItemLoan> loans)
    {
        foreach (var group in loans.GroupBy(x => x.TenantId))
        {
            var template = await sysParam.GetValueAsync("EMAIL_EBOOK_DUE_SOON", group.Key);

            foreach (var loan in group)
            {
                var reader = await db.Readers.FirstOrDefaultAsync(r => r.Id == loan.ReaderId);
                if (reader == null) continue;
                var title = await db.EbookItemXmls.Where(x => x.Id == loan.EbookItemId).Select(x => x.Title).FirstOrDefaultAsync();
                var readerName = $"{reader.LastName} {reader.FirstName}".Trim();

                if (!string.IsNullOrWhiteSpace(template) && !string.IsNullOrWhiteSpace(reader.Email))
                {
                    var body = EmailTemplateHelper.Render(template, new Dictionary<string, string>
                    {
                        ["ReaderName"] = readerName,
                        ["EbookTitle"] = title ?? "",
                        ["DueDate"] = loan.ExpiresAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
                    });
                    await TrySendAsync(reader.Email!, "Nhắc nhở tài liệu số sắp hết hạn", body);
                }

                var tokens = new Dictionary<string, string>
                {
                    ["readerName"] = readerName,
                    ["ebookTitle"] = title ?? "",
                };
                await notificationDispatcher.DispatchSmsAsync(group.Key, reader.Phone, "EBOOK_DUE_SOON", tokens);
                await notificationDispatcher.DispatchZaloAsync(group.Key, reader.Phone, "EBOOK_DUE_SOON", tokens);
            }
        }
    }

    private async Task TrySendAsync(string toEmail, string subject, string body)
    {
        try { await emailService.SendAsync(toEmail, subject, body); }
        catch (Exception ex) { logger.LogWarning(ex, "DueSoonReminderJob: gửi email tới {Email} thất bại", toEmail); }
    }
}
