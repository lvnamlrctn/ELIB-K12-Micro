using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Patron;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

/// <summary>Dữ liệu và client dùng chung cho các test API của space (mỗi test một đơn vị riêng).</summary>
public abstract class SpaceTestBase(SpaceApiFactory factory) : IClassFixture<SpaceApiFactory>, IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7000;
    private static long _nextUserId = 17000;

    protected SpaceApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static Uri U(string path) => new(path, UriKind.Relative);

    protected long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) Factory.Licenses.Licensed.Add((tenantId, "SPACE"));
        return tenantId;
    }

    protected HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        Factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return Factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

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

    protected static ReaderChanged Reader(long tenantId, string card, long? typeId = null, int status = 2, DateOnly? expire = null, long version = 0,
        string? email = null) => new()
    {
        TenantId = tenantId, ReaderPublicId = Guid.CreateVersion7(), CardNo = card, FullName = "Bạn đọc " + card, ReaderTypeId = typeId,
        ReaderTypeName = "Học sinh", Status = status, ExpireDate = expire, Version = version, Email = email,
    };

    protected async Task Publish(params Elib.Contracts.Events.IntegrationEvent[] events)
    {
        foreach (var e in events) await Factory.PublishAsAsync(e);
        foreach (var e in events) await Factory.WaitConsumedAsync(e);
    }

    protected static DateTimeOffset VnLocal(DateOnly date, TimeOnly time) => new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(7)).ToUniversalTime();

    /// <summary>Ngày mai theo giờ Việt Nam, tính từ đồng hồ test — tránh lệ thuộc giờ hệ thống thật khi chạy CI.</summary>
    protected DateOnly Tomorrow() => DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(7)).AddDays(1);

    protected static async Task<BuildingDto> AddBuildingAsync(HttpClient staff, string code = "B1") =>
        await Read<BuildingDto>(await staff.PostAsJsonAsync(U("/api/buildings/Add"), new BuildingRequest(code, "Toà " + code, null), Json));

    protected static async Task<FloorDto> AddFloorAsync(HttpClient staff, long buildingId, string name = "Tầng 1") =>
        await Read<FloorDto>(await staff.PostAsJsonAsync(U("/api/floors/Add"), new FloorRequest(buildingId, name, 1), Json));

    protected static async Task<SpaceDto> AddSpaceAsync(HttpClient staff, long floorId, string code, int capacity = 4, bool autoApprove = true,
        int? minGroupSize = null, IReadOnlyList<long>? allowedReaderTypeIds = null, int checkInGraceMinutes = 15, int minAdvanceMinutes = 0,
        int maxAdvanceDays = 7, int? maxAdvanceHours = null, int maxBookingMinutesPerReader = 0) =>
        await Read<SpaceDto>(await staff.PostAsJsonAsync(U("/api/spaces/Add"), new SpaceRequest(floorId, code, "Phòng " + code, 1, 1, capacity,
            minAdvanceMinutes, maxAdvanceDays, maxAdvanceHours, maxBookingMinutesPerReader, checkInGraceMinutes, autoApprove, null,
            allowedReaderTypeIds, minGroupSize), Json));
}
