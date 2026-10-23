using System.Net;
using System.Net.Http.Json;
using Elib.Digital.Application;
using Elib.Digital.Domain;

namespace Elib.Digital.Tests.Api;

public sealed class DashboardApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static async Task<DigitalDashboard> Dashboard(HttpClient staff) => await Read<DigitalDashboard>(await staff.GetAsync(U("/api/dashboard")));

    private static async Task<(EbookItemDto Item, EbookFileDto File)> AddAsync(HttpClient staff, string title, string ext, double size)
    {
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields(title, Free: true), Json));
        var file = await Read<EbookFileDto>(await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Guid.CreateVersion7(), FileExt: ext, FileSize: size), Json));
        return (item, file);
    }

    [Fact]
    public async Task Dashboard_counts_documents_formats_storage_reads_and_top_read_of_the_tenant_only()
    {
        var a = NewTenant();
        var other = NewTenant();
        var staff = Staff(a);
        var (popular, popularFile) = await AddAsync(staff, "Số đỏ", "pdf", 1000);
        await AddAsync(staff, "Dế mèn phiêu lưu ký", "pdf", 500);
        await AddAsync(staff, "Truyện Kiều", "epub", 500);
        await AddAsync(Staff(other), "Của đơn vị khác", "pdf", 99999);

        var readerId = Guid.CreateVersion7();
        await Publish(Reader(a, "DD-001") with { ReaderPublicId = readerId });
        var reader = ReaderClient(a, "DD-001", readerId);
        foreach (var _ in new[] { 1, 2 })
            Assert.True((await reader.PutAsJsonAsync(U("/api/reader/reading/progress"),
                new ReadBeatRequest(popular.PublicId, popularFile.PublicId, Guid.CreateVersion7(), 1, 100, 5, "desktop"), Json)).IsSuccessStatusCode);

        var d = await Dashboard(staff);
        Assert.Equal((3, 3), (d.Ebooks, d.Published));
        Assert.Equal(12, d.AddedTrend.Points.Count);
        Assert.Equal((3, 3), (d.AddedTrend.Total, d.AddedTrend.Points[^1].Value));
        Assert.Equal((2, 2), (d.ReadsTrend.Total, d.ReadsTrend.Points[^1].Value));
        var top = Assert.Single(d.TopRead);
        Assert.Equal(("Số đỏ", 2, popular.PublicId), (top.Title, top.Count, top.EbookPublicId));
        Assert.Equal([("pdf", 2, 67), ("epub", 1, 33)], d.FormatDistribution.Select(f => (f.Format, f.Count, f.Percent)));
        Assert.Equal((2000L, 3), (d.Storage.TotalBytes, d.Storage.FileCount));
    }

    [Fact]
    public async Task Dashboard_requires_the_digital_items_permission()
    {
        var tenantId = NewTenant();
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_READING:view").GetAsync(U("/api/dashboard"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "DIGITAL_ITEMS:view").GetAsync(U("/api/dashboard"))).StatusCode);
    }
}
