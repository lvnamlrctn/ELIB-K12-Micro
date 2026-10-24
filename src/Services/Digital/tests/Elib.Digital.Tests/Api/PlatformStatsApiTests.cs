using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Digital.Application;
using Elib.Digital.Domain;

namespace Elib.Digital.Tests.Api;

public sealed class PlatformStatsApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static async Task<(EbookItemDto Item, EbookFileDto File)> AddAsync(HttpClient staff, string title, double size)
    {
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields(title, Free: true), Json));
        var file = await Read<EbookFileDto>(await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Guid.CreateVersion7(), FileExt: "pdf", FileSize: size), Json));
        return (item, file);
    }

    [Fact]
    public async Task Platform_admin_sees_digital_documents_reading_and_storage_per_tenant_filtered_by_period()
    {
        var a = NewTenant();
        var b = NewTenant();
        var staffA = Staff(a);
        var (itemA, fileA) = await AddAsync(staffA, "Số đỏ", 1000);
        await AddAsync(staffA, "Dế mèn phiêu lưu ký", 500);
        await AddAsync(Staff(b), "Của đơn vị khác", 7000);

        var readerId = Guid.CreateVersion7();
        await Publish(Reader(a, "PS-001") with { ReaderPublicId = readerId });
        var reader = ReaderClient(a, "PS-001", readerId);
        foreach (var _ in new[] { 1, 2 })
            Assert.True((await reader.PutAsJsonAsync(U("/api/reader/reading/progress"),
                new ReadBeatRequest(itemA.PublicId, fileA.PublicId, Guid.CreateVersion7(), 1, 100, 5, "desktop"), Json)).IsSuccessStatusCode);

        var system = Factory.CreateClient().WithClaims(TestClaims.SuperAdmin);
        var today = PlatformStatsRequest.Today(Factory.Clock);
        async Task<IReadOnlyList<DigitalPlatformStats>> Stats(PlatformStatsRequest request) =>
            await Read<IReadOnlyList<DigitalPlatformStats>>(await system.PostAsJsonAsync(U("/api/system/stats/digital"), request, Json));

        var week = await Stats(new PlatformStatsRequest([a, b], today.AddDays(-7), today));
        Assert.Equal([a, b], week.Select(s => s.TenantId));
        var statsA = week.Single(s => s.TenantId == a);
        Assert.Equal((2, 2, 2, 1500L, 2), (statsA.Titles, statsA.Published, statsA.Files, statsA.StorageBytes, statsA.Added));
        Assert.True(statsA.ReadSessions >= 1); // các lần đập nhịp cùng một phiên đọc: chỉ cần có phiên
        Assert.Equal(1, statsA.Readers);
        var statsB = week.Single(s => s.TenantId == b);
        Assert.Equal((1, 1, 7000L, 1, 0, 0), (statsB.Titles, statsB.Files, statsB.StorageBytes, statsB.Added, statsB.ReadSessions, statsB.Readers));

        // Kỳ đã qua: không có gì bổ sung/đọc trong kỳ; số "hiện tại" (đầu tài liệu, file, dung lượng) vẫn giữ.
        var past = Assert.Single(await Stats(new PlatformStatsRequest([a], today.AddDays(-60), today.AddDays(-1))));
        Assert.Equal((2, 2, 1500L), (past.Titles, past.Files, past.StorageBytes));
        Assert.Equal((0, 0, 0, 0), (past.Added, past.ReadSessions, past.Readers, past.Views));

        // Không lọc đơn vị: có cả hai đơn vị (DB test dùng chung — chỉ kiểm tra có mặt).
        var all = await Stats(new PlatformStatsRequest(null, today, today));
        Assert.Contains(all, s => s.TenantId == a);
        Assert.Contains(all, s => s.TenantId == b);

        // Cán bộ đơn vị không gọi được, kể cả có toàn quyền.
        Assert.Equal(HttpStatusCode.Forbidden, (await staffA.PostAsJsonAsync(U("/api/system/stats/digital"), new PlatformStatsRequest(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await system.PostAsJsonAsync(U("/api/system/stats/digital"), new PlatformStatsRequest(null, today, today.AddDays(-1)), Json)).StatusCode);
    }
}
