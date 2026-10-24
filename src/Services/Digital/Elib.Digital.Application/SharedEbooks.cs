using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.BuildingBlocks.Crud;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Digital.Application;

/// <summary>Một file của tài liệu gốc ở đơn vị SHARED; <see cref="Deleted"/> = đã bị gỡ khỏi tài liệu gốc.</summary>
public sealed record SharedFileChange(Guid PublicId, Guid MediaFileId, bool Deleted, EbookFileFields Fields);

/// <summary>Tài liệu gốc đã đổi: <see cref="Fields"/> mang id danh mục CỦA ĐƠN VỊ SHARED (đơn vị đích ánh xạ lại theo tên/mã).</summary>
public sealed record SharedItemChange(Guid PublicId, bool Deleted, DateTimeOffset? Submited, EbookItemFields Fields, IReadOnlyList<SharedFileChange> Files);

public sealed record SharedCollectionNode(long Id, long? ParentId, string Name, string? Language, string? Link, bool AllowDownload, bool Share, int SortOrder);

public sealed record SharedSubjectNode(long Id, long? ParentId, string Name, string? Ddc, string? Language, string? Link, int SortOrder);

public sealed record SharedTopicNode(long Id, long? ParentId, string Name, bool RequiresLogin, string? Description, string? Keyword, string? PageTitle,
    string? MetaDescription, string? Language, string? Ddc, int SortOrder);

public sealed record SharedTypeNode(long Id, string Code, string? DescriptionVn, string? DescriptionEn, int SortOrder, string? Language);

/// <summary>Dữ liệu gốc đã đổi trong một khoảng mốc, dùng chung cho mọi đơn vị cùng mốc đồng bộ.</summary>
public sealed record SharedEbookChanges(
    long SharedTenantId, IReadOnlyList<SharedItemChange> Items, IReadOnlyList<SharedCollectionNode> Collections, IReadOnlyList<SharedSubjectNode> Subjects,
    IReadOnlyList<SharedTopicNode> Topics, IReadOnlyList<SharedTypeNode> Types);

