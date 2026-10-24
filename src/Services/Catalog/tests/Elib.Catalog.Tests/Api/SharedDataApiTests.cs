using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;
using Elib.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Catalog.Tests.Api;

/// <summary>Dữ liệu dùng chung: đơn vị SHARED giữ gốc (từ điển, ISBD), mọi đơn vị khác nhận bản sao chỉ đọc qua <see cref="SharedSyncRunner{TDbContext}"/>.</summary>
public sealed class SharedDataApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextUserId = 91000;
    private readonly CatalogApiFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private const long Shared = 9100, A = 9101, B = 9102, Unlicensed = 9103;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;

    private async Task SeedTenantsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        foreach (var (id, code) in new[] { (Shared, SharedTenant.Code), (A, "TH-A"), (B, "TH-B"), (Unlicensed, "TH-C") })
        {
            db.Add(new TenantReplicaRecord { TenantId = id, Code = code, Name = code, Subdomain = code.ToLowerInvariant(), Status = "Active", SyncedAt = DateTimeOffset.UtcNow });
            if (id != Unlicensed) _factory.Licenses.Licensed.Add((id, "CATALOG"));
        }
        await db.SaveChangesAsync();
    }

    private Task<SharedSyncResult> SyncAsync() =>
        _factory.Services.GetRequiredService<SharedSyncRunner<CatalogDbContext>>().RunOnceAsync(CancellationToken.None);

    private static async Task<List<DictionaryDto>> Authors(HttpClient staff) =>
        await Read<List<DictionaryDto>>(await staff.PostAsJsonAsync(U("/api/dic-authors/SearchAll"), new CrudSearch(), Json));

    private static async Task<List<IsbdConfigDto>> IsbdConfigs(HttpClient staff) =>
        await Read<List<IsbdConfigDto>>(await staff.PostAsJsonAsync(U("/api/isbd-configs/SearchAll"), new CrudSearch(), Json));

    private static async Task<long> BookTypeId(HttpClient staff, string code = "SACH")
    {
        await Read<object>(await staff.PostAsJsonAsync(U("/api/bib-types/RestoreDefaults"), new { }, Json));
        var types = await Read<List<BibTypeDto>>(await staff.PostAsJsonAsync(U("/api/bib-types/SearchAll"), new CrudSearch(), Json));
        return types.Single(t => t.Code == code).Id;
    }

    private static async Task<IReadOnlyList<IsbdRule>> Template(HttpClient staff) => await Read<List<IsbdRule>>(await staff.GetAsync(U("/api/isbd-configs/Template")));

    [Fact]
    public async Task Master_dictionary_and_isbd_are_copied_as_read_only_replicas_to_licensed_tenants_only()
    {
        await SeedTenantsAsync();
        var shared = Staff(Shared);
        var staffA = Staff(A);
        var staffB = Staff(B);
        var typeShared = await BookTypeId(shared);
        await BookTypeId(staffA); // đơn vị B không có loại biểu ghi nào → bỏ qua ISBD
        await Read<DictionaryDto>(await shared.PostAsJsonAsync(U("/api/dic-authors/Add"), new DictionaryRequest("Nguyễn Nhật Ánh", "Nhà văn"), Json));
        await Read<DictionaryDto>(await shared.PostAsJsonAsync(U("/api/dic-authors/Add"), new DictionaryRequest("Tô Hoài"), Json));
        await Read<IsbdConfigDto>(await shared.PostAsJsonAsync(U("/api/isbd-configs/Add"), new IsbdConfigRequest(typeShared, await Template(shared)), Json));

        var run = await SyncAsync();
        Assert.Equal(0, run.Failed);
        Assert.Equal(Shared, run.SharedTenantId);

        foreach (var staff in new[] { staffA, staffB })
        {
            var authors = await Authors(staff);
            Assert.Equal(["Nguyễn Nhật Ánh", "Tô Hoài"], authors.Select(a => a.Value).Order());
            Assert.All(authors, a => Assert.True(a.IsReplica));
            Assert.Equal("Nhà văn", authors.Single(a => a.Value == "Nguyễn Nhật Ánh").Description);
        }
        // Gốc không bị nhân đôi vào chính SHARED; đơn vị chưa license không nhận gì.
        Assert.All(await Authors(shared), a => Assert.False(a.IsReplica));
        Assert.Equal(2, (await Authors(shared)).Count);
        using (var scope = _factory.Services.CreateScope())
        {
            using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
            var terms = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Set<DictionaryTerm>().IgnoreQueryFilters([ElibQueryFilters.Tenant]);
            Assert.Equal(0, await terms.CountAsync(t => t.TenantId == Unlicensed));
            Assert.Equal(0, await terms.CountAsync(t => t.TenantId == Shared && t.IsReplica));
        }

        // ISBD: A có loại SACH → có bản sao; B không có loại → không có.
        var isbdA = Assert.Single(await IsbdConfigs(staffA));
        Assert.True(isbdA.IsReplica);
        Assert.NotEmpty(isbdA.Rules);
        Assert.Empty(await IsbdConfigs(staffB));

        // Chạy lại không đổi gì (đã kịp mốc).
        var again = await SyncAsync();
        Assert.Equal(0, again.Applied);
        Assert.Equal(2, (await Authors(staffA)).Count);
    }

    [Fact]
    public async Task Edits_and_deletes_of_master_rows_reach_replicas_and_tenant_rows_are_never_touched()
    {
        await SeedTenantsAsync();
        var shared = Staff(Shared);
        var staffA = Staff(A);
        var staffB = Staff(B);
        var own = await Read<DictionaryDto>(await staffA.PostAsJsonAsync(U("/api/dic-authors/Add"), new DictionaryRequest("Tô Hoài", "mô tả riêng của đơn vị A"), Json));
        var master = await Read<DictionaryDto>(await shared.PostAsJsonAsync(U("/api/dic-authors/Add"), new DictionaryRequest("Tô Hoài", "gốc"), Json));
        var other = await Read<DictionaryDto>(await shared.PostAsJsonAsync(U("/api/dic-authors/Add"), new DictionaryRequest("Nam Cao"), Json));
        await SyncAsync();

        // Dòng riêng của A cùng khoá thắng: không thành bản sao, giữ nguyên mô tả. B nhận bản sao.
        var a1 = await Authors(staffA);
        Assert.Equal(("mô tả riêng của đơn vị A", false), (a1.Single(x => x.Value == "Tô Hoài").Description, a1.Single(x => x.Value == "Tô Hoài").IsReplica));
        Assert.Equal(own.PublicId, a1.Single(x => x.Value == "Tô Hoài").PublicId);
        Assert.True(a1.Single(x => x.Value == "Nam Cao").IsReplica);
        Assert.Equal("gốc", (await Authors(staffB)).Single(x => x.Value == "Tô Hoài").Description);

        // Sửa gốc → bản sao đổi theo; xoá gốc → bản sao mất; dòng riêng của A vẫn còn.
        await Read<DictionaryDto>(await shared.PutAsJsonAsync(U($"/api/dic-authors/Update/{master.PublicId}"), new DictionaryRequest("Tô Hoài", "gốc đã sửa"), Json));
        Assert.True((await shared.DeleteAsync(U($"/api/dic-authors/Delete/{other.PublicId}"))).IsSuccessStatusCode);
        await SyncAsync();

        var b2 = await Authors(staffB);
        Assert.Equal("gốc đã sửa", b2.Single().Description);
        Assert.Equal("Tô Hoài", b2.Single().Value);
        var a2 = await Authors(staffA);
        Assert.Equal("Tô Hoài", Assert.Single(a2).Value);
        Assert.Equal("mô tả riêng của đơn vị A", a2[0].Description);
    }

    [Fact]
    public async Task Replica_rows_cannot_be_edited_or_deleted_and_the_learner_leaves_them_alone()
    {
        await SeedTenantsAsync();
        var shared = Staff(Shared);
        var staffA = Staff(A);
        await Read<DictionaryDto>(await shared.PostAsJsonAsync(U("/api/dic-authors/Add"), new DictionaryRequest("Nguyễn Du"), Json));
        await SyncAsync();
        var replica = Assert.Single(await Authors(staffA));

        var update = await staffA.PutAsJsonAsync(U($"/api/dic-authors/Update/{replica.PublicId}"), new DictionaryRequest("Nguyễn Du 2"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal("SHARED_READ_ONLY", await ErrorCode(update));
        var delete = await staffA.DeleteAsync(U($"/api/dic-authors/Delete/{replica.PublicId}"));
        Assert.Equal("SHARED_READ_ONLY", await ErrorCode(delete));
        // Thêm tay trùng khoá với bản sao: bị coi là đã có.
        Assert.Equal(HttpStatusCode.Conflict, (await staffA.PostAsJsonAsync(U("/api/dic-authors/Add"), new DictionaryRequest("nguyen du"), Json)).StatusCode);

        // Biên mục lưu một biểu ghi có tác giả này: không thêm dòng mới, không tăng số lần dùng của bản sao.
        using (var scope = _factory.Services.CreateScope())
        {
            using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A);
            await scope.ServiceProvider.GetRequiredService<DictionaryLearner>().LearnAsync([(DictionaryKinds.Author, "Nguyễn Du"), (DictionaryKinds.Author, "Hàn Mặc Tử")], CancellationToken.None);
        }
        var authors = await Authors(staffA);
        Assert.Equal(["Hàn Mặc Tử", "Nguyễn Du"], authors.Select(a => a.Value).Order());
        var du = authors.Single(a => a.Value == "Nguyễn Du");
        Assert.Equal((true, 0), (du.IsReplica, du.UseCount));
        Assert.False(authors.Single(a => a.Value == "Hàn Mặc Tử").IsReplica);
    }

    [Fact]
    public async Task Isbd_replica_fills_only_missing_types_and_is_replaced_when_the_tenant_configures_its_own()
    {
        await SeedTenantsAsync();
        var shared = Staff(Shared);
        var staffA = Staff(A);
        var staffB = Staff(B);
        var typeShared = await BookTypeId(shared);
        var typeA = await BookTypeId(staffA);
        var typeB = await BookTypeId(staffB);
        var rules = await Template(shared);

        // B tự cấu hình trước khi đồng bộ → bản gốc không ghi đè.
        var ownB = await Read<IsbdConfigDto>(await staffB.PostAsJsonAsync(U("/api/isbd-configs/Add"), new IsbdConfigRequest(typeB, rules.Take(2).ToList()), Json));
        await Read<IsbdConfigDto>(await shared.PostAsJsonAsync(U("/api/isbd-configs/Add"), new IsbdConfigRequest(typeShared, rules), Json));
        await SyncAsync();

        var isbdB = Assert.Single(await IsbdConfigs(staffB));
        Assert.Equal((ownB.PublicId, false, 2), (isbdB.PublicId, isbdB.IsReplica, isbdB.Rules.Count));
        var replicaA = Assert.Single(await IsbdConfigs(staffA));
        Assert.Equal((true, rules.Count), (replicaA.IsReplica, replicaA.Rules.Count));

        // Bản sao chỉ đọc.
        var update = await staffA.PutAsJsonAsync(U($"/api/isbd-configs/Update/{replicaA.PublicId}"), new IsbdConfigRequest(typeA, rules.Take(3).ToList()), Json);
        Assert.Equal("SHARED_READ_ONLY", await ErrorCode(update));
        Assert.Equal("SHARED_READ_ONLY", await ErrorCode(await staffA.DeleteAsync(U($"/api/isbd-configs/Delete/{replicaA.PublicId}"))));

        // A tạo cấu hình riêng cho loại đang có bản sao → bản sao được thay.
        var ownA = await Read<IsbdConfigDto>(await staffA.PostAsJsonAsync(U("/api/isbd-configs/Add"), new IsbdConfigRequest(typeA, rules.Take(3).ToList()), Json));
        var after = Assert.Single(await IsbdConfigs(staffA));
        Assert.Equal((ownA.PublicId, false, 3), (after.PublicId, after.IsReplica, after.Rules.Count));
        await SyncAsync(); // chạy lại: không tạo lại bản sao đè lên cấu hình riêng
        Assert.False(Assert.Single(await IsbdConfigs(staffA)).IsReplica);

        // Sửa gốc → đơn vị chưa tự cấu hình (không có ở đây) hưởng; xoá gốc thì bản sao mất. Dòng riêng không đổi.
        var master = Assert.Single(await IsbdConfigs(shared));
        Assert.True((await shared.DeleteAsync(U($"/api/isbd-configs/Delete/{master.PublicId}"))).IsSuccessStatusCode);
        await SyncAsync();
        Assert.Equal(2, Assert.Single(await IsbdConfigs(staffB)).Rules.Count);
    }

    [Fact]
    public async Task Sync_status_and_run_are_only_for_the_shared_tenant()
    {
        await SeedTenantsAsync();
        var status = await Read<SharedSyncStatusDto>(await Staff(Shared).GetAsync(U("/api/shared-sync/Status")));
        Assert.True(status.IsShared);
        Assert.True((await Staff(Shared).PostAsync(U("/api/shared-sync/Run"), null)).IsSuccessStatusCode);

        var forbidden = await Staff(A).GetAsync(U("/api/shared-sync/Status"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("NOT_SHARED_TENANT", await ErrorCode(forbidden));
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(A).PostAsync(U("/api/shared-sync/Run"), null)).StatusCode);
        // Thiếu quyền chỉnh sửa từ điển/ISBD cũng bị chặn, dù là đơn vị SHARED.
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(Shared, "CATALOG_BIBS:view").GetAsync(U("/api/shared-sync/Status"))).StatusCode);

        // Sau một lượt chạy, trạng thái có kết quả.
        await SyncAsync();
        var done = await Read<SharedSyncStatusDto>(await Staff(Shared).GetAsync(U("/api/shared-sync/Status")));
        Assert.Equal((Shared, 0), (done.Last!.SharedTenantId, done.Last.Failed));
    }
}
