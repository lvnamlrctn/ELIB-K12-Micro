using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Cấu hình chung của đặt phòng cho thủ thư: tham số (SystemParameter ROOM_BOOKING_*), giờ mở cửa theo thứ / loại cơ sở
/// (map.RoomOpeningHour) và ngày đặc biệt (map.RoomSpecialDay). Đổi ngày đặc biệt trả về các lượt đã đặt bị ảnh hưởng (chờ
/// duyệt / đã duyệt nằm ngoài giờ mới); cancelAffected = true thì huỷ các lượt đó và báo bạn đọc.
/// Port ELIB-LRC 10-04. Tenant (K12): mọi cấu hình theo đơn vị — <c>tenantId</c> là cấp đang sửa (đơn vị của thủ thư; tài khoản hệ thống
/// không chọn đơn vị = cấu hình dùng chung TenantId null, áp cho đơn vị nào chưa khai báo riêng). Đọc tham số/mẫu trả giá trị hiệu lực
/// (đơn vị → dùng chung → mặc định); giờ theo thứ và ngày đặc biệt chỉ liệt kê/sửa dòng đúng cấp. Lượt đặt bị ảnh hưởng, tạm ngưng
/// phòng chỉ trong đơn vị đó.
/// </summary>
public class RoomBookingAdminService(ELIBAPIDbContext db, RoomBookingNotifier notifier)
{
    // ── Tham số ────────────────────────────────────────────────────────────

    public async Task<RoomBookingSettings> GetSettingsAsync(long? tenantId)
    {
        var (open, close) = await RoomBookingHours.GetAsync(db, tenantId);
        var r = await RoomBookingRules.GetAsync(db, tenantId);
        return new RoomBookingSettings
        {
            OpenTime = RoomBookingHours.Format(open), CloseTime = RoomBookingHours.Format(close),
            MaxPerDay = r.MaxPerDay, MaxActive = r.MaxActive, MaxSessionMinutes = r.MaxSessionMinutes,
            NoShowLimit = r.NoShowLimit, NoShowWindowDays = r.NoShowWindowDays, BanDays = r.BanDays,
        };
    }

    public async Task<ServiceResult<RoomBookingSettings>> SaveSettingsAsync(RoomBookingSettings s, long userId, long? tenantId)
    {
        if (RoomBookingHours.Window(s.OpenTime, s.CloseTime) == null)
            return ServiceResult<RoomBookingSettings>.BadRequest("Giờ mở/đóng cửa phải dạng HH:mm và giờ mở trước giờ đóng.");
        if (s.MaxPerDay < 0 || s.MaxActive < 0 || s.NoShowLimit < 0)
            return ServiceResult<RoomBookingSettings>.BadRequest("Các giới hạn phải là số không âm (0 = không giới hạn).");
        if (s.MaxSessionMinutes != 0 && (s.MaxSessionMinutes < RoomBookingHours.StepMinutes || s.MaxSessionMinutes % RoomBookingHours.StepMinutes != 0))
            return ServiceResult<RoomBookingSettings>.BadRequest($"Thời lượng tối đa mỗi phiên phải là bội số của {RoomBookingHours.StepMinutes} phút (0 = không giới hạn).");
        if (s.NoShowWindowDays is < 1 or > 365 || s.BanDays is < 1 or > 365)
            return ServiceResult<RoomBookingSettings>.BadRequest("Số ngày đếm vắng mặt và số ngày khoá phải từ 1 đến 365.");

        var values = new (string Code, string Value, string Description)[]
        {
            ("ROOM_BOOKING_OPEN_TIME", s.OpenTime!.Trim(), "Giờ mở cửa đặt phòng mặc định (HH:mm)"),
            ("ROOM_BOOKING_CLOSE_TIME", s.CloseTime!.Trim(), "Giờ đóng cửa đặt phòng mặc định (HH:mm)"),
            (RoomBookingRules.MaxPerDayKey, $"{s.MaxPerDay}", "Số lượt đặt phòng tối đa mỗi bạn đọc mỗi ngày (0 = không giới hạn)"),
            (RoomBookingRules.MaxActiveKey, $"{s.MaxActive}", "Số lượt đặt phòng sắp tới tối đa mỗi bạn đọc (0 = không giới hạn)"),
            (RoomBookingRules.MaxSessionMinutesKey, $"{s.MaxSessionMinutes}", "Thời lượng tối đa mỗi phiên đặt phòng, phút (0 = không giới hạn)"),
            (RoomBookingRules.NoShowLimitKey, $"{s.NoShowLimit}", "Số lần vắng mặt thì tự khoá đặt phòng (0 = tắt)"),
            (RoomBookingRules.NoShowWindowDaysKey, $"{s.NoShowWindowDays}", "Số ngày gần nhất dùng để đếm số lần vắng mặt"),
            (RoomBookingRules.BanDaysKey, $"{s.BanDays}", "Số ngày khoá đặt phòng khi vắng mặt quá số lần cho phép"),
        };
        await UpsertParamsAsync(values, userId, tenantId);
        return ServiceResult<RoomBookingSettings>.Ok(await GetSettingsAsync(tenantId));
    }

