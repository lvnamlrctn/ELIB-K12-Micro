using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Digital;
using Elib.Digital.Api;
using Elib.Digital.Application;
using Elib.Digital.Domain;
using Elib.Digital.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Tests.Api;

/// <summary>Tài liệu số dùng chung: gốc ở đơn vị SHARED, mọi đơn vị nhận bản sao chỉ đọc (đồng bộ), chỉ ẩn/hiện được.</summary>
public sealed class SharedEbooksApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static long _shared;

    private async Task AddReplicaAsync(long tenantId, string code)
    {
        using var scope = Factory.Services.CreateScope();
        using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
        var db = scope.ServiceProvider.GetRequiredService<DigitalDbContext>();
        if (await db.Set<TenantReplicaRecord>().AnyAsync(t => t.TenantId == tenantId)) return;
        db.Add(new TenantReplicaRecord { TenantId = tenantId, Code = code, Name = code, Subdomain = $"{code.ToLowerInvariant()}-{tenantId}", Status = "Active", SyncedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>Đơn vị SHARED duy nhất của cả lớp test (mã dành riêng nên chỉ được có một).</summary>
    private async Task<long> SharedAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (_shared == 0)
            {
                _shared = NewTenant();
                await AddReplicaAsync(_shared, SharedTenant.Code);
            }
            return _shared;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<long> TargetAsync(bool licensed = true)
    {
        var tenantId = NewTenant(licensed);
        await AddReplicaAsync(tenantId, "T" + tenantId);
        return tenantId;
    }

    private async Task<SharedSyncResult> SyncAsync()
    {
        var result = await Factory.Services.GetRequiredService<SharedSyncRunner<DigitalDbContext>>().RunOnceAsync(CancellationToken.None);
        Assert.True(result.Failed == 0, $"Đồng bộ lỗi: {result.Error}");
        return result;
    }

    private async Task<int> CountItemsAsync(long tenantId, string title)
    {
        using var scope = Factory.Services.CreateScope();
        using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        return await scope.ServiceProvider.GetRequiredService<DigitalDbContext>().Set<EbookItem>().CountAsync(i => i.Title == title);
    }

    private static string Unique(string title) => $"{title} {Guid.NewGuid().ToString("N")[..8]}";

    private static async Task<EbookItemDto> FindAsync(HttpClient staff, string title)
    {
        var page = await Read<CrudPage<EbookItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Search"), new EbookItemSearch { Title = title }, Json));
        return page.Items.Single();
    }

    private static async Task<IReadOnlyList<EbookFileDto>> FilesAsync(HttpClient staff, Guid itemPublicId) =>
        (await Read<CrudPage<EbookFileDto>>(await staff.PostAsJsonAsync(U("/api/files/Search"), new EbookFileSearch { EbookItemPublicId = itemPublicId }, Json))).Items;

    /// <summary>Tài liệu gốc của SHARED: bộ sưu tập cha/con + loại + một file, công khai.</summary>
    private static async Task<(EbookItemDto Item, EbookFileDto File, string Title)> MasterItemAsync(HttpClient admin, bool free = true, string? rootName = null)
    {
        var title = Unique("Toán lớp 10");
        var root = await Read<EbookCollectionDto>(await admin.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest(rootName ?? Unique("Sách giáo trình")), Json));
        var child = await Read<EbookCollectionDto>(await admin.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest(Unique("Hình học"), ParentId: root.Id), Json));
        var type = await Read<EbookDigTypeDto>(await admin.PostAsJsonAsync(U("/api/dig-types/Add"), new EbookDigTypeRequest("GT" + Guid.NewGuid().ToString("N")[..8], "Sách giáo khoa"), Json));
        var item = await Read<EbookItemDto>(await admin.PostAsJsonAsync(U("/api/items/Add"),
            new EbookItemFields(title, CollectionId: child.Id, TypeId: type.Id, Author: "Nhiều tác giả", Free: free, AllowDownload: true), Json));
        var file = await Read<EbookFileDto>(await admin.PostAsJsonAsync(U("/api/files/Add"),
            new EbookFileRequest(item.PublicId, Guid.CreateVersion7(), FileExt: "pdf", FileSize: 1000), Json));
        return (item, file, title);
    }

    [Fact]
    public async Task Sync_gives_every_licensed_tenant_a_readonly_replica_with_mapped_categories_and_source_files_without_leaking_to_others()
    {
        var shared = await SharedAsync();
        var admin = Staff(shared);
        var a = await TargetAsync();
        var b = await TargetAsync();
        var unlicensed = await TargetAsync(licensed: false);
        var staffA = Staff(a);
        var rootName = Unique("Sách giáo trình");
        var ownRoot = await Read<EbookCollectionDto>(await staffA.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest(rootName), Json));

        var (master, masterFile, title) = await MasterItemAsync(admin, rootName: rootName);
        var result = await SyncAsync();
        Assert.Equal(0, result.Failed);

        var replica = await FindAsync(staffA, title);
        Assert.True(replica.IsReplica);
        Assert.False(replica.HiddenByTenant);
        Assert.NotEqual(master.PublicId, replica.PublicId);
        Assert.StartsWith("Hình học", replica.CollectionName, StringComparison.Ordinal);
        Assert.Equal("Nhiều tác giả", replica.Author);
        Assert.Equal((IHasStatus.Active, 1), (replica.Status, replica.TotalFile));
        // Danh mục map theo tên: "Giáo trình" sẵn có của A được giữ (không nhân đôi), "Toán" tạo mới dưới nó.
        var collectionsA = (await Read<CrudPage<EbookCollectionDto>>(await staffA.PostAsJsonAsync(U("/api/collections/Search"), new TreeCatalogSearch(), Json))).Items;
        Assert.Single(collectionsA, c => c.Name == rootName);
        Assert.Equal(ownRoot.Id, collectionsA.Single(c => c.Name == replica.CollectionName).ParentId);
        var fileA = Assert.Single(await FilesAsync(staffA, replica.PublicId));
        Assert.Equal(masterFile.MediaFileId, fileA.MediaFileId);

        // Đơn vị B cũng có; đơn vị không có license thì không; SHARED giữ gốc không thành bản sao của chính nó.
        Assert.True((await FindAsync(Staff(b), title)).IsReplica);
        Assert.Equal(0, await CountItemsAsync(unlicensed, title)); // chưa có license DIGITAL: không nhận bản sao
        Assert.False((await FindAsync(admin, title)).IsReplica);

        // Search của từng đơn vị nhận EbookChanged đúng đơn vị (không lẫn).
        Assert.Contains(Factory.PublishedOf<EbookChanged>(), e => e.TenantId == a && e.EbookPublicId == replica.PublicId && e.Status == IHasStatus.Active && e.HasFile);
        Assert.DoesNotContain(Factory.PublishedOf<EbookChanged>(), e => e.TenantId == unlicensed && e.Title == title);

        // Chạy lại không tạo trùng.
        await SyncAsync();
        Assert.Equal(1, (await Read<CrudPage<EbookItemDto>>(await staffA.PostAsJsonAsync(U("/api/items/Search"), new EbookItemSearch { Title = title }, Json))).TotalCount);
    }

    [Fact]
    public async Task Changing_or_deleting_the_master_updates_replicas_and_retiring_keeps_existing_loans()
    {
        var shared = await SharedAsync();
        var admin = Staff(shared);
        var a = await TargetAsync();
        var staffA = Staff(a);
        var (master, masterFile, title) = await MasterItemAsync(admin);
        await SyncAsync();
        var replica = await FindAsync(staffA, title);

        // Sửa tiêu đề + thêm file ở gốc → bản sao đổi theo.
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var newTitle = title + " (tái bản)";
        await Read<EbookItemDto>(await admin.PutAsJsonAsync(U($"/api/items/Update/{master.PublicId}"),
            new EbookItemFields(newTitle, CollectionId: master.CollectionId, TypeId: master.TypeId, Author: "Nhiều tác giả", Free: true, AllowDownload: true), Json));
        await Read<EbookFileDto>(await admin.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(master.PublicId, Guid.CreateVersion7(), FileExt: "pdf", SortOrder: 2), Json));
        await SyncAsync();
        var updated = await FindAsync(staffA, newTitle);
        Assert.Equal(replica.PublicId, updated.PublicId);
        Assert.Equal(2, updated.TotalFile);
        Assert.True((updated.UpdatedAt ?? updated.CreatedAt) > (replica.UpdatedAt ?? replica.CreatedAt));

        // Gỡ một file ở gốc → gỡ ở bản sao.
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await admin.DeleteAsync(U($"/api/files/Delete/{masterFile.PublicId}"));
        await SyncAsync();
        Assert.Single(await FilesAsync(staffA, replica.PublicId));

        // Xoá gốc (xoá file còn lại trước) → bản sao ngừng xuất bản, vẫn còn trong danh sách quản trị, OPAC không thấy.
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var remaining = (await FilesAsync(admin, master.PublicId)).Single();
        await admin.DeleteAsync(U($"/api/files/Delete/{remaining.PublicId}"));
        Assert.Equal(204, (int)(await admin.DeleteAsync(U($"/api/items/Delete/{master.PublicId}"))).StatusCode);
        await SyncAsync();
        var retired = await FindAsync(staffA, newTitle);
        Assert.Equal(IHasStatus.Inactive, retired.Status);
        Assert.Equal(404, (int)(await Opac(a).GetAsync(U($"/api/opac/items/{replica.PublicId}"))).StatusCode);
    }

    [Fact]
    public async Task Replicas_are_readonly_for_the_tenant_in_every_write_path()
    {
        var shared = await SharedAsync();
        var admin = Staff(shared);
        var a = await TargetAsync();
        var staffA = Staff(a);
        var (_, _, title) = await MasterItemAsync(admin);
        await SyncAsync();
        var replica = await FindAsync(staffA, title);
        var file = Assert.Single(await FilesAsync(staffA, replica.PublicId));
        var own = await Read<EbookItemDto>(await staffA.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields(Unique("Của đơn vị")), Json));

        async Task AssertReadOnly(Task<HttpResponseMessage> call)
        {
            var (status, code, _) = await Error(await call);
            Assert.Equal((403, "SHARED_READ_ONLY"), (status, code));
        }

        await AssertReadOnly(staffA.PutAsJsonAsync(U($"/api/items/Update/{replica.PublicId}"), new EbookItemFields("Sửa trộm"), Json));
        await AssertReadOnly(staffA.DeleteAsync(U($"/api/items/Delete/{replica.PublicId}")));
        await AssertReadOnly(staffA.PutAsJsonAsync(U("/api/items/ChangeStatus"), new ChangeStatusRequest(replica.PublicId, IHasStatus.Inactive), Json));
        await AssertReadOnly(staffA.PutAsJsonAsync(U($"/api/items/{replica.PublicId}/cover"), new SetCoverRequest(null), Json));
        await AssertReadOnly(staffA.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(replica.PublicId, Guid.CreateVersion7(), FileExt: "pdf"), Json));
        await AssertReadOnly(staffA.PutAsJsonAsync(U($"/api/files/Update/{file.PublicId}"), new EbookFileRequest(replica.PublicId, file.MediaFileId, Description: "đổi"), Json));
        await AssertReadOnly(staffA.DeleteAsync(U($"/api/files/Delete/{file.PublicId}")));
        await AssertReadOnly(staffA.PutAsJsonAsync(U($"/api/items/{replica.PublicId}/metadata"), new EbookMetadataSave([]), Json));

        // Hàng loạt: bản sao bị bỏ qua kèm lý do, tài liệu của đơn vị vẫn xử lý.
        var selection = new ItemSelection([replica.PublicId, own.PublicId]);
        var status = await Read<ItemBulkResult>(await staffA.PutAsJsonAsync(U("/api/items/BulkStatus"), new ItemBulkStatusRequest(selection, IHasStatus.Inactive), Json));
        Assert.Equal(1, status.Updated);
        Assert.Equal("SHARED_READ_ONLY", Assert.Single(status.Skipped).Code);
        var deleted = await Read<ItemBulkResult>(await staffA.PostAsJsonAsync(U("/api/items/BulkDelete"), selection, Json));
        Assert.Equal("SHARED_READ_ONLY", Assert.Single(deleted.Skipped).Code);
        Assert.Equal(IHasStatus.Active, (await FindAsync(staffA, title)).Status);
        Assert.Equal(title, (await FindAsync(staffA, title)).Title);
    }

    [Fact]
    public async Task Tenant_can_hide_and_unhide_a_replica_and_sync_never_overwrites_the_choice()
    {
        var shared = await SharedAsync();
        var admin = Staff(shared);
        var a = await TargetAsync();
        var staffA = Staff(a);
        var (master, _, title) = await MasterItemAsync(admin);
        await SyncAsync();
        var replica = await FindAsync(staffA, title);
        var opac = Opac(a);
        Assert.Equal(replica.PublicId, Assert.Single((await Read<CrudPage<OpacItemSummary>>(await opac.PostAsJsonAsync(U("/api/opac/items/Search"), new OpacItemSearch(title), Json))).Items).PublicId);

        var hidden = await Read<EbookItemDto>(await staffA.PutAsJsonAsync(U($"/api/items/{replica.PublicId}/Hidden"), new SetHiddenRequest(true), Json));
        Assert.True(hidden.HiddenByTenant);
        Assert.Equal(0, (await Read<CrudPage<OpacItemSummary>>(await opac.PostAsJsonAsync(U("/api/opac/items/Search"), new OpacItemSearch(title), Json))).TotalCount);
        Assert.Equal(404, (int)(await opac.GetAsync(U($"/api/opac/items/{replica.PublicId}"))).StatusCode);
        // Search chỉ mục theo event: bản mới nhất của bản sao phải là không xuất bản.
        var latest = Factory.PublishedOf<EbookChanged>().Where(e => e.TenantId == a && e.EbookPublicId == replica.PublicId).OrderBy(e => e.Version).Last();
        Assert.Equal(IHasStatus.Inactive, latest.Status);

        // Gốc đổi → đồng bộ lại nhưng cờ ẩn của đơn vị giữ nguyên và event vẫn là không xuất bản.
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await Read<EbookItemDto>(await admin.PutAsJsonAsync(U($"/api/items/Update/{master.PublicId}"),
            new EbookItemFields(title + " v2", CollectionId: master.CollectionId, TypeId: master.TypeId, Free: true), Json));
        await SyncAsync();
        var afterSync = await FindAsync(staffA, title + " v2");
        Assert.True(afterSync.HiddenByTenant);
        Assert.Equal(IHasStatus.Inactive, Factory.PublishedOf<EbookChanged>().Where(e => e.TenantId == a && e.EbookPublicId == replica.PublicId).OrderBy(e => e.Version).Last().Status);
        Assert.Equal(404, (int)(await opac.GetAsync(U($"/api/opac/items/{replica.PublicId}"))).StatusCode);

        var shown = await Read<EbookItemDto>(await staffA.PutAsJsonAsync(U($"/api/items/{replica.PublicId}/Hidden"), new SetHiddenRequest(false), Json));
        Assert.False(shown.HiddenByTenant);
        Assert.Equal(200, (int)(await opac.GetAsync(U($"/api/opac/items/{replica.PublicId}"))).StatusCode);
        Assert.Equal(IHasStatus.Active, Factory.PublishedOf<EbookChanged>().Where(e => e.TenantId == a && e.EbookPublicId == replica.PublicId).OrderBy(e => e.Version).Last().Status);

        // Ẩn/hiện chỉ dành cho bản sao, không phải tài liệu của đơn vị.
        var own = await Read<EbookItemDto>(await staffA.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields(Unique("Của đơn vị")), Json));
        Assert.Equal("NOT_SHARED_REPLICA", (await Error(await staffA.PutAsJsonAsync(U($"/api/items/{own.PublicId}/Hidden"), new SetHiddenRequest(true), Json))).Code);
    }

    [Fact]
    public async Task Reading_a_replica_file_asks_media_for_the_shared_tenant_and_borrowing_follows_the_tenants_own_policy()
    {
        var shared = await SharedAsync();
        var admin = Staff(shared);
        var a = await TargetAsync();
        var staffA = Staff(a);
        var (_, masterFile, title) = await MasterItemAsync(admin, free: false);
        await SyncAsync();
        var replica = await FindAsync(staffA, title);
        var file = Assert.Single(await FilesAsync(staffA, replica.PublicId));

        var cardNo = "SH-" + a;
        var reader = Reader(a, cardNo, typeId: 1, email: "reader@example.com");
        await Publish(reader);
        var readerClient = ReaderClient(a, cardNo, reader.ReaderPublicId);
        // Chưa mượn thì chưa đọc được (tài liệu không công khai) — quy tắc mượn của đơn vị áp dụng cho bản sao.
        Assert.Equal("EBOOK_NOT_BORROWED", (await Error(await readerClient.GetAsync(U($"/api/opac/items/{replica.PublicId}/files/{file.PublicId}/url")))).Code);

        // Chính sách theo bộ sưu tập của ĐƠN VỊ (bộ sưu tập cục bộ đã ánh xạ) chặn đọc.
        var localCollection = replica.CollectionId!.Value;
        var policy = await Read<PolicyDigitalByCollectionDto>(await staffA.PostAsJsonAsync(U("/api/policies-by-collection/Add"),
            new PolicyDigitalByCollectionRequest(1, localCollection, Read: false), Json));
        Assert.Equal("EBOOK_READ_DENIED", (await Error(await readerClient.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(replica.PublicId), Json))).Code);
        await staffA.DeleteAsync(U($"/api/policies-by-collection/Delete/{policy.PublicId}"));

        var loan = await Read<EbookLoanDto>(await readerClient.PostAsJsonAsync(U("/api/reader/loans/Borrow"), new ReaderBorrowRequest(replica.PublicId), Json));
        Assert.Equal(1, loan.Status);
        var queueBefore = Factory.Media.RequestedSourceTenants.Count;
        var url = await Read<EbookFileUrl>(await readerClient.GetAsync(U($"/api/opac/items/{replica.PublicId}/files/{file.PublicId}/url")));
        Assert.Contains(masterFile.MediaFileId.ToString("N"), url.Url, StringComparison.Ordinal);
        // File nằm ở kho của đơn vị SHARED: media được hỏi theo đơn vị nguồn, không phải đơn vị hiện tại.
        Assert.Equal(shared, Factory.Media.RequestedSourceTenants.Skip(queueBefore).Last());

        // Tài liệu của chính đơn vị: đơn vị hiện tại (không truyền đơn vị nguồn).
        var own = await Read<EbookItemDto>(await staffA.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields(Unique("Của đơn vị"), Free: true), Json));
        var ownFile = await Read<EbookFileDto>(await staffA.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(own.PublicId, Guid.CreateVersion7(), FileExt: "pdf"), Json));
        queueBefore = Factory.Media.RequestedSourceTenants.Count;
        await Read<EbookFileUrl>(await Opac(a).GetAsync(U($"/api/opac/items/{own.PublicId}/files/{ownFile.PublicId}/url")));
        Assert.Null(Factory.Media.RequestedSourceTenants.Skip(queueBefore).Last());
    }

    [Fact]
    public async Task Only_the_shared_tenant_can_see_the_sync_status_and_run_it()
    {
        var shared = await SharedAsync();
        var other = await TargetAsync();

        await SyncAsync();
        var status = await Read<SharedSyncStatus>(await Staff(shared).GetAsync(U("/api/shared-sync/Status")));
        Assert.True(status.IsShared);
        Assert.NotNull(status.LastRunAt);
        Assert.Equal(200, (int)(await Staff(shared).PostAsync(U("/api/shared-sync/Run"), null)).StatusCode);

        var (statusCode, code, _) = await Error(await Staff(other).GetAsync(U("/api/shared-sync/Status")));
        Assert.Equal((403, "NOT_SHARED_TENANT"), (statusCode, code));
        Assert.Equal(403, (int)(await Staff(other).PostAsync(U("/api/shared-sync/Run"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(shared, "DIGITAL_ITEMS:view").GetAsync(U("/api/shared-sync/Status"))).StatusCode);
    }
}
