using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

public sealed class HoursApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Weekly_hours_default_to_settings_until_saved()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var weekly = await Read<List<WeeklyHourRow>>(await staff.GetAsync(U("/api/hours/weekly")));
        Assert.Equal(7, weekly.Count);
        Assert.All(weekly, r => Assert.Equal((new TimeOnly(7, 0), new TimeOnly(21, 0), false), (r.OpenTime, r.CloseTime, r.IsClosed)));

        var rows = Enumerable.Range(0, 7).Select(w => new WeeklyHourRow(w, new TimeOnly(8, 0), new TimeOnly(17, 0), w == 0)).ToList();
        var saved = await Read<List<WeeklyHourRow>>(await staff.PutAsJsonAsync(U("/api/hours/weekly"), new SaveWeeklyHoursRequest(null, rows), Json));
        Assert.True(saved.Single(r => r.Weekday == 0).IsClosed);
        Assert.Equal(new TimeOnly(8, 0), saved.Single(r => r.Weekday == 1).OpenTime);

        var reread = await Read<List<WeeklyHourRow>>(await staff.GetAsync(U("/api/hours/weekly")));
        Assert.True(reread.Single(r => r.Weekday == 0).IsClosed);
    }

    [Fact]
    public async Task Saving_weekly_hours_requires_exactly_seven_distinct_weekdays()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var rows = Enumerable.Range(0, 6).Select(w => new WeeklyHourRow(w, new TimeOnly(8, 0), new TimeOnly(17, 0), false)).ToList();

        var response = await staff.PutAsJsonAsync(U("/api/hours/weekly"), new SaveWeeklyHoursRequest(null, rows), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.BadRequest, "WEEKLY_HOURS_INVALID"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Special_days_cannot_duplicate_the_same_date_and_category()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var date = new DateOnly(2026, 2, 1);
        await staff.PostAsJsonAsync(U("/api/special-days/Add"), new SpecialDayRequest(date, null, true, null, null, "Tết"), Json);

        var dup = await staff.PostAsJsonAsync(U("/api/special-days/Add"), new SpecialDayRequest(date, null, true, null, null, "Tết (trùng)"), Json);
        var (status, code, _) = await Error(dup);
        Assert.Equal((HttpStatusCode.Conflict, "SPECIAL_DAY_EXISTS"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Settings_round_trip_and_validate()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var initial = await Read<SettingsDto>(await staff.GetAsync(U("/api/settings")));
        Assert.Equal((new TimeOnly(7, 0), new TimeOnly(21, 0), 0), (initial.DefaultOpenTime, initial.DefaultCloseTime, initial.MaxPerDay));

        var updated = await Read<SettingsDto>(await staff.PutAsJsonAsync(U("/api/settings"),
            new UpdateSettingsRequest(new TimeOnly(6, 30), new TimeOnly(22, 0), 3, 2, 240, 2, 14, 7), Json));
        Assert.Equal((3, 2, 2), (updated.MaxPerDay, updated.MaxActive, updated.NoShowLimit));

        var invalid = await staff.PutAsJsonAsync(U("/api/settings"),
            new UpdateSettingsRequest(new TimeOnly(22, 0), new TimeOnly(6, 30), 3, 2, 240, 2, 14, 7), Json);
        var (status, code, _) = await Error(invalid);
        Assert.Equal((HttpStatusCode.BadRequest, "HOURS_INVALID"), ((HttpStatusCode)status, code));
    }
}
