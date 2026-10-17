using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Patron;

namespace Elib.Digital.Tests.Api;

/// <summary>Dữ liệu và client dùng chung cho các test API của digital (mỗi test một đơn vị riêng).</summary>
public abstract class DigitalTestBase(DigitalApiFactory factory) : IClassFixture<DigitalApiFactory>, IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 9000;
    private static long _nextUserId = 19000;

    protected DigitalApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static Uri U(string path) => new(path, UriKind.Relative);

    protected long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) Factory.Licenses.Licensed.Add((tenantId, "DIGITAL"));
        return tenantId;
    }

    protected HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        Factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return Factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    /// <summary>Token bạn đọc (OPAC tự phục vụ) — card_no/reader_id như token thật do identity phát.</summary>
    protected HttpClient ReaderClient(long tenantId, string cardNo, Guid readerPublicId) =>
        Factory.CreateClient().WithClaims(TestClaims.Reader(tenantId, cardNo, readerPublicId));

    protected static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected static async Task<(int Status, string? Code, string? Detail)> Error(HttpResponseMessage response)
    {
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return ((int)response.StatusCode, root.GetProperty("code").GetString(), root.GetProperty("detail").GetString());
    }

    protected static ReaderChanged Reader(long tenantId, string card, long? typeId = null, long version = 0, string? email = null) => new()
    {
        TenantId = tenantId, ReaderPublicId = Guid.CreateVersion7(), CardNo = card, FullName = "Bạn đọc " + card, ReaderTypeId = typeId,
        ReaderTypeName = "Học sinh", Version = version, Email = email,
    };

    /// <summary>Đưa vào bản sao qua event (như patron phát) và chờ consumer xử lý xong.</summary>
    protected async Task Publish(params IntegrationEvent[] events)
    {
        foreach (var e in events) await Factory.PublishAsAsync(e);
        foreach (var e in events) await Factory.WaitConsumedAsync(e);
    }
}