    /// <summary>Ghi (thêm/sửa) SystemParameter theo Code (không phân biệt hoa thường) ở đúng cấp đơn vị <paramref name="tenantId"/>.</summary>
    private async Task UpsertParamsAsync(IEnumerable<(string Code, string Value, string Description)> values, long userId, long? tenantId)
    {
        var list = values.ToList();
        var codes = list.Select(v => v.Code.ToLower()).ToList();
        var rows = await db.SystemParameters.Where(x => x.IsDelete != 2 && x.TenantId == tenantId && codes.Contains(x.Code!.ToLower())).ToListAsync();
        var now = LibraryClock.Now;
        foreach (var (code, value, description) in list)
        {
            var row = rows.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            if (row == null)
                db.SystemParameters.Add(new SystemParameter
                {
                    Code = code, Value = value, DescriptionVn = description, IsDelete = 1,
                    CreatedRowBy = userId, CreatedRowDate = now, TenantId = tenantId, PublicId = Guid.NewGuid(),
                });
            else if (row.Value != value)
            {
                row.Value = value; row.UpdateRowBy = userId; row.UpdatedRowDate = now;
            }
        }
        await db.SaveChangesAsync();
    }

    // ── Mẫu thông báo email ──────────────────────────────────────────────

    public async Task<List<NotificationTemplateView>> TemplatesAsync(long? tenantId)
    {
        var result = new List<NotificationTemplateView>();
        foreach (var (evt, key, defaultSubject, defaultBody) in RoomBookingNotifier.Catalog)
        {
            var body = await RoomBookingParams.ValueAsync(db, key, tenantId);
            var subject = await RoomBookingParams.ValueAsync(db, RoomBookingNotifier.SubjectKey(key), tenantId);
            result.Add(new NotificationTemplateView(evt.ToString(), key,
                string.IsNullOrWhiteSpace(subject) ? defaultSubject : subject.Trim(),
                string.IsNullOrWhiteSpace(body) ? defaultBody : body,
                string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(body), defaultSubject, defaultBody));
        }
        return result;
    }

