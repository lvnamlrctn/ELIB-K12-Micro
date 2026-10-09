using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// Quét PrintBook.BookRequest đang "pending" đã quá DueDate → chuyển "R" (Rejected, cùng mã
/// CirculationRequestController.Reject dùng). Không cần đụng entity nào khác để nhả chỗ — bản sao tự
/// động rảnh cho hold tiếp theo vì MyLibraryController.Hold chỉ loại các request Status=="pending".
/// Mẫu email tra theo TenantId tường minh (không dùng overload đọc HttpContext vì job không chạy
/// trong ngữ cảnh HTTP request) — xem EbookLoanExpiryJob cho cùng khuôn xử lý.
public class BookRequestExpiryJob(
    ELIBAPIDbContext db,
    IEmailService emailService,
    ISystemParameterService sysParam,
    ILogger<BookRequestExpiryJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var now = DateTime.Now;
        var expired = await db.BookRequests
            .Where(x => x.IsDelete != 2 && x.Status == "pending" && x.DueDate != null && x.DueDate < now)
            .ToListAsync();

        if (expired.Count == 0)
        {
            logger.LogInformation("BookRequestExpiryJob: không có yêu cầu giữ chỗ nào quá hạn");
            return;
        }

        foreach (var req in expired)
        {
            req.Status = "R";
            req.UpdateRowBy = null;
            req.UpdatedRowDate = now;
        }
        await db.SaveChangesAsync();

        await SendExpiredEmailsAsync(expired);

        logger.LogInformation("BookRequestExpiryJob: {Count} yêu cầu giữ chỗ quá hạn đã bị huỷ", expired.Count);
    }

    private async Task SendExpiredEmailsAsync(List<BookRequest> expired)
    {
        foreach (var group in expired.GroupBy(x => x.TenantId))
        {
            var template = await sysParam.GetValueAsync("EMAIL_PRINT_HOLD_EXPIRED", group.Key);
            if (string.IsNullOrWhiteSpace(template)) continue;

            foreach (var req in group)
            {
                if (!req.ReaderId.HasValue) continue;
                var reader = await db.Readers.FirstOrDefaultAsync(r => r.Id == req.ReaderId.Value);
                if (string.IsNullOrWhiteSpace(reader?.Email)) continue;

                var title = req.Barcode == null ? null : await (
                    from bc in db.Barcodes
                    join x in db.BibXmls on bc.BibId equals x.BibId
                    // Mã ĐKCB chỉ duy nhất trong 1 đơn vị (Đợt 20) → khớp cả đơn vị của giao dịch.
                    where (bc.BarcodeValue == req.Barcode && bc.TenantId == req.TenantId)
                    select x.Title
                ).FirstOrDefaultAsync();

                var body = EmailTemplateHelper.Render(template, new Dictionary<string, string>
                {
                    ["ReaderName"] = $"{reader.LastName} {reader.FirstName}".Trim(),
                    ["BibTitle"] = title ?? "",
                    ["ExpiredDate"] = req.DueDate?.ToString("dd/MM/yyyy HH:mm") ?? "",
                });

                try { await emailService.SendAsync(reader.Email!, "Thông báo huỷ giữ chỗ sách quá hạn", body); }
                catch (Exception ex) { logger.LogWarning(ex, "BookRequestExpiryJob: gửi email tới {Email} thất bại", reader.Email); }
            }
        }
    }
}
