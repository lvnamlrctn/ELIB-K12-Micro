using System.Net;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Thông báo đặt phòng cho bạn đọc (email + SMS/Zalo qua <see cref="INotificationDispatcher"/>), dùng chung cho repository,
/// job hết hạn và màn quản trị. Mẫu email lấy từ SystemParameter <c>EMAIL_ROOM_BOOKING_*</c>; trống thì dùng mẫu mặc định bên
/// dưới để email vẫn gửi được. Giờ trong thông báo đổi về giờ thư viện (trước đây in thẳng giờ UTC, lệch 7 tiếng), giá trị
/// chèn vào mẫu được mã hoá HTML. Mọi lỗi gửi đều nuốt (best-effort) — gọi SAU khi đã lưu.
/// Port ELIB-LRC 10-04. Tenant (K12): mẫu email đọc theo đơn vị của bạn đọc (đơn vị trước, dùng chung sau — <see cref="RoomBookingParams"/>);
/// SMS/Zalo gửi qua cấu hình của đơn vị đó.
/// </summary>
public class RoomBookingNotifier(ELIBAPIDbContext db, IEmailService email,
    INotificationDispatcher dispatcher, ILogger<RoomBookingNotifier>? logger = null)
{
    public enum Event { Created, Approved, Rejected, Expired, Cancelled, NoShow, Banned }

    private static readonly Dictionary<Event, (string Key, string Code, string Subject, string Body)> Templates = new()
    {
        [Event.Created] = ("EMAIL_ROOM_BOOKING_CREATED", "ROOM_BOOKING_CREATED", "Đã nhận yêu cầu đặt phòng",
            "<p>Chào {ReaderName},</p><p>Thư viện đã nhận yêu cầu đặt phòng <b>{RoomName}</b> từ {StartAt} đến {EndAt}. Yêu cầu đang chờ thủ thư duyệt, bạn sẽ nhận email khi có kết quả.</p>"),
        [Event.Approved] = ("EMAIL_ROOM_BOOKING_APPROVED", "ROOM_BOOKING_APPROVED", "Yêu cầu đặt phòng học nhóm đã được duyệt",
            "<p>Chào {ReaderName},</p><p>Lượt đặt phòng <b>{RoomName}</b> từ {StartAt} đến {EndAt} đã được duyệt. Vui lòng check-in đúng giờ, quá thời gian ân hạn lượt đặt sẽ bị tính vắng mặt.</p>"),
        [Event.Rejected] = ("EMAIL_ROOM_BOOKING_REJECTED", "ROOM_BOOKING_REJECTED", "Yêu cầu đặt phòng học nhóm bị từ chối",
            "<p>Chào {ReaderName},</p><p>Yêu cầu đặt phòng <b>{RoomName}</b> từ {StartAt} đến {EndAt} không được duyệt. {Reason}</p>"),
        [Event.Expired] = ("EMAIL_ROOM_BOOKING_EXPIRED", "ROOM_BOOKING_EXPIRED", "Yêu cầu đặt phòng học nhóm đã hết hạn",
            "<p>Chào {ReaderName},</p><p>Yêu cầu đặt phòng <b>{RoomName}</b> lúc {StartAt} đã hết hạn vì chưa được duyệt trước giờ bắt đầu.</p>"),
        [Event.Cancelled] = ("EMAIL_ROOM_BOOKING_CANCELLED", "ROOM_BOOKING_CANCELLED", "Lượt đặt phòng đã bị huỷ",
            "<p>Chào {ReaderName},</p><p>Lượt đặt phòng <b>{RoomName}</b> từ {StartAt} đến {EndAt} đã được huỷ. {Reason}</p>"),
        [Event.NoShow] = ("EMAIL_ROOM_BOOKING_NOSHOW", "ROOM_BOOKING_NOSHOW", "Bạn đã vắng mặt lượt đặt phòng",
            "<p>Chào {ReaderName},</p><p>Bạn không check-in lượt đặt phòng <b>{RoomName}</b> lúc {StartAt} nên lượt đặt bị tính là vắng mặt và phòng đã được mở cho bạn đọc khác. Vắng mặt nhiều lần có thể bị tạm khoá đặt phòng.</p>"),
        [Event.Banned] = ("EMAIL_ROOM_BOOKING_BANNED", "ROOM_BOOKING_BANNED", "Tài khoản bị tạm khoá đặt phòng",
            "<p>Chào {ReaderName},</p><p>Tài khoản của bạn bị tạm khoá chức năng đặt phòng đến {BannedUntil}. Lý do: {Reason}</p>"),
    };

    /// <summary>Biến chèn được vào mẫu (hiện ở màn quản lý mẫu thông báo).</summary>
    public static readonly string[] Tokens = ["ReaderName", "RoomName", "StartAt", "EndAt", "Reason", "BannedUntil"];

    public static IEnumerable<(Event Event, string Key, string DefaultSubject, string DefaultBody)> Catalog =>
        Templates.Select(t => (t.Key, t.Value.Key, t.Value.Subject, t.Value.Body));

    public static string SubjectKey(string templateKey) => templateKey + "_SUBJECT";

    /// <summary>Gửi thử mẫu hiện hành với dữ liệu mẫu tới 1 địa chỉ email (màn quản lý mẫu thông báo).</summary>
    public async Task<string?> SendTestAsync(Event evt, string to, long? tenantId)
    {
        var (key, _, subject, fallback) = Templates[evt];
        var tokens = new Dictionary<string, string>
        {
            ["ReaderName"] = "Nguyễn Văn A", ["RoomName"] = "Phòng học nhóm 1", ["StartAt"] = "05/10/2026 09:00", ["EndAt"] = "05/10/2026 10:00",
            ["Reason"] = "(lý do mẫu)", ["BannedUntil"] = "12/10/2026 09:00",
        };
        var template = await RoomBookingParams.ValueAsync(db, key, tenantId);
        var customSubject = await RoomBookingParams.ValueAsync(db, SubjectKey(key), tenantId);
        var body = EmailTemplateHelper.Render(string.IsNullOrWhiteSpace(template) ? fallback : template, tokens);
        try { await email.SendAsync(to, "[Thử] " + (string.IsNullOrWhiteSpace(customSubject) ? subject : customSubject.Trim()), body); return null; }
        catch (Exception ex) { return "Gửi email thất bại: " + ex.Message; }
    }

    public Task NotifyAsync(RoomBooking booking, Event evt, string? reason = null) =>
        SendAsync(booking.ReaderId, evt, new Dictionary<string, string>
        {
            ["RoomName"] = "", ["StartAt"] = Format(booking.StartAt), ["EndAt"] = Format(booking.EndAt), ["Reason"] = reason ?? "",
        }, booking.MapObjectId);

    public Task NotifyBanAsync(RoomBookingBan ban) =>
        SendAsync(ban.ReaderId, Event.Banned, new Dictionary<string, string>
        {
            ["BannedUntil"] = Format(ban.BannedUntil), ["Reason"] = ban.Reason ?? "",
        }, null);

    private static string Format(DateTime utc) => RoomBookingHours.ToLibraryLocal(utc).ToString("dd/MM/yyyy HH:mm");

    private async Task SendAsync(long readerId, Event evt, Dictionary<string, string> tokens, long? mapObjectId)
    {
        try
        {
            var reader = await db.Readers.AsNoTracking().Where(x => x.Id == readerId)
                .Select(x => new { x.FirstName, x.LastName, x.Email, x.Phone, x.TenantId }).FirstOrDefaultAsync();
            if (reader == null) return;
            tokens["ReaderName"] = RoomBookingPrivacy.FullName(reader.FirstName, reader.LastName);
            if (mapObjectId != null)
                tokens["RoomName"] = await db.MapObjects.AsNoTracking().Where(o => o.Id == mapObjectId).Select(o => o.Name).FirstOrDefaultAsync() ?? "";

            var (key, code, defaultSubject, fallback) = Templates[evt];
            var subject = defaultSubject;
            if (!string.IsNullOrWhiteSpace(reader.Email))
            {
                var template = await RoomBookingParams.ValueAsync(db, key, reader.TenantId);
                var customSubject = await RoomBookingParams.ValueAsync(db, SubjectKey(key), reader.TenantId);
                if (!string.IsNullOrWhiteSpace(customSubject)) subject = System.Text.RegularExpressions.Regex.Replace(customSubject, "<.*?>", "").Trim();
                var encoded = tokens.ToDictionary(t => t.Key, t => WebUtility.HtmlEncode(t.Value));
                var body = EmailTemplateHelper.Render(string.IsNullOrWhiteSpace(template) ? fallback : template, encoded);
                try { await email.SendAsync(reader.Email!, EmailTemplateHelper.Render(subject, tokens), body); }
                catch (Exception ex) { logger?.LogWarning(ex, "Gửi email {Event} tới {Email} thất bại", evt, reader.Email); }
            }
            await dispatcher.DispatchSmsAsync(reader.TenantId, reader.Phone, code, tokens);
            await dispatcher.DispatchZaloAsync(reader.TenantId, reader.Phone, code, tokens);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "Thông báo đặt phòng {Event} cho bạn đọc {ReaderId} thất bại", evt, readerId); }
    }
}
