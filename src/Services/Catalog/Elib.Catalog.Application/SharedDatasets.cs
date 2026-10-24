using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elib.Catalog.Application;

/// <summary>Một thay đổi của mục từ điển gốc: <see cref="Deleted"/> = đã xoá ở đơn vị SHARED.</summary>
public sealed record DictionaryChange(string Kind, string Value, string? Description, bool Deleted);

public sealed record DictionaryChanges(IReadOnlyList<DictionaryChange> Items);

/// <summary>Một thay đổi cấu hình ISBD gốc; khoá theo mã loại biểu ghi (id loại khác nhau giữa các đơn vị).</summary>
public sealed record IsbdChange(string BibTypeCode, IReadOnlyList<IsbdRule> Rules, bool Deleted);

public sealed record IsbdChanges(IReadOnlyList<IsbdChange> Items);

/// <summary>Truy vấn dữ liệu GỐC của đơn vị SHARED: bỏ lọc đơn vị (ngữ cảnh hệ thống) và lọc xoá mềm để thấy dòng đã xoá.</summary>
internal static class SharedMaster
{
    public static IQueryable<T> Of<T>(ICrudDbContext db, long sharedTenantId) where T : TenantEntity =>
        db.Set<T>().IgnoreQueryFilters([ElibQueryFilters.Tenant, ElibQueryFilters.SoftDelete]).AsNoTracking().Where(x => x.TenantId == sharedTenantId);

    /// <summary>Mốc thay đổi lớn nhất = max(UpdatedAt ?? CreatedAt, DeletedAt).</summary>
    public static async Task<DateTimeOffset?> MarkerAsync<T>(IQueryable<T> master, CancellationToken ct) where T : TenantEntity
    {
        var created = await master.MaxAsync(x => (DateTimeOffset?)x.CreatedAt, ct);
        var updated = await master.MaxAsync(x => x.UpdatedAt, ct);
        var deleted = await master.MaxAsync(x => x.DeletedAt, ct);
        DateTimeOffset?[] all = [created, updated, deleted];
        return all.Where(v => v is not null).Max();
    }

    /// <summary>Dòng đổi trong (since, until]; since = null → lần đầu: chỉ dòng chưa xoá.</summary>
    public static IQueryable<T> Changed<T>(IQueryable<T> master, DateTimeOffset? since, DateTimeOffset until) where T : TenantEntity
    {
        if (since is not { } s) return master.Where(x => !x.IsDeleted && (x.UpdatedAt ?? x.CreatedAt) <= until);
        return master.Where(x => (((x.UpdatedAt ?? x.CreatedAt) > s && (x.UpdatedAt ?? x.CreatedAt) <= until) || (x.DeletedAt > s && x.DeletedAt <= until)));
    }
}

/// <summary>
/// Từ điển/thesaurus dùng chung: gốc ở đơn vị SHARED (<see cref="DictionaryTerm.IsReplica"/> = false), bản sao chỉ đọc ở các đơn vị. Khoá tự nhiên (Kind, Key):
/// đơn vị đã có mục riêng cùng khoá thì mục riêng thắng và không tạo bản sao; không bao giờ đụng dòng của đơn vị.
/// </summary>
public sealed class DictionarySharedDataset(ICrudDbContext db) : ISharedDataset
{
    public const string DatasetName = "catalog.dictionary";
    private const int Chunk = 1000;

    public string Name => DatasetName;

    public string RequiredModule => "CATALOG";

    public Task<DateTimeOffset?> MasterMarkerAsync(long sharedTenantId, CancellationToken ct) =>
        SharedMaster.MarkerAsync(SharedMaster.Of<DictionaryTerm>(db, sharedTenantId).Where(t => !t.IsReplica), ct);

    public async Task<object> LoadChangesAsync(long sharedTenantId, DateTimeOffset? since, DateTimeOffset until, CancellationToken ct)
    {
        var rows = await SharedMaster.Changed(SharedMaster.Of<DictionaryTerm>(db, sharedTenantId).Where(t => !t.IsReplica), since, until)
            .OrderBy(t => t.Id).Select(t => new DictionaryChange(t.Kind, t.Value, t.Description, t.IsDeleted)).ToListAsync(ct);
        return new DictionaryChanges(rows);
    }