    /// <summary>Lưu mẫu. reset = true: xoá nội dung đã sửa để dùng lại mẫu mặc định.</summary>
    public async Task<ServiceResult<List<NotificationTemplateView>>> SaveTemplateAsync(string eventName, TemplateInput input, bool reset, long userId, long? tenantId)
    {
        if (!Enum.TryParse<RoomBookingNotifier.Event>(eventName, true, out var evt))
            return ServiceResult<List<NotificationTemplateView>>.NotFound("Không có loại thông báo này.");
        var (_, key, _, _) = RoomBookingNotifier.Catalog.First(c => c.Event == evt);
        var subject = reset ? "" : (input.Subject ?? "").Trim();
        var body = reset ? "" : (input.Body ?? "").Trim();
        if (!reset && (subject.Length == 0 || body.Length == 0))
            return ServiceResult<List<NotificationTemplateView>>.BadRequest("Tiêu đề và nội dung không được để trống.");
        if (subject.Length > 300) return ServiceResult<List<NotificationTemplateView>>.BadRequest("Tiêu đề tối đa 300 ký tự.");
        await UpsertParamsAsync([
            (RoomBookingNotifier.SubjectKey(key), subject, "Tiêu đề email đặt phòng (" + evt + ")"),
            (key, body, "Mẫu email đặt phòng (" + evt + ")"),
        ], userId, tenantId);
        return ServiceResult<List<NotificationTemplateView>>.Ok(await TemplatesAsync(tenantId));
    }

    public async Task<ServiceResult<bool>> SendTestTemplateAsync(string eventName, string? to, long userId, long? tenantId)
    {
        if (!Enum.TryParse<RoomBookingNotifier.Event>(eventName, true, out var evt))
            return ServiceResult<bool>.NotFound("Không có loại thông báo này.");
        var address = string.IsNullOrWhiteSpace(to)
            ? await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync()
            : to.Trim();
        if (string.IsNullOrWhiteSpace(address) || !System.Net.Mail.MailAddress.TryCreate(address, out _))
            return ServiceResult<bool>.BadRequest("Vui lòng nhập email nhận thử (tài khoản của bạn chưa có email).");
        var error = await notifier.SendTestAsync(evt, address, tenantId);
        return error == null ? ServiceResult<bool>.Ok(true) : ServiceResult<bool>.BadRequest(error);
    }

    // ── Giờ mở cửa theo thứ ────────────────────────────────────────────────

