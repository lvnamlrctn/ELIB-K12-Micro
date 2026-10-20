using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

public sealed record WeeklyHourRow(int Weekday, TimeOnly OpenTime, TimeOnly CloseTime, bool IsClosed);

public sealed record SaveWeeklyHoursRequest(int? Category, IReadOnlyList<WeeklyHourRow> Rows);

public sealed class SpecialDaySearch : CrudSearch
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record SpecialDayRequest(DateOnly Date, int? Category, bool IsClosed, TimeOnly? OpenTime, TimeOnly? CloseTime, string? Reason);

public sealed record SpecialDayDto(long Id, Guid PublicId, DateOnly Date, int? Category, bool IsClosed, TimeOnly? OpenTime, TimeOnly? CloseTime,
    string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Kết quả tra giờ mở cửa cho một ngày + nhóm không gian cụ thể (dùng khi đặt phòng — lát sau).</summary>
public sealed record HoursResolution(bool Closed, TimeOnly? OpenTime, TimeOnly? CloseTime, string? Reason);

/// <summary>
/// Giờ mở cửa hàng tuần + ngày đặc biệt (monolith: RoomOpeningHour/RoomSpecialDay + RoomBookingHours resolver, quyền
/// SPACE_HOURS). Khác bản gốc: không có khái niệm "dòng chung mọi đơn vị" — mỗi đơn vị có dữ liệu riêng, tra không đủ thì rơi về
/// giờ mặc định của <see cref="SpaceBookingSettings"/>.
/// </summary>
public sealed class HoursResource(ICrudDbContext db)
{
    /// <summary>7 dòng Chủ nhật(0)..Thứ bảy(6) — thứ chưa cấu hình riêng thì trả giờ mặc định của đơn vị, không đóng.</summary>
    public async Task<IReadOnlyList<WeeklyHourRow>> GetWeeklyAsync(int? category, CancellationToken ct)
    {
        var rows = await db.Set<RoomOpeningHour>().AsNoTracking().Where(x => x.Category == category).ToListAsync(ct);
        var settings = await GetOrCreateSettingsAsync(ct);
        return Enumerable.Range(0, 7).Select(weekday => rows.Find(r => r.Weekday == weekday) is { } r
            ? new WeeklyHourRow(weekday, r.OpenTime, r.CloseTime, r.IsClosed)
            : new WeeklyHourRow(weekday, settings.DefaultOpenTime, settings.DefaultCloseTime, false)).ToList();
    }

    /// <summary>Thay toàn bộ 7 dòng của một nhóm (xoá-rồi-chèn) — đúng cách monolith lưu cả tuần một lần.</summary>
    public async Task<IReadOnlyList<WeeklyHourRow>> SaveWeeklyAsync(SaveWeeklyHoursRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var weekdays = request.Rows.Select(r => r.Weekday).ToList();
        if (weekdays.Count != 7 || weekdays.Distinct().Count() != 7 || weekdays.Any(w => w is < 0 or > 6))
            throw new BusinessRuleException("WEEKLY_HOURS_INVALID", "Cần đúng 7 dòng, mỗi thứ trong tuần (0-6) một dòng.");
        var category = request.Category is >= 1 and <= 4 ? request.Category : null;
        var existing = await db.Set<RoomOpeningHour>().Where(x => x.Category == category).ToListAsync(ct);
        db.Set<RoomOpeningHour>().RemoveRange(existing);
        foreach (var row in request.Rows)
            db.Set<RoomOpeningHour>().Add(RoomOpeningHour.Create(category, row.Weekday, row.OpenTime, row.CloseTime, row.IsClosed));
        await db.SaveChangesAsync(ct);
        return await GetWeeklyAsync(category, ct);
    }

    /// <summary>Độ ưu tiên: ngày đặc biệt (đúng nhóm &gt; mọi nhóm) &gt; giờ tuần (đúng nhóm &gt; mọi nhóm) &gt; giờ mặc định đơn vị.</summary>
    public async Task<HoursResolution> ResolveAsync(DateOnly date, int category, CancellationToken ct)
    {
        var special = await db.Set<RoomSpecialDay>().AsNoTracking().Where(x => x.Date == date && (x.Category == category || x.Category == null))
            .OrderByDescending(x => x.Category.HasValue).FirstOrDefaultAsync(ct);
        if (special is not null)
            return special.IsClosed ? new HoursResolution(true, null, null, special.Reason) : new HoursResolution(false, special.OpenTime, special.CloseTime, special.Reason);

        var weekday = (int)date.DayOfWeek; // .NET: Chủ nhật=0 .. Thứ bảy=6, khớp đúng quy ước monolith.
        var hour = await db.Set<RoomOpeningHour>().AsNoTracking().Where(x => x.Weekday == weekday && (x.Category == category || x.Category == null))
            .OrderByDescending(x => x.Category.HasValue).FirstOrDefaultAsync(ct);
        if (hour is not null)
            return hour.IsClosed ? new HoursResolution(true, null, null, null) : new HoursResolution(false, hour.OpenTime, hour.CloseTime, null);

        var settings = await GetOrCreateSettingsAsync(ct);
        return new HoursResolution(false, settings.DefaultOpenTime, settings.DefaultCloseTime, null);
    }

    private async Task<SpaceBookingSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        if (await db.Set<SpaceBookingSettings>().AsNoTracking().FirstOrDefaultAsync(ct) is { } existing) return existing;
        var created = SpaceBookingSettings.CreateDefault();
        db.Set<SpaceBookingSettings>().Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}

/// <summary>Ngày đặc biệt (monolith: map.RoomSpecialDay, quyền SPACE_HOURS). Mỗi (Ngày, Nhóm) chỉ một dòng.</summary>
public sealed class SpecialDayResource(ICrudDbContext db) : CrudResource<SpecialDayResource, RoomSpecialDay, SpecialDaySearch, SpecialDayRequest, SpecialDayDto>(db)
{
    protected override string EntityName => "Ngày đặc biệt";

    protected override string? Describe(RoomSpecialDay entity) => $"{entity.Date:dd/MM/yyyy}" + (entity.Reason is { } r ? $" — {r}" : "");

    protected override System.Linq.Expressions.Expression<Func<RoomSpecialDay, SpecialDayDto>> Projection => x =>
        new SpecialDayDto(x.Id, x.PublicId, x.Date, x.Category, x.IsClosed, x.OpenTime, x.CloseTime, x.Reason, x.CreatedAt, x.UpdatedAt);

    protected override RoomSpecialDay Create(SpecialDayRequest request) =>
        RoomSpecialDay.Create(request.Date, request.Category, request.IsClosed, request.OpenTime, request.CloseTime, request.Reason);

    protected override void Update(RoomSpecialDay entity, SpecialDayRequest request) =>
        entity.Update(request.Date, request.Category, request.IsClosed, request.OpenTime, request.CloseTime, request.Reason);

    protected override IQueryable<RoomSpecialDay> Filter(IQueryable<RoomSpecialDay> query, SpecialDaySearch search)
    {
        if (search.From is { } from) query = query.Where(x => x.Date >= from);
        if (search.To is { } to) query = query.Where(x => x.Date <= to);
        return query;
    }

    protected override IOrderedQueryable<RoomSpecialDay> Order(IQueryable<RoomSpecialDay> query) => query.OrderBy(x => x.Date).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(RoomSpecialDay entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Date == entity.Date && x.Category == entity.Category && x.Id != entity.Id, ct))
            throw new ConflictException("SPECIAL_DAY_EXISTS", $"Đã có ngày đặc biệt {entity.Date:dd/MM/yyyy} cho nhóm này.");
    }
}