    public async Task ApplyAsync(long tenantId, object changes, CancellationToken ct)
    {
        // Khoá cuối cùng của mỗi (Kind, Key): còn dòng chưa xoá → upsert; chỉ còn dòng đã xoá → xoá bản sao.
        var final = new Dictionary<(string Kind, string Key), DictionaryChange>();
        foreach (var change in ((DictionaryChanges)changes).Items)
        {
            var key = (change.Kind, DictionaryTerm.KeyOf(change.Value));
            if (!final.TryGetValue(key, out var old) || old.Deleted || !change.Deleted) final[key] = change;
        }

        var set = db.Set<DictionaryTerm>();
        foreach (var chunk in final.Chunk(Chunk))
        {
            var keys = chunk.Select(c => c.Key.Key).Distinct().ToList();
            var existing = (await set.Where(t => keys.Contains(t.Key)).ToListAsync(ct)).ToDictionary(t => (t.Kind, t.Key));
            foreach (var (key, change) in chunk.Select(c => (c.Key, c.Value)))
            {
                existing.TryGetValue(key, out var term);
                if (change.Deleted)
                {
                    if (term is { IsReplica: true }) set.Remove(term);
                }
                else if (term is null) set.Add(DictionaryTerm.CreateReplica(change.Kind, change.Value, change.Description));
                else if (term.IsReplica && (term.Value != DictionaryTerm.Normalize(change.Value) || term.Description != (string.IsNullOrWhiteSpace(change.Description) ? null : change.Description.Trim())))
                    term.ApplyReplica(change.Value, change.Description);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}

/// <summary>
/// Cấu hình ISBD dùng chung: khoá gốc là mã loại biểu ghi (<see cref="BibType.Code"/>), map sang id loại của đơn vị. Bản sao chỉ lấp loại đơn vị CHƯA có cấu hình
/// (unique theo loại); loại không có ở đơn vị thì bỏ qua (ghi log).
/// </summary>
public sealed partial class IsbdSharedDataset(ICrudDbContext db, ILogger<IsbdSharedDataset> logger) : ISharedDataset
{
    public const string DatasetName = "catalog.isbd";

    public string Name => DatasetName;

    public string RequiredModule => "CATALOG";

    public Task<DateTimeOffset?> MasterMarkerAsync(long sharedTenantId, CancellationToken ct) =>
        SharedMaster.MarkerAsync(SharedMaster.Of<IsbdConfig>(db, sharedTenantId).Where(c => !c.IsReplica), ct);

    public async Task<object> LoadChangesAsync(long sharedTenantId, DateTimeOffset? since, DateTimeOffset until, CancellationToken ct)
    {
        var configs = await SharedMaster.Changed(SharedMaster.Of<IsbdConfig>(db, sharedTenantId).Where(c => !c.IsReplica), since, until).OrderBy(c => c.Id).ToListAsync(ct);
        var typeIds = configs.Select(c => c.BibTypeId).Distinct().ToList();
        var codes = await SharedMaster.Of<BibType>(db, sharedTenantId).Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var items = configs.Where(c => codes.ContainsKey(c.BibTypeId))
            .Select(c => new IsbdChange(codes[c.BibTypeId], [.. c.Rules.Select(Clone)], c.IsDeleted)).ToList();
        return new IsbdChanges(items);
    }

    public async Task ApplyAsync(long tenantId, object changes, CancellationToken ct)
    {
        var final = new Dictionary<string, IsbdChange>();
        foreach (var change in ((IsbdChanges)changes).Items)
            if (!final.TryGetValue(change.BibTypeCode, out var old) || old.Deleted || !change.Deleted) final[change.BibTypeCode] = change;
        if (final.Count == 0) return;

        var codes = final.Keys.ToList();
        var types = await db.Set<BibType>().Where(t => codes.Contains(t.Code)).ToDictionaryAsync(t => t.Code, t => t.Id, ct);
        var typeIds = types.Values.ToList();
        var existing = (await db.Set<IsbdConfig>().Where(c => typeIds.Contains(c.BibTypeId)).ToListAsync(ct)).ToDictionary(c => c.BibTypeId);
        var set = db.Set<IsbdConfig>();
        foreach (var (code, change) in final)
        {
            if (!types.TryGetValue(code, out var typeId))
            {
                if (!change.Deleted) LogUnmapped(logger, tenantId, code);
                continue;
            }
            existing.TryGetValue(typeId, out var config);
            if (change.Deleted)
            {
                if (config is { IsReplica: true }) set.Remove(config);
            }
            else if (config is null) set.Add(IsbdConfig.CreateReplica(typeId, change.Rules.Select(Clone)));
            else if (config.IsReplica) config.ApplyReplica(change.Rules.Select(Clone));
        }
        await db.SaveChangesAsync(ct);
    }

    private static IsbdRule Clone(IsbdRule r) => new() { Tag = r.Tag, Code = r.Code, Prefix = r.Prefix, Suffix = r.Suffix, Separator = r.Separator };

    [LoggerMessage(Level = LogLevel.Information, Message = "ISBD dùng chung: đơn vị {TenantId} không có loại biểu ghi mã {Code}, bỏ qua")]
    private static partial void LogUnmapped(ILogger logger, long tenantId, string code);
}
