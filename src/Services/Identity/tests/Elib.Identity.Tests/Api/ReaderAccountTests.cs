using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Patron;
using Elib.Contracts.Events.Platform;
using Elib.Identity.Application;
using Elib.Identity.Domain;
using Elib.Identity.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Identity.Tests.Api;

/// <summary>Mật khẩu OPAC của bạn đọc: hồ sơ theo ReaderChanged, cán bộ đặt mật khẩu/số thẻ, hỏi patron khi chưa có event, cô lập đơn vị.</summary>
public sealed class ReaderAccountTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>, IAsyncLifetime
{
    private static int _nextTenant = 900;
    private readonly Dictionary<long, long> _admins = [];

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private async Task<long> NewTenantAsync()
    {
        var tenantId = Interlocked.Increment(ref _nextTenant);
        await factory.ProvisionTenantAsync(tenantId, "RA" + tenantId.ToString(CultureInfo.InvariantCulture));
        var admin = await factory.CreateClient().WithClaims(TestClaims.SuperAdmin)
            .PostAsJsonAsync($"/api/system/tenants/{tenantId}/admins", new CreateTenantAdminRequest("quantri", "Quản trị", null, "Matkhau123"));
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
        _admins[tenantId] = (await admin.Content.ReadFromJsonAsync<UserDto>())!.Id;
        return tenantId;
    }