/// <summary>
/// Tài liệu số dùng chung: gốc do quản trị nền tảng tạo ở đơn vị SHARED (dùng nguyên màn tài liệu số), mọi đơn vị nhận BẢN SAO chỉ đọc có file trỏ về kho của đơn vị chủ
/// (không copy PDF). Danh mục (bộ sưu tập/môn loại/chủ đề/loại tài liệu) ánh xạ theo tên/mã, tạo nếu đơn vị chưa có. Không đụng bộ đếm lượt, chính sách,
/// mượn/đặt trước hay cờ ẩn của đơn vị. Gốc bị xoá → bản sao ngừng xuất bản (giữ lượt mượn/đặt trước đang có).
/// Giới hạn v1: đổi tên/cấu trúc danh mục ở SHARED chỉ lan tới tài liệu được cập nhật sau đó.
/// </summary>
public sealed class SharedEbookDataset(
    ICrudDbContext db, IOptions<DigitalOptions> options, TimeProvider clock, EbookStatePublisher states) : ISharedDataset
{
    public const string DatasetName = "digital.ebooks";
    private const int Chunk = 25;

    public string Name => DatasetName;

    public string RequiredModule => "DIGITAL";

    private static readonly string[] AllFilters = [ElibQueryFilters.Tenant, ElibQueryFilters.SoftDelete];

    // ---- Đọc dữ liệu gốc (ngữ cảnh hệ thống) ----

    public async Task<DateTimeOffset?> MasterMarkerAsync(long sharedTenantId, CancellationToken ct)
    {
        var item = await db.Set<EbookItem>().IgnoreQueryFilters(AllFilters).AsNoTracking().Where(i => i.TenantId == sharedTenantId)
            .OrderByDescending(i => i.DeletedAt ?? i.UpdatedAt ?? i.CreatedAt).Select(i => (DateTimeOffset?)(i.DeletedAt ?? i.UpdatedAt ?? i.CreatedAt)).FirstOrDefaultAsync(ct);
        var file = await db.Set<EbookFile>().IgnoreQueryFilters(AllFilters).AsNoTracking().Where(f => f.TenantId == sharedTenantId)
            .OrderByDescending(f => f.DeletedAt ?? f.UpdatedAt ?? f.CreatedAt).Select(f => (DateTimeOffset?)(f.DeletedAt ?? f.UpdatedAt ?? f.CreatedAt)).FirstOrDefaultAsync(ct);
        return item is null ? file : file is null ? item : item > file ? item : file;
    }

    public async Task<object> LoadChangesAsync(long sharedTenantId, DateTimeOffset? since, DateTimeOffset until, CancellationToken ct)
    {
        var items = db.Set<EbookItem>().IgnoreQueryFilters(AllFilters).AsNoTracking().Where(i => i.TenantId == sharedTenantId);
        var files = db.Set<EbookFile>().IgnoreQueryFilters(AllFilters).AsNoTracking().Where(f => f.TenantId == sharedTenantId);
        if (since is { } s)
        {
            var changedFileItems = files.Where(f => (f.DeletedAt ?? f.UpdatedAt ?? f.CreatedAt) > s && (f.DeletedAt ?? f.UpdatedAt ?? f.CreatedAt) <= until).Select(f => f.EbookItemId);
            items = items.Where(i => ((i.DeletedAt ?? i.UpdatedAt ?? i.CreatedAt) > s && (i.DeletedAt ?? i.UpdatedAt ?? i.CreatedAt) <= until) || changedFileItems.Contains(i.Id));
        }
        else
        {
            items = items.Where(i => !i.IsDeleted); // đơn vị chưa đồng bộ lần nào: chỉ cần tài liệu còn tồn tại
            files = files.Where(f => !f.IsDeleted);
        }

        var rows = await items.OrderBy(i => i.Id).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var fileRows = new List<EbookFile>();
        foreach (var part in ids.Chunk(500))
            fileRows.AddRange(await files.Where(f => part.Contains(f.EbookItemId)).OrderBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync(ct));
        var byItem = fileRows.GroupBy(f => f.EbookItemId).ToDictionary(g => g.Key, g => g.ToList());

        var changes = rows.Select(r => new SharedItemChange(r.PublicId, r.IsDeleted, r.Submited, r.ToFields(),
            [.. (byItem.GetValueOrDefault(r.Id) ?? []).Where(f => f.SourcePublicId is null).Select(f => new SharedFileChange(f.PublicId, f.MediaFileId, f.IsDeleted, f.ToFields()))])).ToList();

        var collections = await db.Set<EbookCollection>().IgnoreQueryFilters(AllFilters).AsNoTracking().Where(x => x.TenantId == sharedTenantId && !x.IsDeleted)
            .OrderBy(x => x.Level).ThenBy(x => x.Id).Select(x => new SharedCollectionNode(x.Id, x.ParentId, x.Name, x.Language, x.Link, x.AllowDownload, x.Share, x.SortOrder)).ToListAsync(ct);
        var subjects = await db.Set<EbookSubject>().IgnoreQueryFilters(AllFilters).AsNoTracking().Where(x => x.TenantId == sharedTenantId && !x.IsDeleted)
            .OrderBy(x => x.Level).ThenBy(x => x.Id).Select(x => new SharedSubjectNode(x.Id, x.ParentId, x.Name, x.Ddc, x.Language, x.Link, x.SortOrder)).ToListAsync(ct);
        var topics = await db.Set<EbookTopic>().IgnoreQueryFilters(AllFilters).AsNoTracking().Where(x => x.TenantId == sharedTenantId && !x.IsDeleted)
            .OrderBy(x => x.Level).ThenBy(x => x.Id).Select(x => new SharedTopicNode(x.Id, x.ParentId, x.Name, x.RequiresLogin, x.Description, x.Keyword, x.PageTitle, x.MetaDescription, x.Language, x.Ddc, x.SortOrder)).ToListAsync(ct);
        var types = await db.Set<EbookDigType>().IgnoreQueryFilters(AllFilters).AsNoTracking().Where(x => x.TenantId == sharedTenantId && !x.IsDeleted)
            .OrderBy(x => x.Id).Select(x => new SharedTypeNode(x.Id, x.Code, x.DescriptionVn, x.DescriptionEn, x.SortOrder, x.Language)).ToListAsync(ct);
        return new SharedEbookChanges(sharedTenantId, changes, collections, subjects, topics, types);
    }

    // ---- Áp vào đơn vị (scope riêng + ITenantContext.Use) ----

    public async Task ApplyAsync(long tenantId, object changes, CancellationToken ct)
    {
        var data = (SharedEbookChanges)changes;
        var map = new CategoryMap(db, options.Value.MediaPublicPrefix, tenantId, data);
        foreach (var part in data.Items.Chunk(Chunk))
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async token =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(token);
                await ApplyChunkAsync(data.SharedTenantId, part, map, token);
                await transaction.CommitAsync(token);
            }, ct);
        }
    }

    private async Task ApplyChunkAsync(long sourceTenantId, SharedItemChange[] part, CategoryMap map, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var prefix = options.Value.MediaPublicPrefix;
        var keys = part.Select(p => (Guid?)p.PublicId).ToList();
        var existing = await db.Set<EbookItem>().Where(i => i.SourcePublicId != null && keys.Contains(i.SourcePublicId)).ToDictionaryAsync(i => i.SourcePublicId!.Value, ct);
        var touched = new Dictionary<Guid, EbookItem>();

        foreach (var change in part)
        {
            existing.TryGetValue(change.PublicId, out var item);
            if (change.Deleted)
            {
                if (item is null) continue;
                item.RetireReplica();
                touched[change.PublicId] = item;
                continue;
            }
            var fields = change.Fields with
            {
                CollectionId = await map.CollectionAsync(change.Fields.CollectionId, ct),
                SubjectId = await map.SubjectAsync(change.Fields.SubjectId, ct),
                TopicId = await map.TopicAsync(change.Fields.TopicId, ct),
                TypeId = await map.TypeAsync(change.Fields.TypeId, ct),
                LinkedBibPublicId = null, // liên kết sách in là của catalog đơn vị chủ — không có nghĩa ở đơn vị khác
            };
            if (item is null)
            {
                item = EbookItem.CreateReplica(fields, prefix, sourceTenantId, change.PublicId, change.Submited, now);
                db.Set<EbookItem>().Add(item);
            }
            else item.ApplyReplica(fields, prefix, change.Submited, now);
            touched[change.PublicId] = item;
        }
        await db.SaveChangesAsync(ct); // có Id cho tài liệu mới trước khi ghi file

        foreach (var change in part.Where(c => !c.Deleted && touched.ContainsKey(c.PublicId)))
            await SyncFilesAsync(touched[change.PublicId], change, sourceTenantId, ct);
        await db.SaveChangesAsync(ct);

        // Phát trạng thái (outbox, cùng transaction) sau khi file đã lưu để "có file để đọc" đúng.
        foreach (var item in touched.Values) await states.PublishAsync(item, deleted: false, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SyncFilesAsync(EbookItem item, SharedItemChange change, long sourceTenantId, CancellationToken ct)
    {
        var existing = await db.Set<EbookFile>().Where(f => f.EbookItemId == item.Id && f.SourcePublicId != null).ToListAsync(ct);
        foreach (var file in change.Files)
        {
            var current = existing.FirstOrDefault(f => f.SourcePublicId == file.PublicId);
            if (file.Deleted)
            {
                if (current is not null) db.Set<EbookFile>().Remove(current);
                continue;
            }
            if (current is null) db.Set<EbookFile>().Add(EbookFile.CreateReplica(item.Id, file.MediaFileId, file.Fields, sourceTenantId, file.PublicId));
            else current.ApplyReplica(file.MediaFileId, file.Fields);
        }
    }

    /// <summary>Ánh xạ id danh mục của đơn vị SHARED sang id của đơn vị đích theo tên (+ cha) hoặc mã; chưa có thì tạo (cha trước con).</summary>
    private sealed class CategoryMap(ICrudDbContext db, string prefix, long tenantId, SharedEbookChanges data)
    {
        private readonly Dictionary<long, long?> _collections = [];
        private readonly Dictionary<long, long?> _subjects = [];
        private readonly Dictionary<long, long?> _topics = [];
        private readonly Dictionary<long, long?> _types = [];

        public Task<long?> CollectionAsync(long? id, CancellationToken ct) =>
            TreeAsync(id, _collections, data.Collections, n => n.Id, n => n.ParentId, n => n.Name,
                n => { var c = EbookCollection.Create(n.Name, n.Language, n.Link, n.AllowDownload, n.Share, null, prefix, tenantId); c.Reorder(n.SortOrder); return c; }, ct);

        public Task<long?> SubjectAsync(long? id, CancellationToken ct) =>
            TreeAsync(id, _subjects, data.Subjects, n => n.Id, n => n.ParentId, n => n.Name,
                n => { var c = EbookSubject.Create(n.Name, n.Ddc, n.Language, n.Link); c.Reorder(n.SortOrder); return c; }, ct);

        public Task<long?> TopicAsync(long? id, CancellationToken ct) =>
            TreeAsync(id, _topics, data.Topics, n => n.Id, n => n.ParentId, n => n.Name,
                n => { var c = EbookTopic.Create(n.Name, n.RequiresLogin, n.Description, n.Keyword, n.PageTitle, n.MetaDescription, n.Language, n.Ddc); c.Reorder(n.SortOrder); return c; }, ct);

        public async Task<long?> TypeAsync(long? id, CancellationToken ct)
        {
            if (id is not { } masterId) return null;
            if (_types.TryGetValue(masterId, out var done)) return done;
            var node = data.Types.FirstOrDefault(t => t.Id == masterId);
            if (node is null) return _types[masterId] = null;
            var local = await db.Set<EbookDigType>().Where(t => t.Code == node.Code).Select(t => (long?)t.Id).FirstOrDefaultAsync(ct);
            if (local is null)
            {
                var entity = EbookDigType.Create(node.Code, node.DescriptionVn, node.DescriptionEn, node.SortOrder, node.Language);
                db.Set<EbookDigType>().Add(entity);
                await db.SaveChangesAsync(ct);
                local = entity.Id;
            }
            return _types[masterId] = local;
        }

        private async Task<long?> TreeAsync<TNode, TEntity>(
            long? masterId, Dictionary<long, long?> memo, IReadOnlyList<TNode> nodes, Func<TNode, long> idOf, Func<TNode, long?> parentOf, Func<TNode, string> nameOf,
            Func<TNode, TEntity> create, CancellationToken ct) where TEntity : TreeCatalogItem
        {
            if (masterId is not { } id) return null;
            if (memo.TryGetValue(id, out var done)) return done;
            var node = nodes.FirstOrDefault(n => idOf(n) == id);
            if (node is null) return memo[id] = null; // danh mục đã bị xoá ở gốc
            var parent = await TreeAsync(parentOf(node), memo, nodes, idOf, parentOf, nameOf, create, ct);
            var name = nameOf(node);
            var local = await db.Set<TEntity>().Where(x => x.Name == name && x.ParentId == parent).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
            if (local is null)
            {
                var entity = create(node);
                entity.AttachTo(parent is { } pid ? await db.Set<TEntity>().FirstAsync(x => x.Id == pid, ct) : null);
                db.Set<TEntity>().Add(entity);
                await db.SaveChangesAsync(ct);
                local = entity.Id;
            }
            return memo[id] = local;
        }
    }
}
