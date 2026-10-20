using System.Net.Http.Json;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Testing;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

public sealed class ReportApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Report_summarizes_bookings_by_status_and_space()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P130", capacity: 4);
        var readerA = Reader(tenantId, "hs-30");
        var readerB = Reader(tenantId, "hs-31");
        await Publish(readerA, readerB);
        var clientA = ReaderClient(tenantId, "hs-30", readerA.ReaderPublicId);
        var clientB = ReaderClient(tenantId, "hs-31", readerB.ReaderPublicId);
        var day = Tomorrow();

        await clientA.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json);
        var cancelMe = await Read<BookingDto>(await clientB.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(11, 0)), VnLocal(day, new TimeOnly(12, 0)), 1, null, null), Json));
        await clientB.DeleteAsync(U($"/api/reader/bookings/{cancelMe.PublicId}"));

        var report = await Read<RoomBookingReport>(await staff.PostAsJsonAsync(U("/api/reports/Search"), new ReportRequest(day, day), Json));
        Assert.Equal((2, 1, 1), (report.TotalBookings, report.Approved, report.Cancelled));
        var spaceRow = Assert.Single(report.BySpace);
        Assert.Equal((space.Id, 2, 1), (spaceRow.SpaceId, spaceRow.Bookings, spaceRow.Cancelled));
        Assert.Equal(2, report.TopReaders.Count);
    }

    [Fact]
    public async Task Excel_export_has_three_sheets_each_with_the_parent_body_date_line_and_signature_block()
    {
        var tenantId = NewTenant();
        Factory.Permissions.Grants[7701] = ["*"];
        var staff = Factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, 7701) + ";name=Quan Tri Vien");
        staff.DefaultRequestHeaders.Add("X-Report-Parent", Uri.EscapeDataString("Sở Giáo dục Thái Nguyên"));
        staff.DefaultRequestHeaders.Add("X-Report-Place", Uri.EscapeDataString("Thái Nguyên"));
        var day = Tomorrow();

        var response = await staff.PostAsJsonAsync(U("/api/reports/Export"), new ReportRequest(day, day), Json);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.StartsWith("bao-cao-dat-phong-", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
        using var book = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal(["Tổng hợp", "Theo không gian", "Bạn đọc đặt nhiều"], book.Worksheets.Select(s => s.Name));

        var summary = book.Worksheet(1);
        Assert.Equal("SỞ GIÁO DỤC THÁI NGUYÊN", summary.Cell(1, 1).GetString());
        Assert.Equal("BÁO CÁO ĐẶT PHÒNG", summary.Cell(3, 1).GetString());
        Assert.Equal("Tổng lượt đặt", summary.Cell(7, 2).GetString());
        // 12 chỉ tiêu: dòng cuối 18 → ngày 20, khối chữ ký 22, tên người lập 27.
        Assert.StartsWith("Thái Nguyên, ngày ", summary.Cell(20, 1).GetString(), StringComparison.Ordinal);
        Assert.Equal(("NGƯỜI LẬP BÁO CÁO", "THỦ TRƯỞNG ĐƠN VỊ"), (summary.Cell(22, 1).GetString(), summary.Cell(22, 2).GetString()));
        Assert.Equal("Quan Tri Vien", summary.Cell(27, 1).GetString());
        // Sheet rỗng dữ liệu vẫn có đủ đầu và cuối (dòng ngày 8).
        Assert.StartsWith("Thái Nguyên, ngày ", book.Worksheet(2).Cell(8, 1).GetString(), StringComparison.Ordinal);
    }
}