    private HttpClient Admin(long tenantId) => factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, _admins[tenantId]));

    private static ReaderChanged Reader(long tenantId, Guid id, string cardNo, long version = 1, bool deleted = false) => new()
    {
        TenantId = tenantId, ReaderPublicId = id, CardNo = cardNo, FullName = "Học sinh " + cardNo, Email = cardNo.ToLowerInvariant() + "@truong.test",
        Status = 2, Version = version, Deleted = deleted,
    };

    /// <summary>Patron phát ReaderChanged → chờ identity ghi xong.</summary>
    private async Task PublishAsync(ReaderChanged e)
    {
        await factory.Services.GetRequiredService<IBus>().Publish(e);
        for (var i = 0; i < 100; i++)
        {
            if (await AccountAsync(e.TenantId, e.ReaderPublicId) is { } a && a.Version >= e.Version) return;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Bạn đọc {e.CardNo} không được đồng bộ vào identity.");
    }

    private async Task<ReaderAccount?> AccountAsync(long tenantId, Guid readerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.ReaderAccounts.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == tenantId && a.ReaderPublicId == readerId);
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task Staff_sets_reader_password_which_is_stored_as_bcrypt_and_never_returned_or_logged()
    {
        var tenantId = await NewTenantAsync();
        var readerId = Guid.NewGuid();
        await PublishAsync(Reader(tenantId, readerId, "HS-01"));
        var admin = Admin(tenantId);

        var before = await Read<List<ReaderAccountDto>>(await admin.PostAsJsonAsync(U("/api/reader-accounts/Status"), new ReaderAccountStatusRequest([readerId])));
        Assert.False(Assert.Single(before).HasPassword);

        var weak = await admin.PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest("12345", false));
        Assert.Equal((HttpStatusCode.BadRequest, "READER_PASSWORD_INVALID"), (weak.StatusCode, await ErrorCode(weak)));

        var set = await admin.PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest("hocsinh2026", false));
        var body = await set.Content.ReadAsStringAsync();
        var dto = await Read<ReaderAccountDto>(set);
        Assert.Equal((true, false, true), (dto.HasPassword, dto.CardNoPassword, dto.MustChangePassword));
        Assert.DoesNotContain("hocsinh2026", body, StringComparison.Ordinal);
        Assert.DoesNotContain("$2", body, StringComparison.Ordinal);

        var account = (await AccountAsync(tenantId, readerId))!;
        Assert.StartsWith("$2", account.Credentials.PasswordHash, StringComparison.Ordinal);
        var hasher = factory.Services.GetRequiredService<IPasswordHasher>();
        Assert.True(account.VerifyPassword("hocsinh2026", hasher.Verify));
        Assert.False(account.VerifyPassword("HS-01", hasher.Verify));

        var audit = factory.Published.OfType<AuditRecorded>().Last(a => a.TenantId == tenantId && a.Action == "READER_PASSWORD");
        Assert.Equal(readerId.ToString(), audit.EntityId);
        Assert.Contains("HS-01", audit.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("hocsinh2026", audit.Summary, StringComparison.Ordinal);

        // Mật khẩu = số thẻ: bỏ chuỗi băm, so không phân biệt hoa/thường, vẫn bắt đổi.
        dto = await Read<ReaderAccountDto>(await admin.PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest(null, true)));
        Assert.Equal((true, true, true), (dto.HasPassword, dto.CardNoPassword, dto.MustChangePassword));
        account = (await AccountAsync(tenantId, readerId))!;
        Assert.Equal("", account.Credentials.PasswordHash);
        Assert.True(account.VerifyPassword("hs-01", hasher.Verify));
        Assert.False(account.VerifyPassword("hocsinh2026", hasher.Verify));
    }

    [Fact]
    public async Task Imported_reader_flag_sets_card_number_password_only_when_the_account_has_none()
    {
        var tenantId = await NewTenantAsync();
        var hasher = factory.Services.GetRequiredService<IPasswordHasher>();
        var fresh = Guid.NewGuid();
        await PublishAsync(Reader(tenantId, fresh, "NK-01") with { InitialCardNoPassword = true });
        var account = (await AccountAsync(tenantId, fresh))!;
        Assert.Equal((true, true), (account.CardNoPassword, account.Credentials.MustChangePassword));
        Assert.True(account.VerifyPassword("nk-01", hasher.Verify));

        // Đã có mật khẩu riêng → cờ không ghi đè.
        var existing = Guid.NewGuid();
        await PublishAsync(Reader(tenantId, existing, "NK-02"));
        await Read<ReaderAccountDto>(await Admin(tenantId).PutAsJsonAsync(U($"/api/reader-accounts/{existing}/password"), new SetReaderPasswordRequest("rieng2026", false)));
        await PublishAsync(Reader(tenantId, existing, "NK-02", version: 2) with { InitialCardNoPassword = true });
        account = (await AccountAsync(tenantId, existing))!;
        Assert.False(account.CardNoPassword);
        Assert.True(account.VerifyPassword("rieng2026", hasher.Verify));

        // Không có cờ → chưa có mật khẩu như cũ.
        var plain = Guid.NewGuid();
        await PublishAsync(Reader(tenantId, plain, "NK-03"));
        Assert.False((await AccountAsync(tenantId, plain))!.HasPassword);
    }

    [Fact]
    public async Task Profile_follows_reader_events_by_version_and_deleted_reader_cannot_get_password()
    {
        var tenantId = await NewTenantAsync();
        var readerId = Guid.NewGuid();
        await PublishAsync(Reader(tenantId, readerId, "HS-02", version: 5));
        await factory.Services.GetRequiredService<IBus>().Publish(Reader(tenantId, readerId, "CU-02", version: 3)); // tới muộn, cũ hơn
        await PublishAsync(Reader(tenantId, readerId, "HS-02B", version: 6));
        Assert.Equal(("HS-02B", "HS-02B"), ((await AccountAsync(tenantId, readerId))!.CardNo, (await AccountAsync(tenantId, readerId))!.CardKey));

        var admin = Admin(tenantId);
        await Read<ReaderAccountDto>(await admin.PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest(null, true)));
        // Số thẻ đổi → mật khẩu mặc định đổi theo.
        await PublishAsync(Reader(tenantId, readerId, "HS-02C", version: 7));
        var hasher = factory.Services.GetRequiredService<IPasswordHasher>();
        Assert.True((await AccountAsync(tenantId, readerId))!.VerifyPassword("hs-02c", hasher.Verify));

        await PublishAsync(Reader(tenantId, readerId, "HS-02C", version: 8, deleted: true));
        var gone = await admin.PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest("matkhau9", false));
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        var status = await Read<List<ReaderAccountDto>>(await admin.PostAsJsonAsync(U("/api/reader-accounts/Status"), new ReaderAccountStatusRequest([readerId])));
        Assert.False(Assert.Single(status).HasPassword);
    }

    [Fact]
    public async Task Reader_not_yet_synced_is_fetched_from_patron_and_bulk_reports_missing_readers()
    {
        var tenantId = await NewTenantAsync();
        var synced = Guid.NewGuid();
        var onlyInPatron = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        await PublishAsync(Reader(tenantId, synced, "GV-01"));
        factory.Readers.Readers[(tenantId, onlyInPatron)] = Reader(tenantId, onlyInPatron, "HS-03", version: 2);
        var admin = Admin(tenantId);

        var result = await Read<BulkReaderPasswordResult>(await admin.PutAsJsonAsync(U("/api/reader-accounts/BulkPassword"),
            new BulkReaderPasswordRequest([synced, onlyInPatron, unknown, synced], null, true)));
        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal([unknown], result.NotFound);
        Assert.Equal("HS-03", (await AccountAsync(tenantId, onlyInPatron))!.CardNo);

        var statuses = await Read<List<ReaderAccountDto>>(await admin.PostAsJsonAsync(U("/api/reader-accounts/Status"),
            new ReaderAccountStatusRequest([synced, onlyInPatron, unknown])));
        Assert.Equal([true, true, false], statuses.Select(s => s.CardNoPassword));
        Assert.Contains(factory.Published.OfType<AuditRecorded>(),
            a => a.TenantId == tenantId && a.Action == "READER_PASSWORD_BULK" && a.Summary != null && a.Summary.Contains("2 bạn đọc", StringComparison.Ordinal));

        // Mật khẩu chung: một chuỗi băm cho cả lượt.
        result = await Read<BulkReaderPasswordResult>(await admin.PutAsJsonAsync(U("/api/reader-accounts/BulkPassword"),
            new BulkReaderPasswordRequest([synced, onlyInPatron], "thuvien2026", false)));
        Assert.Equal(2, result.UpdatedCount);
        var hasher = factory.Services.GetRequiredService<IPasswordHasher>();
        Assert.True((await AccountAsync(tenantId, onlyInPatron))!.VerifyPassword("thuvien2026", hasher.Verify));

        var empty = await admin.PutAsJsonAsync(U("/api/reader-accounts/BulkPassword"), new BulkReaderPasswordRequest([], null, true));
        Assert.Equal("READER_IDS_REQUIRED", await ErrorCode(empty));
        var many = await admin.PutAsJsonAsync(U("/api/reader-accounts/BulkPassword"),
            new BulkReaderPasswordRequest(Enumerable.Range(0, ReaderAccountService.MaxBulk + 1).Select(_ => Guid.NewGuid()).ToList(), null, true));
        Assert.Equal("READER_IDS_TOO_MANY", await ErrorCode(many));
    }

    [Fact]
    public async Task Other_tenant_cannot_see_or_set_password_and_permission_is_required()
    {
        var tenantA = await NewTenantAsync();
        var tenantB = await NewTenantAsync();
        var readerId = Guid.NewGuid();
        await PublishAsync(Reader(tenantA, readerId, "HS-04"));
        await Read<ReaderAccountDto>(await Admin(tenantA).PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest(null, true)));

        var other = Admin(tenantB);
        var status = await Read<List<ReaderAccountDto>>(await other.PostAsJsonAsync(U("/api/reader-accounts/Status"), new ReaderAccountStatusRequest([readerId])));
        Assert.False(Assert.Single(status).HasPassword);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest("matkhau9", false))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync(U($"/api/reader-accounts/{readerId}/unlock"), null)).StatusCode);
        Assert.True((await AccountAsync(tenantA, readerId))!.CardNoPassword);

        // Chỉ được xem bạn đọc: xem tình trạng được, đặt mật khẩu thì không.
        var role = await Read<RoleDto>(await Admin(tenantA).PostAsJsonAsync(U("/api/roles"), new RoleRequest("Xem bạn đọc", null, ["READERS:view"])));
        var viewer = await Read<UserDto>(await Admin(tenantA).PostAsJsonAsync(U("/api/users"),
            new CreateUserRequest("xembandoc", "Xem bạn đọc", null, null, "Matkhau123", [role.PublicId])));
        var client = factory.CreateClient().WithClaims(TestClaims.Staff(tenantA, viewer.Id));
        Assert.True((await client.PostAsJsonAsync(U("/api/reader-accounts/Status"), new ReaderAccountStatusRequest([readerId]))).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest("matkhau9", false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync(U("/api/reader-accounts/BulkPassword"), new BulkReaderPasswordRequest([readerId], null, true))).StatusCode);
    }
}
