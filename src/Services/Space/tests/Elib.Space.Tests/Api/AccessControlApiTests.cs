using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.BuildingBlocks.Tenancy;
using Elib.Space.Api;
using Elib.Space.Application;
using Elib.Space.Domain;

namespace Elib.Space.Tests.Api;

public sealed class AccessControlApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    private const string GatewayKey = "test-gateway-key-0123456789abcdef";

    /// <summary>
    /// Thiết bị không mang JWT — tenant đến từ header X-Tenant-Id có chữ ký gateway (đúng cách OPAC ẩn danh khác xác định đơn
    /// vị khi gọi thẳng service trong test, không qua Host thật).
    /// </summary>
    private HttpClient DeviceClient(long tenantId, string code, string key)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        client.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(GatewayKey, tenantId));
        client.DefaultRequestHeaders.Add("X-Device-Code", code);
        client.DefaultRequestHeaders.Add("X-Device-Key", key);
        return client;
    }

    [Fact]
    public async Task Staff_adds_a_device_and_gets_a_one_time_api_key()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var response = await Read<JsonElement>(await staff.PostAsJsonAsync(U("/api/access/devices/Add"), new AccessDeviceRequest("DOOR-1", "Cửa phòng 101", null), Json));
        var device = response.GetProperty("device").Deserialize<AccessDeviceDto>(Json)!;
        var key = response.GetProperty("key").Deserialize<NewApiKey>(Json)!;
        Assert.Equal(("DOOR-1", "Cửa phòng 101"), (device.Code, device.Name));
        Assert.False(string.IsNullOrWhiteSpace(key.RawKey));

        var wrongKey = await DeviceClient(tenantId, "DOOR-1", "sai-khoa").PostAsJsonAsync(U("/api/device/Verify"), new VerifyRequest("HS-99"), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);

        var unknownCard = await Read<AccessDecision>(await DeviceClient(tenantId, "DOOR-1", key.RawKey).PostAsJsonAsync(U("/api/device/Verify"), new VerifyRequest("HS-99"), Json));
        Assert.False(unknownCard.Allowed);
    }

    /// <summary>Phần cứng cửa có thể gửi ScannedAt theo giờ địa phương (offset khác 0) — endpoint phải tự chuẩn hoá trước khi lưu log.</summary>
    [Fact]
    public async Task Verify_with_a_non_utc_scanned_at_does_not_fail()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var device = await AddDeviceAsync(staff, "DOOR-6", null);
        var localNow = Factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7));

        var response = await DeviceClient(tenantId, device.Code, device.RawKey).PostAsJsonAsync(U("/api/device/Verify"), new VerifyRequest("HS-99", localNow), Json);
        var decision = await Read<AccessDecision>(response);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task Staff_card_always_opens_the_door()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var device = await AddDeviceAsync(staff, "DOOR-2", null);
        await staff.PostAsJsonAsync(U("/api/access/staff-cards/Add"), new AccessStaffCardRequest("STAFF-UID-1", "Thủ thư A"), Json);

        var decision = await Read<AccessDecision>(await DeviceClient(tenantId, device.Code, device.RawKey).PostAsJsonAsync(U("/api/device/Verify"), new VerifyRequest("STAFF-UID-1"), Json));
        Assert.Equal((true, "Thẻ cán bộ"), (decision.Allowed, decision.Reason));
    }

    [Fact]
    public async Task Approved_booking_within_grace_opens_and_auto_checks_in()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P140", checkInGraceMinutes: 30);
        var device = await AddDeviceAsync(staff, "DOOR-3", space.Id);
        var reader = Reader(tenantId, "hs-40");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-40", reader.ReaderPublicId);
        var day = Tomorrow();
        var start = VnLocal(day, new TimeOnly(9, 0));

        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, start, VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json));
        Factory.Clock.Advance(start.AddMinutes(5) - Factory.Clock.GetUtcNow());

        var decision = await Read<AccessDecision>(await DeviceClient(tenantId, device.Code, device.RawKey).PostAsJsonAsync(U("/api/device/Verify"), new VerifyRequest("HS-40"), Json));
        Assert.True(decision.Allowed);

        var reread = await Read<BookingDto>(await staff.GetAsync(U($"/api/bookings/GetById/{booking.PublicId}")));
        Assert.Equal(RoomBookingStatus.CheckedIn, reread.Status);

        var denyAgain = await DeviceClient(tenantId, device.Code, device.RawKey).PostAsJsonAsync(U("/api/device/Verify"), new VerifyRequest("HS-40"), Json);
        var again = await Read<AccessDecision>(denyAgain);
        Assert.True(again.Allowed); // đã CheckedIn — vẫn mở (đang sử dụng không gian)
    }

    [Fact]
    public async Task Unlock_command_is_polled_and_acknowledged_by_the_device()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var device = await AddDeviceAsync(staff, "DOOR-4", null);

        await staff.PostAsJsonAsync(U("/api/access/unlock"), new UnlockRequest(device.Id, null, "Mở hộ khách"), Json);
        var client = DeviceClient(tenantId, device.Code, device.RawKey);
        var pending = await Read<List<PendingCommand>>(await client.GetAsync(U("/api/device/Commands")));
        var command = Assert.Single(pending);
        Assert.Equal("OPEN", command.Command);

        await client.PostAsJsonAsync(U("/api/device/Commands/Ack"), new AckRequest(command.Id), Json);
        var afterAck = await Read<List<PendingCommand>>(await client.GetAsync(U("/api/device/Commands")));
        Assert.Empty(afterAck);
    }

    [Fact]
    public async Task Regenerating_the_key_invalidates_the_old_one()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var device = await AddDeviceAsync(staff, "DOOR-5", null);

        var newKey = await Read<NewApiKey>(await staff.PostAsync(U($"/api/access/devices/{device.PublicId}/RegenerateKey"), null));
        var oldKeyResponse = await DeviceClient(tenantId, device.Code, device.RawKey).PostAsJsonAsync(U("/api/device/Verify"), new VerifyRequest("X"), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, oldKeyResponse.StatusCode);

        var newKeyResponse = await DeviceClient(tenantId, device.Code, newKey.RawKey).PostAsJsonAsync(U("/api/device/Verify"), new VerifyRequest("X"), Json);
        Assert.True(newKeyResponse.IsSuccessStatusCode);
    }

    private static async Task<(Guid PublicId, long Id, string Code, string RawKey)> AddDeviceAsync(HttpClient staff, string code, long? spaceId)
    {
        var response = await Read<JsonElement>(await staff.PostAsJsonAsync(U("/api/access/devices/Add"), new AccessDeviceRequest(code, "Thiết bị " + code, spaceId), Json));
        var device = response.GetProperty("device").Deserialize<AccessDeviceDto>(Json)!;
        var key = response.GetProperty("key").Deserialize<NewApiKey>(Json)!;
        return (device.PublicId, device.Id, device.Code, key.RawKey);
    }
}
