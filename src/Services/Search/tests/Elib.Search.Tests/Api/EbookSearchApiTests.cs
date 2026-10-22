using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Digital;
using Elib.Search.Application;

namespace Elib.Search.Tests.Api;

public sealed class EbookSearchApiTests(SearchApiFactory factory) : IClassFixture<SearchApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7600;
    private static long _nextUserId = 17600;
    private static long _nextEbookId = 1;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "SEARCH"));
        return tenantId;
    }

    private HttpClient Opac(long tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        client.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(SearchApiFactory.GatewayKey, tenantId));
        return client;
    }

    private HttpClient Staff(long tenantId)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        factory.Permissions.Grants[userId] = ["*"];
        return factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static EbookChanged Ebook(long tenantId, string title, string? author = null, int status = 2, long version = 1, Guid? id = null, bool deleted = false,
        IReadOnlyList<string>? metadata = null) => new()
    {
        TenantId = tenantId, EbookPublicId = id ?? Guid.CreateVersion7(), EbookId = Interlocked.Increment(ref _nextEbookId), Title = title, Author = author,
        PublishYear = "2020", Status = status, Version = version, Deleted = deleted, Free = true, HasFile = true, CollectionName = "Văn học",
        Metadata = metadata ?? [],
    };

    private async Task Publish(params IntegrationEvent[] events)
    {
        foreach (var e in events) await factory.PublishAsAsync(e);
        foreach (var e in events) await factory.WaitConsumedAsync(e);
    }

    private static async Task<OpacEbookResult> Search(HttpClient opac, string? q) =>
        await (await opac.PostAsJsonAsync(U("/api/opac/ebooks/Search"), new OpacEbookSearchRequest { Q = q }, Json)).Content.ReadFromJsonAsync<OpacEbookResult>(Json)
            ?? throw new InvalidOperationException();

    [Fact]
    public async Task Published_ebooks_are_found_without_diacritics_including_their_metadata_and_hidden_or_deleted_ones_are_not()
    {
        var tenantId = NewTenant();
        var visible = Ebook(tenantId, "Số đỏ", "Vũ Trọng Phụng", metadata: ["Châm biếm xã hội"]);
        var hidden = Ebook(tenantId, "Số đỏ bản nháp", status: 1);
        var deleted = Ebook(tenantId, "Số đỏ đã xoá", deleted: true);
        await Publish(visible, hidden, deleted);
        var opac = Opac(tenantId);

        var byTitle = await Search(opac, "so do");
        var found = Assert.Single(byTitle.Items);
        Assert.Equal((visible.EbookPublicId, true, true), (found.PublicId, found.Free, found.HasFile));
        Assert.Equal("Vũ Trọng Phụng", found.Author);
        Assert.Single((await Search(opac, "cham biem")).Items); // tìm theo giá trị metadata
        Assert.Single((await Search(opac, "vu trong phung")).Items);
        Assert.Empty((await Search(opac, "khong co")).Items);
    }

    [Fact]
    public async Task Older_versions_and_other_tenants_do_not_change_what_a_tenant_sees()
    {
        var tenantId = NewTenant();
        var id = Guid.CreateVersion7();
        await Publish(Ebook(tenantId, "Bản mới", id: id, version: 5));
        await Publish(Ebook(tenantId, "Bản cũ", id: id, version: 3)); // event đến muộn
        Assert.Equal("Bản mới", Assert.Single((await Search(Opac(tenantId), null)).Items).Title);

        await Publish(Ebook(tenantId, "Bản mới", id: id, version: 6, deleted: true));
        Assert.Empty((await Search(Opac(tenantId), null)).Items);
        Assert.Empty((await Search(Opac(NewTenant()), "ban moi")).Items);
    }

    [Fact]
    public async Task Rebuild_reads_ebooks_from_digital_and_marks_missing_ones_deleted_but_keeps_everything_when_digital_is_unreachable()
    {
        var tenantId = NewTenant();
        var gone = Ebook(tenantId, "Đã xoá ở digital");
        await Publish(gone);
        factory.Sources.Ebooks[tenantId] = [Ebook(tenantId, "Truyện Kiều", "Nguyễn Du"), Ebook(tenantId, "Chinh phụ ngâm"), Ebook(tenantId, "Cung oán ngâm khúc")];

        var admin = Staff(tenantId);
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync(U("/api/index/Rebuild"), null)).StatusCode);
        var status = await SearchApiFactory.WaitForAsync(async () =>
        {
            var s = await (await admin.GetAsync(U("/api/index/Status"))).Content.ReadFromJsonAsync<SearchIndexStatus>(Json);
            return s!.Status == "Done" ? s : null;
        }, "dựng lại chỉ mục");
        Assert.Equal((3, 3), (status.Ebooks, status.IndexedEbooks));

        var opac = Opac(tenantId);
        Assert.Equal(3, (await Search(opac, null)).Total);
        Assert.Empty((await Search(opac, "da xoa")).Items);
        Assert.Single((await Search(opac, "\"chinh phu\"")).Items);
    }
}