public sealed record SettingsDto(TimeOnly DefaultOpenTime, TimeOnly DefaultCloseTime, int MaxPerDay, int MaxActive, int MaxSessionMinutes,
    int NoShowLimit, int NoShowWindowDays, int BanDays);

public sealed record UpdateSettingsRequest(TimeOnly DefaultOpenTime, TimeOnly DefaultCloseTime, int MaxPerDay, int MaxActive, int MaxSessionMinutes,
    int NoShowLimit, int NoShowWindowDays, int BanDays);

/// <summary>Tham số đặt phòng của đơn vị (monolith: SystemParameter ROOM_BOOKING_*, quyền SPACE_SETTINGS) — tự seed khi đọc lần đầu.</summary>
public sealed class SettingsResource(ICrudDbContext db)
{
    public async Task<SettingsDto> GetAsync(CancellationToken ct) => ToDto(await GetOrCreateAsync(ct));

    public async Task<SettingsDto> UpdateAsync(UpdateSettingsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await GetOrCreateAsync(ct);
        settings.Update(request.DefaultOpenTime, request.DefaultCloseTime, request.MaxPerDay, request.MaxActive, request.MaxSessionMinutes,
            request.NoShowLimit, request.NoShowWindowDays, request.BanDays);
        await db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    private async Task<SpaceBookingSettings> GetOrCreateAsync(CancellationToken ct)
    {
        if (await db.Set<SpaceBookingSettings>().FirstOrDefaultAsync(ct) is { } existing) return existing;
        var created = SpaceBookingSettings.CreateDefault();
        db.Set<SpaceBookingSettings>().Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    private static SettingsDto ToDto(SpaceBookingSettings s) => new(s.DefaultOpenTime, s.DefaultCloseTime, s.MaxPerDay, s.MaxActive,
        s.MaxSessionMinutes, s.NoShowLimit, s.NoShowWindowDays, s.BanDays);
}
