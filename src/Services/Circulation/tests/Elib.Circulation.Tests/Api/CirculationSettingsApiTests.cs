using System.Net.Http.Json;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;

namespace Elib.Circulation.Tests.Api;

public sealed class CirculationSettingsApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    [Theory]
    [InlineData(null, null, false, OverdueMethod.Calendar)]
    [InlineData("1", "1", true, OverdueMethod.SkipWeekends)]
    [InlineData("<p>1</p>", "<p>2</p>", true, OverdueMethod.SkipSundays)]
    [InlineData(" 0 ", "3", false, OverdueMethod.Calendar)] // giá trị lạ (vd. OverdueBorrowMethod=3 của iLib) → mặc định
    public void Raw_parameter_values_are_cleaned_and_unknown_values_fall_back_to_defaults(string? renew, string? method, bool renewFromToday, OverdueMethod expected)
    {
        var settings = CirculationSettings.From(new Dictionary<string, string?> { [CirculationSettings.RenewDateCode] = renew, [CirculationSettings.OverdueMethodCode] = method });
        Assert.Equal((renewFromToday, expected), (settings.RenewFromToday, settings.OverdueMethod));
    }

    [Fact]
    public async Task Renewal_counts_from_the_old_due_date_unless_the_unit_parameter_says_from_today()
    {
        var oldDue = await RenewedDueShift(null);
        var fromToday = await RenewedDueShift(new CirculationSettings(true, OverdueMethod.Calendar));
        Assert.Equal(7, oldDue); // hạn cũ (14 ngày) + 7 ngày gia hạn: hạn mới cách hạn cũ 7 ngày
        Assert.Equal(3, fromToday); // tính từ hôm nay (đã qua 10 ngày) + 7 ngày = ngày 17: cách hạn cũ (ngày 14) 3 ngày
    }

    private async Task<double> RenewedDueShift(CirculationSettings? settings)
    {
        var tenantId = NewTenant();
        if (settings is not null) Factory.Settings[tenantId] = settings;
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "P1");
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "GH-01"), Bib(tenantId, mfn, "Gia hạn"), Item(tenantId, "GH001", mfn));
        var loan = (await Read<CheckoutResult>(await Checkout(staff, "GH-01", place.Id, "GH001"))).Lines[0].Loan!;
        Factory.Clock.Advance(TimeSpan.FromDays(10)); // hạn 14 ngày, mới qua 10 ngày
        var renewed = await Read<LoanDto>(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(loan.PublicId, "Ôn thi"), Json));
        return (renewed.DueAt - loan.DueAt).TotalDays;
    }

    [Theory]
    [InlineData(OverdueMethod.Calendar)]
    [InlineData(OverdueMethod.SkipWeekends)]
    [InlineData(OverdueMethod.SkipSundays)]
    public async Task Overdue_days_in_fines_follow_the_unit_overdue_method(OverdueMethod method)
    {
        var tenantId = NewTenant();
        Factory.Settings[tenantId] = new CirculationSettings(false, method);
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "P1");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14, FinePerDay: 1000), Json));
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "QH-01"), Bib(tenantId, mfn, "Quá hạn"), Item(tenantId, "QH001", mfn));
        var loan = (await Read<CheckoutResult>(await Checkout(staff, "QH-01", place.Id, "QH001"))).Lines[0].Loan!;

        Factory.Clock.Advance(TimeSpan.FromDays(14 + 10)); // quá hạn 10 ngày lịch
        var expected = Enumerable.Range(1, 10).Select(i => Loan.LocalDate(loan.DueAt.AddDays(i)).DayOfWeek)
            .Count(d => method switch
            {
                OverdueMethod.SkipWeekends => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
                OverdueMethod.SkipSundays => d != DayOfWeek.Sunday,
                _ => true,
            });
        var built = await Read<FineTicketDetail>(await staff.PostAsJsonAsync(U("/api/fine-tickets/BuildForReader"), new BuildFineTicketRequest("QH-01"), Json));
        var line = Assert.Single(built.Lines);
        Assert.Equal((expected, expected * 1000m), (line.OverdueDays, line.Amount));
    }

    [Fact]
    public async Task A_loan_overdue_only_by_skipped_weekend_days_gets_no_fine_line_yet()
    {
        var tenantId = NewTenant();
        Factory.Settings[tenantId] = new CirculationSettings(false, OverdueMethod.SkipWeekends);
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "P1");
        // Hạn trả rơi vào Thứ sáu (giờ Việt Nam), rồi dời đồng hồ tới Chủ nhật: quá hạn theo lịch 2 ngày nhưng đều là cuối tuần.
        var today = Loan.LocalDate(Factory.Clock.GetUtcNow());
        var loanDays = (((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7) + 7;
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, loanDays, FinePerDay: 1000), Json));
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "CT-01"), Bib(tenantId, mfn, "Cuối tuần"), Item(tenantId, "CT001", mfn));
        await Read<CheckoutResult>(await Checkout(staff, "CT-01", place.Id, "CT001"));

        Factory.Clock.Advance(TimeSpan.FromDays(loanDays + 2));
        var built = await Read<FineTicketDetail>(await staff.PostAsJsonAsync(U("/api/fine-tickets/BuildForReader"), new BuildFineTicketRequest("CT-01"), Json));
        Assert.Empty(built.Lines);
    }
}
