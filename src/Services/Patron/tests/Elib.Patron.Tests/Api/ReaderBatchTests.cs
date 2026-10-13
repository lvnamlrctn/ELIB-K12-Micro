using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Patron;
using Elib.Contracts.Events.Platform;
using Elib.Patron.Application;
using Elib.Patron.Domain;
using Elib.Patron.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Patron.Tests.Api;

/// <summary>Thao tác hàng loạt kiểu ELIB-LRC: sửa theo bộ lọc có mã xác nhận, nhóm bạn đọc, xoá nhiều, nhật ký có diff + lý do sửa.</summary>
public sealed class ReaderBatchTests : IClassFixture<PatronApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7000;
    private static long _nextUserId = 17000;

    private readonly PatronApiFactory _factory;

    public ReaderBatchTests(PatronApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

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
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private static async Task<ReaderDto> AddReader(HttpClient client, ReaderRequest request) =>
        await Read<ReaderDto>(await client.PostAsJsonAsync(U("/api/readers/Add"), request, Json));

    private static async Task<NamedItemDto> AddNamed(HttpClient client, string resource, string name) =>
        await Read<NamedItemDto>(await client.PostAsJsonAsync(U($"/api/{resource}/Add"), new NameRequest(name), Json));

    private static async Task<string[]> Cards(HttpClient client, ReaderSearch search) =>
        [.. (await Read<CrudPage<ReaderDto>>(await client.PostAsJsonAsync(U("/api/readers/Search"), search, Json))).Items.Select(r => r.CardNo).Order()];

    [Fact]
    public async Task Batch_update_by_filter_needs_a_fresh_preview_token_for_the_same_filter()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        _factory.References[(tenantId, "orgs")] = [new(51, "Khối 6")];
        var c6 = await AddNamed(staff, "classes", "6A");
        var c7 = await AddNamed(staff, "classes", "7A");
        foreach (var card in new[] { "F1", "F2", "F3" }) await AddReader(staff, new ReaderRequest(card, null, "Bạn " + card, ClassId: c6.Id));
        await AddReader(staff, new ReaderRequest("G1", null, "Khác", ClassId: c7.Id));
        var other = Staff(tenantId + 500);
        await AddReader(other, new ReaderRequest("F9", null, "Đơn vị khác", ClassId: null));

        var filter = new ReaderSearch { ClassId = c6.Id };
        var preview = await Read<ReaderSelectionPreview>(await staff.PostAsJsonAsync(U("/api/readers/Batch/Preview"), filter, Json));
        Assert.Equal(3, preview.Count);

        // Không mã / mã của bộ lọc khác → từ chối, không ai bị sửa.
        Assert.Equal("READER_BATCH_TOKEN_REQUIRED", await ErrorCode(await staff.PutAsJsonAsync(U("/api/readers/BatchUpdate"),
            new ReaderBatchUpdateRequest(new ReaderSelection(Search: filter), ClassId: c7.Id), Json)));
        Assert.Equal("READER_BATCH_TOKEN_MISMATCH", await ErrorCode(await staff.PutAsJsonAsync(U("/api/readers/BatchUpdate"),
            new ReaderBatchUpdateRequest(new ReaderSelection(Search: new ReaderSearch(), Token: preview.Token), ClassId: c7.Id), Json)));
        // Mã do người khác xem trước cũng không dùng được.
        Assert.Equal("READER_BATCH_TOKEN_MISMATCH", await ErrorCode(await Staff(tenantId).PutAsJsonAsync(U("/api/readers/BatchUpdate"),
            new ReaderBatchUpdateRequest(new ReaderSelection(Search: filter, Token: preview.Token), ClassId: c7.Id), Json)));

        var request = new HttpRequestMessage(HttpMethod.Put, U("/api/readers/BatchUpdate"))
        {
            Content = JsonContent.Create(new ReaderBatchUpdateRequest(new ReaderSelection(Search: new ReaderSearch { ClassId = c6.Id, PageIndex = 3 },
                Token: preview.Token), ClassId: c7.Id, OrgId: 51, ExpireDate: new DateOnly(2028, 6, 30)), options: Json),
        };
        request.Headers.Add(CrudDiff.ReasonHeader, Uri.EscapeDataString("Lên lớp năm học mới"));
        Assert.Equal(3, (await Read<ReaderBatchResult>(await staff.SendAsync(request))).Count);

        Assert.Equal(["F1", "F2", "F3", "G1"], await Cards(staff, new ReaderSearch { ClassId = c7.Id }));
        Assert.Equal(["F1", "F2", "F3"], await Cards(staff, new ReaderSearch { OrgIds = [51] }));
        Assert.Equal(["F9"], await Cards(other, new ReaderSearch()));

        // Mỗi bạn đọc một dòng nhật ký có diff + lý do; event bản sao mang lớp mới.
        var f1 = (await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch { CardNo = "F1" }, Json))).Items[0];
        var log = Assert.Single(_factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenantId && a.EntityId == f1.PublicId.ToString() && a.Action == "UPDATE");
        var changes = JsonDocument.Parse(log.ChangesJson!).RootElement;
        Assert.Equal((c6.Id, c7.Id), (changes.GetProperty("classId")[0].GetInt64(), changes.GetProperty("classId")[1].GetInt64()));
        Assert.Equal("2028-06-30", changes.GetProperty("expireDate")[1].GetString());
        Assert.Equal("Lên lớp năm học mới", changes.GetProperty("_reason").GetString());
        Assert.False(changes.TryGetProperty("fullName", out _));
        Assert.Contains(_factory.PublishedOf<ReaderChanged>(), e => e.ReaderPublicId == f1.PublicId && e.ClassId == c7.Id);

        // Đơn vị không có trong danh mục → từ chối.
        var p2 = await Read<ReaderSelectionPreview>(await staff.PostAsJsonAsync(U("/api/readers/Batch/Preview"), filter, Json));
        Assert.Equal("IMPORT_REF_NOT_FOUND", await ErrorCode(await staff.PutAsJsonAsync(U("/api/readers/BatchUpdate"),
            new ReaderBatchUpdateRequest(new ReaderSelection(Search: filter, Token: p2.Token), OrgId: 999), Json)));
    }

    [Fact]
    public async Task Groups_are_assigned_by_selection_or_filter_shown_per_reader_and_removed_with_the_group()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var group = await Read<ReaderGroupDto>(await staff.PostAsJsonAsync(U("/api/reader-groups/Add"), new ReaderGroupRequest("CLB Đọc sách", "CLB"), Json));
        var other = await Read<ReaderGroupDto>(await staff.PostAsJsonAsync(U("/api/reader-groups/Add"), new ReaderGroupRequest("Đội tuyển", null), Json));
        var a = await AddReader(staff, new ReaderRequest("N1", null, "An"));
        var b = await AddReader(staff, new ReaderRequest("N2", null, "Bình"));
        await AddReader(staff, new ReaderRequest("M1", null, "Minh"));

        Assert.Equal(2, (await Read<ReaderBatchResult>(await staff.PutAsJsonAsync(U("/api/readers/Groups"),
            new ReaderGroupsRequest(new ReaderSelection([a.PublicId, b.PublicId]), group.Id, Add: true), Json))).Count);
        // Thêm lại không nhân đôi.
        Assert.Equal(0, (await Read<ReaderBatchResult>(await staff.PutAsJsonAsync(U("/api/readers/Groups"),
            new ReaderGroupsRequest(new ReaderSelection([a.PublicId]), group.Id, Add: true), Json))).Count);
        Assert.Equal(["N1", "N2"], await Cards(staff, new ReaderSearch { GroupId = group.Id }));

        // Theo bộ lọc (kèm mã xem trước): mọi bạn đọc có "N" trong số thẻ vào nhóm thứ hai.
        var filter = new ReaderSearch { CardNoContains = "N" };
        var preview = await Read<ReaderSelectionPreview>(await staff.PostAsJsonAsync(U("/api/readers/Batch/Preview"), filter, Json));
        Assert.Equal(2, (await Read<ReaderBatchResult>(await staff.PutAsJsonAsync(U("/api/readers/Groups"),
            new ReaderGroupsRequest(new ReaderSelection(Search: filter, Token: preview.Token), other.Id, Add: true), Json))).Count);

        Assert.Equal([group.Id, other.Id], await Read<List<long>>(await staff.GetAsync(U($"/api/readers/Groups/{a.PublicId}"))));
        Assert.Equal([other.Id], await Read<List<long>>(await staff.PutAsJsonAsync(U($"/api/readers/Groups/{a.PublicId}"),
            new ReaderGroupSetRequest([other.Id]), Json)));
        Assert.Contains(_factory.PublishedOf<AuditRecorded>(), l => l.EntityId == a.PublicId.ToString() && l.ChangesJson?.Contains("groupIds", StringComparison.Ordinal) == true);
        Assert.Equal(["N2"], await Cards(staff, new ReaderSearch { GroupId = group.Id }));

        Assert.Equal(1, (await Read<ReaderBatchResult>(await staff.PutAsJsonAsync(U("/api/readers/Groups"),
            new ReaderGroupsRequest(new ReaderSelection([b.PublicId]), group.Id, Add: false), Json))).Count);
        Assert.Empty(await Cards(staff, new ReaderSearch { GroupId = group.Id }));

        // Xoá nhóm → bỏ thành viên; nhóm của đơn vị khác không thấy.
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/reader-groups/Delete/{other.PublicId}"))).StatusCode);
        Assert.Empty(await Read<List<long>>(await staff.GetAsync(U($"/api/readers/Groups/{a.PublicId}"))));
        Assert.Equal(HttpStatusCode.NotFound, (await Staff(tenantId + 500).PutAsJsonAsync(U("/api/readers/Groups"),
            new ReaderGroupsRequest(new ReaderSelection([a.PublicId]), group.Id, Add: true), Json)).StatusCode);
    }

    [Fact]
    public async Task Bulk_delete_soft_deletes_selected_readers_and_needs_delete_permission()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var a = await AddReader(staff, new ReaderRequest("D1", null, "Một"));
        var b = await AddReader(staff, new ReaderRequest("D2", null, "Hai"));
        await AddReader(staff, new ReaderRequest("D3", null, "Ba"));

        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "READERS:view", "READERS:edit").PostAsJsonAsync(U("/api/readers/BulkDelete"),
            new ReaderBulkDeleteRequest([a.PublicId]), Json)).StatusCode);
        Assert.Equal(2, (await Read<ReaderBatchResult>(await staff.PostAsJsonAsync(U("/api/readers/BulkDelete"),
            new ReaderBulkDeleteRequest([a.PublicId, b.PublicId, Guid.NewGuid()]), Json))).Count);
        Assert.Equal(["D3"], await Cards(staff, new ReaderSearch()));
        Assert.Contains(_factory.PublishedOf<ReaderChanged>(), e => e.ReaderPublicId == a.PublicId && e.Deleted);
        Assert.Contains(_factory.PublishedOf<AuditRecorded>(), l => l.EntityId == b.PublicId.ToString() && l.Action == "DELETE");
        // Số thẻ đã xoá dùng lại được.
        await AddReader(staff, new ReaderRequest("D1", null, "Một mới"));
    }

    [Fact]
    public async Task Batch_update_over_the_sync_limit_runs_in_background_as_the_user_with_reason_and_skips_invalid_readers()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var c6 = await AddNamed(staff, "classes", "6B");
        var c7 = await AddNamed(staff, "classes", "7B");
        var total = ReaderResource.MaxBulk + 5;
        using (var scope = _factory.Services.CreateScope())
        {
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<PatronDbContext>();
            db.AddRange(Enumerable.Range(1, total).Select(i => Reader.Create(new ReaderData(
                $"BG{i:00000}", null, "Bạn " + i, null, null, null, null, null, null, null, null, c6.Id, null, null, null, null, null,
                i == 7 ? new DateOnly(2027, 1, 1) : new DateOnly(2025, 9, 1), null))));
            await db.SaveChangesAsync();
        }

        var filter = new ReaderSearch { ClassId = c6.Id };
        var preview = await Read<ReaderSelectionPreview>(await staff.PostAsJsonAsync(U("/api/readers/Batch/Preview"), filter, Json));
        Assert.Equal(total, preview.Count);
        var request = new HttpRequestMessage(HttpMethod.Put, U("/api/readers/BatchUpdate"))
        {
            Content = JsonContent.Create(new ReaderBatchUpdateRequest(new ReaderSelection(Search: filter, Token: preview.Token), ClassId: c7.Id,
                ExpireDate: new DateOnly(2026, 6, 30)), options: Json),
        };
        request.Headers.Add(CrudDiff.ReasonHeader, Uri.EscapeDataString("Chuyển lớp cả khối"));
        var queued = await Read<ReaderBatchResult>(await staff.SendAsync(request));
        Assert.Equal(total, queued.Count);
        Assert.NotNull(queued.JobId);

        var done = await PatronApiFactory.WaitForAsync(async () =>
        {
            var current = await Read<ReaderImportJobDto>(await staff.GetAsync(U($"/api/readers/Batch/Jobs/{queued.JobId}")));
            return current.Status is "Done" or "Failed" ? current : null;
        }, "sửa hàng loạt chạy nền xong");
        Assert.Equal(("Done", total, total - 1, 1), (done.Status, done.Processed, done.Updated, done.Failed));
        Assert.Contains("BG00007", Assert.Single(done.Errors).Message, StringComparison.Ordinal);
        Assert.Equal(total - 1, (await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"),
            new ReaderSearch { ClassId = c7.Id, PageSize = 1 }, Json))).TotalCount);

        // Nhật ký/lịch sử ghi dưới tên người bấm, kèm lý do sửa — dù worker không có request HTTP.
        var first = (await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch { CardNo = "BG00001" }, Json))).Items[0];
        var log = Assert.Single(_factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenantId && a.EntityId == first.PublicId.ToString() && a.Action == "UPDATE");
        Assert.Equal("staff", log.Actor?.Kind);
        Assert.NotNull(log.Actor?.Id);
        Assert.Equal("Chuyển lớp cả khối", JsonDocument.Parse(log.ChangesJson!).RootElement.GetProperty("_reason").GetString());
        Assert.Contains(_factory.PublishedOf<ReaderChanged>(), e => e.ReaderPublicId == first.PublicId && e.ClassId == c7.Id && e.Actor?.Kind == "staff");

        // Lượt sửa hàng loạt không lẫn với lượt nhập; cần quyền sửa; đơn vị khác không thấy.
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(U($"/api/readers/Import/Jobs/{queued.JobId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "READERS:view").GetAsync(U($"/api/readers/Batch/Jobs/{queued.JobId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Staff(tenantId + 500).GetAsync(U($"/api/readers/Batch/Jobs/{queued.JobId}"))).StatusCode);
    }
}