    public Task<List<RoomOpeningHour>> OpeningHoursAsync(long? tenantId) =>
        db.RoomOpeningHours.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Category).ThenBy(x => x.Weekday).ToListAsync();

    /// <summary>Thay toàn bộ giờ theo thứ của 1 loại cơ sở (null = mọi loại). Dòng không đóng cửa mà để trống giờ = không khai
    /// báo (dùng mức chung) nên không lưu.</summary>
    public async Task<ServiceResult<List<RoomOpeningHour>>> SaveOpeningHoursAsync(int? category, List<OpeningHourInput> input, long userId, long? tenantId)
    {
        if (input.Any(x => x.Weekday is < 0 or > 6) || input.GroupBy(x => x.Weekday).Any(g => g.Count() > 1))
            return ServiceResult<List<RoomOpeningHour>>.BadRequest("Mỗi thứ trong tuần (0–6) chỉ được khai báo một lần.");
        var keep = new List<RoomOpeningHour>();
        foreach (var x in input)
        {
            var empty = string.IsNullOrWhiteSpace(x.OpenTime) && string.IsNullOrWhiteSpace(x.CloseTime);
            if (!x.IsClosed && empty) continue;
            if (!x.IsClosed && RoomBookingHours.Window(x.OpenTime, x.CloseTime) == null)
                return ServiceResult<List<RoomOpeningHour>>.BadRequest($"{WeekdayName(x.Weekday)}: giờ mở/đóng phải dạng HH:mm và giờ mở trước giờ đóng.");
            keep.Add(new RoomOpeningHour
            {
                Category = category, Weekday = x.Weekday, IsClosed = x.IsClosed,
                OpenTime = x.IsClosed ? null : x.OpenTime!.Trim(), CloseTime = x.IsClosed ? null : x.CloseTime!.Trim(),
                UpdateRowBy = userId, UpdatedRowDate = LibraryClock.Now, TenantId = tenantId,
            });
        }
        db.RoomOpeningHours.RemoveRange(await db.RoomOpeningHours.Where(x => x.Category == category && x.TenantId == tenantId).ToListAsync());
        db.RoomOpeningHours.AddRange(keep);
        await db.SaveChangesAsync();
        return ServiceResult<List<RoomOpeningHour>>.Ok(await OpeningHoursAsync(tenantId));
    }

    private static string WeekdayName(int d) => d == 0 ? "Chủ nhật" : $"Thứ {d + 1}";

    // ── Ngày đặc biệt ──────────────────────────────────────────────────────

    public Task<List<RoomSpecialDay>> SpecialDaysAsync(DateTime? from, long? tenantId) =>
        db.RoomSpecialDays.AsNoTracking()
            .Where(x => x.TenantId == tenantId && (from == null || x.Date >= from.Value.Date))
            .OrderBy(x => x.Date).ThenBy(x => x.Category).ToListAsync();

    /// <summary>Thêm (publicId null) hoặc sửa ngày đặc biệt. Preview = true: chỉ trả lượt bị ảnh hưởng, không lưu.</summary>
    public async Task<ServiceResult<SpecialDayResult>> SaveSpecialDayAsync(Guid? publicId, SpecialDayInput input, bool preview, long userId, long? tenantId)
    {
        if (input.Date == null) return ServiceResult<SpecialDayResult>.BadRequest("Vui lòng chọn ngày.");
        if (!input.IsClosed && RoomBookingHours.Window(input.OpenTime, input.CloseTime) == null)
            return ServiceResult<SpecialDayResult>.BadRequest("Giờ mở/đóng phải dạng HH:mm và giờ mở trước giờ đóng (hoặc chọn đóng cửa cả ngày).");
        var date = input.Date.Value.Date;

        RoomSpecialDay? row = null;
        if (publicId != null)
        {
            row = await db.RoomSpecialDays.FirstOrDefaultAsync(x => x.PublicId == publicId && x.TenantId == tenantId);
            if (row == null) return ServiceResult<SpecialDayResult>.NotFound("Không tìm thấy ngày đặc biệt.");
        }
        if (await db.RoomSpecialDays.AnyAsync(x => x.Date >= date && x.Date < date.AddDays(1) && x.Category == input.Category && x.TenantId == tenantId
                && (row == null || x.Id != row.Id)))
            return ServiceResult<SpecialDayResult>.BadRequest("Ngày này đã được khai báo cho cùng loại cơ sở.");

        var window = input.IsClosed ? null : RoomBookingHours.Window(input.OpenTime, input.CloseTime);
        var affected = await AffectedAsync(date, input.Category, window, tenantId);
        if (preview) return ServiceResult<SpecialDayResult>.Ok(new SpecialDayResult(null, affected, 0));

        row ??= db.RoomSpecialDays.Add(new RoomSpecialDay { CreatedRowBy = userId, CreatedRowDate = LibraryClock.Now, TenantId = tenantId, PublicId = Guid.NewGuid() }).Entity;
        row.Date = date;
        row.Category = input.Category;
        row.IsClosed = input.IsClosed;
        row.OpenTime = input.IsClosed ? null : input.OpenTime!.Trim();
        row.CloseTime = input.IsClosed ? null : input.CloseTime!.Trim();
        row.Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        await db.SaveChangesAsync();

        var cancelled = input.CancelAffected ? await CancelAsync(affected, row.Reason, userId) : 0;
        return ServiceResult<SpecialDayResult>.Ok(new SpecialDayResult(row, cancelled > 0 ? await AffectedAsync(date, input.Category, window, tenantId) : affected, cancelled));
    }

    public async Task<ServiceResult<bool>> DeleteSpecialDayAsync(Guid publicId, long? tenantId)
    {
        var row = await db.RoomSpecialDays.FirstOrDefaultAsync(x => x.PublicId == publicId && x.TenantId == tenantId);
        if (row == null) return ServiceResult<bool>.NotFound("Không tìm thấy ngày đặc biệt.");
        db.RoomSpecialDays.Remove(row);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>Lượt chờ duyệt/đã duyệt trong ngày (giờ thư viện) của các phòng thuộc loại cơ sở, nằm ngoài giờ mới
    /// (window null = đóng cả ngày). Tenant: lượt của đơn vị đang sửa; cấu hình dùng chung (tenantId null) ảnh hưởng mọi đơn vị.</summary>
    private async Task<List<AffectedBooking>> AffectedAsync(DateTime date, int? category, (TimeSpan Open, TimeSpan Close)? window, long? tenantId)
    {
        var (dayStart, dayEnd) = RoomBookingHours.LibraryDayUtcRange(date);
        var rows = await (
            from b in db.RoomBookings.AsNoTracking()
            where b.IsDelete != 2 && (b.Status == 1 || b.Status == 2) && b.StartAt >= dayStart && b.StartAt < dayEnd
                && (tenantId == null || b.TenantId == tenantId)
            join o in db.MapObjects.AsNoTracking() on b.MapObjectId equals o.Id
            where category == null || o.Category == category
            join r in db.Readers.AsNoTracking() on b.ReaderId equals r.Id into rj
            from r in rj.DefaultIfEmpty()
            orderby b.StartAt
            select new { b.PublicId, b.MapObjectId, RoomName = o.Name, b.StartAt, b.EndAt, b.Status,
                FirstName = r != null ? r.FirstName : null, LastName = r != null ? r.LastName : null, Cardno = r != null ? r.Cardno : null }).ToListAsync();
        return rows.Where(b => window == null
                || RoomBookingHours.ToLibraryLocal(b.StartAt).TimeOfDay < window.Value.Open
                || RoomBookingHours.ToLibraryLocal(b.EndAt).TimeOfDay > window.Value.Close)
            .Select(b => new AffectedBooking(b.PublicId, b.RoomName, DateTime.SpecifyKind(b.StartAt, DateTimeKind.Utc),
                DateTime.SpecifyKind(b.EndAt, DateTimeKind.Utc), b.Status, RoomBookingPrivacy.FullName(b.FirstName, b.LastName), b.Cardno)).ToList();
    }

    // ── Tạm ngưng phòng (bảo trì) ────────────────────────────────────────

    /// <summary>Bật/tắt tạm ngưng phòng. Bật thì trả các lượt chờ duyệt/đã duyệt chưa kết thúc của phòng; preview = chỉ xem,
    /// cancelAffected = huỷ các lượt đó và báo bạn đọc. Tenant: cấu hình phòng phải thuộc phạm vi đơn vị của thủ thư.</summary>
    public async Task<ServiceResult<SpecialDayResult>> SetMaintenanceAsync(Guid configPublicId, MaintenanceInput input, bool preview, long userId, TenantScope scope)
    {
        var config = await db.RoomBookingConfigs.FirstOrDefaultAsync(c => c.PublicId == configPublicId && c.IsDelete != 2
            && (scope.All || c.TenantId == scope.TenantId || (scope.IncludeShared && c.TenantId == null)));
        if (config == null) return ServiceResult<SpecialDayResult>.NotFound("Không tìm thấy cấu hình phòng.");
        var affected = new List<AffectedBooking>();
        if (input.On)
        {
            var now = DateTime.UtcNow;
            var rows = await (
                from b in db.RoomBookings.AsNoTracking()
                where b.MapObjectId == config.MapObjectId && b.TenantId == config.TenantId && b.IsDelete != 2 && (b.Status == 1 || b.Status == 2) && b.EndAt > now
                join o in db.MapObjects.AsNoTracking() on b.MapObjectId equals o.Id
                join r in db.Readers.AsNoTracking() on b.ReaderId equals r.Id into rj
                from r in rj.DefaultIfEmpty()
                orderby b.StartAt
                select new { b.PublicId, RoomName = o.Name, b.StartAt, b.EndAt, b.Status,
                    FirstName = r != null ? r.FirstName : null, LastName = r != null ? r.LastName : null, Cardno = r != null ? r.Cardno : null }).ToListAsync();
            affected = rows.Select(b => new AffectedBooking(b.PublicId, b.RoomName, DateTime.SpecifyKind(b.StartAt, DateTimeKind.Utc),
                DateTime.SpecifyKind(b.EndAt, DateTimeKind.Utc), b.Status, RoomBookingPrivacy.FullName(b.FirstName, b.LastName), b.Cardno)).ToList();
        }
        if (preview) return ServiceResult<SpecialDayResult>.Ok(new SpecialDayResult(null, affected, 0));

        config.Maintenance = input.On;
        config.MaintenanceNote = input.On && !string.IsNullOrWhiteSpace(input.Note) ? input.Note.Trim() : null;
        config.UpdateRowBy = userId;
        config.UpdatedRowDate = LibraryClock.Now;
        await db.SaveChangesAsync();
        var cancelled = input.On && input.CancelAffected
            ? await CancelAsync(affected, "Phòng tạm ngưng phục vụ" + (config.MaintenanceNote == null ? "" : $" ({config.MaintenanceNote})"), userId, "")
            : 0;
        return ServiceResult<SpecialDayResult>.Ok(new SpecialDayResult(null, cancelled > 0 ? [] : affected, cancelled));
    }

    private async Task<int> CancelAsync(List<AffectedBooking> affected, string? reason, long userId, string? prefix = null)
    {
        var ids = affected.Select(a => a.PublicId).ToList();
        var bookings = await db.RoomBookings.Where(b => ids.Contains(b.PublicId) && b.IsDelete != 2 && (b.Status == 1 || b.Status == 2)).ToListAsync();
        var now = DateTime.UtcNow;
        var note = prefix != null ? reason ?? "" : "Thư viện thay đổi giờ mở cửa" + (string.IsNullOrWhiteSpace(reason) ? "" : $": {reason}");
        foreach (var b in bookings)
        {
            b.Status = 5; b.CancelledAt = now; b.Note = note; b.UpdateRowBy = userId; b.UpdatedRowDate = now;
        }
        await db.SaveChangesAsync();
        foreach (var b in bookings) await notifier.NotifyAsync(b, RoomBookingNotifier.Event.Cancelled, $"Lý do: {note}.");
        return bookings.Count;
    }
}

