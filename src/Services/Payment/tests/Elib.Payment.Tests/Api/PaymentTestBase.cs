using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Testing;

namespace Elib.Payment.Tests.Api;

/// <summary>Client, đơn vị và hàm đọc kết quả dùng chung cho các test API của payment (mỗi test một đơn vị riêng).</summary>
public abstract class PaymentTestBase(PaymentApiFactory factory) : IClassFixture<PaymentApiFactory>, IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7000;
    private static long _nextUserId = 17000;

    protected PaymentApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static Uri U(string path) => new(path, UriKind.Relative);

    protected long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) Factory.Licenses.Licensed.Add((tenantId, "PAYMENT"));
        return tenantId;
    }

    protected HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        Factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return Factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    protected static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();
}
