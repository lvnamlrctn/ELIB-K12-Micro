using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;

namespace Elib.Circulation.Tests.Api;

public sealed class ReportsApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private static async Task<CirculationReport> Report(HttpClient staff, int type, DateOnly? from = null, DateOnly? to = null) =>
        await Read<CirculationReport>(await staff.PostAsJsonAsync(U("/api/reports/Search"), new CirculationReportRequest(type, from, to, PageSize: 100), Json));

    [Fact]
    public async Task Reports_count_daily_activity_and_list_overdue_popular_and_unused_items()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "R1");
        var (popular, quiet) = (NextMfn(), NextMfn());
        await Publish(Reader(tenantId, "BC-01"), Reader(tenantId, "BC-02"), Bib(tenantId, popular, "Tuổi thơ dữ dội"), Bib(tenantId, quiet, "Sách ít đọc"),
            Item(tenantId, "TT001", popular), Item(tenantId, "TT002", popular), Item(tenantId, "IT001", quiet));
        var today = DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(7));

        var first = (await Read<CheckoutResult>(await Checkout(staff, "BC-01", place.Id, "TT001"))).Lines[0].Loan!;
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "BC-02", place.Id, "TT002"))).Succeeded);
        await Read<LoanDto>(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(first.PublicId, "Đọc tiếp"), Json));
        await Read<ReturnResult>(await staff.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest("TT002"), Json));

        var daily = await Report(staff, 1, today, today);
        Assert.Equal(["Ngày", "Số lượt mượn", "Số lượt trả", "Số lượt gia hạn", "Số bạn đọc phục vụ"], daily.Headers);
        Assert.Equal([today.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture), "2", "1", "1", "2"], Assert.Single(daily.Rows));
        Assert.Equal(["Tổng cộng", "2", "1", "1", "2"], daily.TotalRow);

        var popularity = await Report(staff, 8);
        Assert.Equal(["1", "Tuổi thơ dữ dội", "Tô Hoài", "2"], popularity.Rows[0]);
        var unused = await Report(staff, 9);
        Assert.Equal("IT001", Assert.Single(unused.Rows)[1]);

        Factory.Clock.Advance(TimeSpan.FromDays(30)); // hạn 14 + gia hạn 7 → quá hạn 9 ngày
        var overdue = await Report(staff, 4);
        Assert.Equal(["1", "TT001", "Tuổi thơ dữ dội", "Bạn đọc BC-01 (BC-01)"], Assert.Single(overdue.Rows).Take(4));
        Assert.Equal("9", overdue.Rows[0][^1]);
        var readers = await Report(staff, 7);
        Assert.Equal(("BC-01", "1", "9"), (readers.Rows[0][1], readers.Rows[0][6], readers.Rows[0][7]));

        var excel = await staff.PostAsJsonAsync(U("/api/reports/Export"), new CirculationReportRequest(4), Json);
        Assert.True(excel.IsSuccessStatusCode);
        Assert.Equal(CrudExcel.ContentType, excel.Content.Headers.ContentType?.MediaType);
        Assert.Equal((byte)'P', (await excel.Content.ReadAsByteArrayAsync())[0]);

        var history = await staff.PostAsJsonAsync(U("/api/loans/Export"), new LoanSearch { CardNo = "BC-01" }, Json);
        Assert.True(history.IsSuccessStatusCode);
        Assert.Equal((byte)'P', (await history.Content.ReadAsByteArrayAsync())[0]);
        Assert.Equal("REPORT_TYPE_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/reports/Search"), new CirculationReportRequest(12), Json))).Code);
    }

    [Fact]
    public async Task Photocopy_total_is_computed_by_the_server_and_can_be_marked_paid()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "PC-01"), Bib(tenantId, mfn, "Giáo trình Toán"), Item(tenantId, "GT001", mfn));

        var photo = await Read<PhotocopyDto>(await staff.PostAsJsonAsync(U("/api/photocopies/Add"), new PhotocopyRequest("pc-01", "gt001", 1, 10, 2, 500), Json));
        Assert.Equal((10000m, false, "Giáo trình Toán", "Bạn đọc PC-01"), (photo.Total, photo.Paid, photo.Title, photo.ReaderName));
        await Read<PhotocopyDto>(await staff.PostAsJsonAsync(U("/api/photocopies/Add"), new PhotocopyRequest("PC-01", "GT001", 5, 5, 1, 1000, Paid: true), Json));

        var totals = await Read<PhotocopyTotals>(await staff.PostAsJsonAsync(U("/api/photocopies/Totals"), new PhotocopySearch(), Json));
        Assert.Equal(new PhotocopyTotals(2, 11000m, 1000m, 10000m), totals);
        var paid = await Read<PhotocopyDto>(await staff.PostAsJsonAsync(U("/api/photocopies/SetPaid"), new PhotocopyPaidRequest(photo.PublicId, true), Json));
        Assert.True(paid.Paid);
        Assert.Equal(0, (await Read<CrudPage<PhotocopyDto>>(await staff.PostAsJsonAsync(U("/api/photocopies/Search"), new PhotocopySearch { Paid = false }, Json))).TotalCount);

        Assert.Equal("PAGES_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/photocopies/Add"), new PhotocopyRequest("PC-01", "GT001", 9, 3, 1, 500), Json))).Code);
        Assert.Equal("READER_NOT_FOUND", (await Error(await staff.PostAsJsonAsync(U("/api/photocopies/Add"), new PhotocopyRequest("KHONGCO", "GT001", 1, 1, 1, 500), Json))).Code);
    }
}
