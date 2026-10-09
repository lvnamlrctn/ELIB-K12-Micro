using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

public class EbookReservationReadyNotifier(
    ELIBAPIDbContext db,
    IEmailService emailService,
    ISystemParameterService sysParam,
    INotificationDispatcher notificationDispatcher,
    ILogger<EbookReservationReadyNotifier> logger) : IEbookReservationReadyNotifier
{
    public async Task NotifyAsync(IEnumerable<EbookItemReservation> promoted)
    {
        var list = promoted.ToList();
        if (list.Count == 0) return;

        foreach (var group in list.GroupBy(x => x.TenantId))
        {
            // Không "continue" cả nhóm khi thiếu template email nữa — SMS/Zalo có cờ/template riêng
            // (đọc trong NotificationDispatcher), phải bắn độc lập dù email chưa cấu hình.
            var template = await sysParam.GetValueAsync("EMAIL_EBOOK_RESERVATION_READY", group.Key);

            foreach (var res in group)
            {
                var reader = await db.Readers.FirstOrDefaultAsync(r => r.Id == res.ReaderId);
                if (reader == null) continue;
                var title = await db.EbookItemXmls.Where(x => x.Id == res.EbookItemId).Select(x => x.Title).FirstOrDefaultAsync();
                var readerName = $"{reader.LastName} {reader.FirstName}".Trim();

                if (!string.IsNullOrWhiteSpace(template) && !string.IsNullOrWhiteSpace(reader.Email))
                {
                    var body = EmailTemplateHelper.Render(template, new Dictionary<string, string>
                    {
                        ["ReaderName"] = readerName,
                        ["EbookTitle"] = title ?? "",
                        ["ReadyExpiresAt"] = res.ReadyExpiresAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
                    });

                    try { await emailService.SendAsync(reader.Email!, "Đã đến lượt mượn tài liệu số bạn đặt trước", body); }
                    catch (Exception ex) { logger.LogWarning(ex, "EbookReservationReadyNotifier: gửi email tới {Email} thất bại", reader.Email); }
                }

                var tokens = new Dictionary<string, string>
                {
                    ["readerName"] = readerName,
                    ["ebookTitle"] = title ?? "",
                };
                await notificationDispatcher.DispatchSmsAsync(group.Key, reader.Phone, "EBOOK_RESERVATION_READY", tokens);
                await notificationDispatcher.DispatchZaloAsync(group.Key, reader.Phone, "EBOOK_RESERVATION_READY", tokens);
            }
        }
    }
}