public sealed class RoomBookingSettings
{
    public string? OpenTime          { get; set; }
    public string? CloseTime         { get; set; }
    public int     MaxPerDay         { get; set; }
    public int     MaxActive         { get; set; }
    public int     MaxSessionMinutes { get; set; }
    public int     NoShowLimit       { get; set; }
    public int     NoShowWindowDays  { get; set; }
    public int     BanDays           { get; set; }
}

public sealed class OpeningHourInput
{
    public int     Weekday   { get; set; }
    public string? OpenTime  { get; set; }
    public string? CloseTime { get; set; }
    public bool    IsClosed  { get; set; }
}

public sealed class SpecialDayInput
{
    public DateTime? Date           { get; set; }
    public int?      Category       { get; set; }
    public bool      IsClosed       { get; set; }
    public string?   OpenTime       { get; set; }
    public string?   CloseTime      { get; set; }
    public string?   Reason         { get; set; }
    public bool      CancelAffected { get; set; }
}

public sealed record NotificationTemplateView(string Event, string Key, string Subject, string Body, bool IsDefault, string DefaultSubject, string DefaultBody);

public sealed class TemplateInput
{
    public string? Subject { get; set; }
    public string? Body    { get; set; }
    public string? To      { get; set; }
}

public sealed class MaintenanceInput
{
    public bool    On             { get; set; }
    public string? Note           { get; set; }
    public bool    CancelAffected { get; set; }
}

public sealed record AffectedBooking(Guid PublicId, string? RoomName, DateTime StartAt, DateTime EndAt, int Status, string ReaderName, string? ReaderCardNo);

public sealed record SpecialDayResult(RoomSpecialDay? Day, List<AffectedBooking> Affected, int Cancelled);
