using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Serials.Application;
using Elib.Serials.Domain;

namespace Elib.Serials.Tests.Api;

public abstract class SerialsTestBase(SerialsApiFactory factory) : IClassFixture<SerialsApiFactory>, IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 31000;
    private static long _nextUserId = 41000;
    private static long _nextMfn = 700;

    protected SerialsApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static Uri U(string path) => new(path, UriKind.Relative);

    protected long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) Factory.Licenses.Licensed.Add((tenantId, "SERIALS"));
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

    protected async Task<BibChanged> PublishBib(long tenantId, string title)
    {
        var bib = new BibChanged { TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = title, Author = "Toà soạn", Status = 2 };
        await Factory.PublishAsync(bib);
        await Factory.WaitConsumedAsync(bib);
        return bib;
    }

    protected static Task<HttpResponseMessage> Post<T>(HttpClient c, string path, T body) => c.PostAsJsonAsync(U(path), body, Json);

    protected static async Task<FrequencyDto> AddFrequency(HttpClient c, string name, int unit, int? issuesPerUnit = 1, int? unitsPerIssue = null) =>
        await Read<FrequencyDto>(await Post(c, "/api/frequencies/Add", new FrequencyData(name, unit, issuesPerUnit, unitsPerIssue)));

    protected static async Task<PatternDto> AddPattern(HttpClient c, string name, string template = "Số {X}/{Y}", int? maxX = 12, int? maxY = null) =>
        await Read<PatternDto>(await Post(c, "/api/patterns/Add", new PatternData(name, null, template, "Số", "Năm", MaxX: maxX, MaxY: maxY)));

    protected static async Task<SubscriptionDto> AddSubscription(HttpClient c, SubscriptionRequest request) =>
        await Read<SubscriptionDto>(await Post(c, "/api/subscriptions/Add", request));

    /// <summary>Đăng ký hằng tháng, mẫu "Số X/Y" (X tối đa 12), kỳ đầu 15/01/2026 — mọi ngày dự kiến cố định, không phụ thuộc hôm nay.</summary>
    protected static async Task<(SubscriptionDto Sub, FrequencyDto Frequency, PatternDto Pattern)> MonthlySubscription(HttpClient c, Guid? bib = null, int startX = 1, int startY = 2026)
    {
        var frequency = await AddFrequency(c, "Tháng " + Guid.NewGuid().ToString("N")[..6], FrequencyUnit.Month);
        var pattern = await AddPattern(c, "Mẫu " + Guid.NewGuid().ToString("N")[..6]);
        var sub = await AddSubscription(c, new SubscriptionRequest("Tạp chí thử", "1234-5678", bib, FrequencyId: frequency.Id, PatternId: pattern.Id,
            FirstIssueDate: new DateOnly(2026, 1, 15), StartX: startX, StartY: startY));
        return (sub, frequency, pattern);
    }

    protected static async Task<IssuePredictResult> Predict(HttpClient c, Guid subscription, int count) =>
        await Read<IssuePredictResult>(await Post(c, "/api/issues/Predict", new IssuePredictRequest(subscription, count)));
}
