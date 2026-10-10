using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Patron;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>Trạng thái hiện tại lấy thẳng từ service gốc (/internal, service token) — chỉ khi bản sao chưa có.</summary>
public interface IReplicaSources
{
    Task<ReaderChanged?> ReaderAsync(long tenantId, string cardNo, CancellationToken ct);
    Task<ItemChanged?> ItemAsync(long tenantId, string barcode, CancellationToken ct);
    Task<BibChanged?> BibAsync(long tenantId, long mfn, CancellationToken ct);
}

/// <summary>
/// Bản sao bạn đọc/bản sách/biểu ghi (docs ADR-007): ghi từ event, chỉ khi Version lớn hơn bản đang có. Quầy mượn trả đọc bản sao
/// cùng DB — patron/holdings/catalog đang down vẫn mượn trả được. Bản sao chưa có (service mới triển khai, event chưa tới) thì hỏi
/// service gốc một lần rồi lưu lại.
/// </summary>
public sealed class Replicas(ICrudDbContext db, IReplicaSources sources, ITenantContext tenant)
{
    /// <summary>Upsert bạn đọc. Chưa lưu — nơi gọi SaveChanges.</summary>
    public async Task<PatronReplica> ApplyAsync(ReaderChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var r = await db.Set<PatronReplica>().FirstOrDefaultAsync(x => x.ReaderPublicId == e.ReaderPublicId, ct);
        if (r is null) db.Set<PatronReplica>().Add(r = new PatronReplica { ReaderPublicId = e.ReaderPublicId, Version = -1 });
        if (e.Version <= r.Version) return r;
        r.CardNo = Cut(PatronReplica.Key(e.CardNo), 50)!;
        r.FullName = Cut(e.FullName, 250) ?? "";
        r.ReaderTypeId = e.ReaderTypeId;
        r.ReaderTypeName = Cut(e.ReaderTypeName, 250);
        r.ClassName = Cut(e.ClassName, 250);
        r.CourseName = Cut(e.CourseName, 250);
        r.PhotoId = e.PhotoId;
        r.Email = Cut(e.Email, 250);
        r.Phone = Cut(e.Phone, 30);
        r.Status = e.Status;
        r.ExpireDate = e.ExpireDate;
        r.Deleted = e.Deleted;
        r.Version = e.Version;
        return r;
    }

    /// <summary>Upsert bản sách. Chưa lưu — nơi gọi SaveChanges.</summary>
    public async Task<ItemReplica> ApplyAsync(ItemChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var r = await db.Set<ItemReplica>().FirstOrDefaultAsync(x => x.ItemPublicId == e.ItemPublicId, ct);
        if (r is null) db.Set<ItemReplica>().Add(r = new ItemReplica { ItemPublicId = e.ItemPublicId, Version = -1 });
        if (e.Version <= r.Version) return r;
        r.Barcode = Cut(e.Barcode.Trim(), 50)!;
        r.BarcodeKey = ItemReplica.Key(r.Barcode);
        r.BibPublicId = e.BibPublicId;
        r.Mfn = e.Mfn;
        r.StoreId = e.StoreId;
        r.StoreName = Cut(e.StoreName, 250);
        r.Status = Cut(e.Status, 1) ?? "";
        r.Deleted = e.Deleted;
        r.Version = e.Version;
        return r;
    }

    /// <summary>Upsert biểu ghi. Chưa lưu — nơi gọi SaveChanges.</summary>
    public async Task<BibSnapshot> ApplyAsync(BibChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var r = await db.Set<BibSnapshot>().FirstOrDefaultAsync(x => x.BibPublicId == e.BibPublicId, ct);
        if (r is null) db.Set<BibSnapshot>().Add(r = new BibSnapshot { BibPublicId = e.BibPublicId, Version = -1 });
        if (e.Version <= r.Version) return r;
        r.Mfn = e.Mfn;
        r.Title = Cut(e.Title, 1000) ?? "";
        r.Author = Cut(e.Author, 500);
        r.Ddc = Cut(e.Ddc, 50);
        r.Deleted = e.Deleted;
        r.Version = e.Version;
        return r;
    }

    /// <summary>Bạn đọc theo số thẻ (bản đã xoá → null).</summary>
    public async Task<PatronReplica?> ReaderAsync(string cardNo, CancellationToken ct)
    {
        var key = PatronReplica.Key(cardNo);
        if (key.Length == 0) return null;
        var r = await db.Set<PatronReplica>().FirstOrDefaultAsync(x => x.CardNo == key && !x.Deleted, ct);
        if (r is null && await sources.ReaderAsync(tenant.RequireTenantId(), key, ct) is { } state)
        {
            r = await ApplyAsync(state, ct);
            if (!await TrySaveAsync(r, ct))
                r = await db.Set<PatronReplica>().FirstOrDefaultAsync(x => x.ReaderPublicId == state.ReaderPublicId, ct);
        }
        return r is { Deleted: false } ? r : null;
    }

    /// <summary>Bản sách theo số ĐKCB (bản đã xoá → null).</summary>
    public async Task<ItemReplica?> ItemAsync(string barcode, CancellationToken ct)
    {
        var key = ItemReplica.Key(barcode);
        if (key.Length == 0) return null;
        var r = await db.Set<ItemReplica>().FirstOrDefaultAsync(x => x.BarcodeKey == key && !x.Deleted, ct);
        if (r is null && await sources.ItemAsync(tenant.RequireTenantId(), key, ct) is { } state)
        {
            r = await ApplyAsync(state, ct);
            if (!await TrySaveAsync(r, ct))
                r = await db.Set<ItemReplica>().FirstOrDefaultAsync(x => x.ItemPublicId == state.ItemPublicId, ct);
        }
        return r is { Deleted: false } ? r : null;
    }

    /// <summary>Đảm bảo có nhan đề cho các MFN (hiển thị) — thiếu thì hỏi catalog.</summary>
    public async Task EnsureBibsAsync(IEnumerable<long> mfns, CancellationToken ct)
    {
        var wanted = mfns.Distinct().ToList();
        var have = await db.Set<BibSnapshot>().Where(x => wanted.Contains(x.Mfn)).Select(x => x.Mfn).ToListAsync(ct);
        foreach (var mfn in wanted.Except(have))
        {
            if (await sources.BibAsync(tenant.RequireTenantId(), mfn, ct) is not { } state) continue;
            await TrySaveAsync(await ApplyAsync(state, ct), ct);
        }
    }

    /// <summary>
    /// Lưu bản sao vừa hỏi được từ service gốc. Consumer có thể vừa ghi cùng bản ghi (event tới đúng lúc) → trùng khoá:
    /// bỏ bản của mình, nơi gọi đọc lại bản consumer đã ghi.
    /// </summary>
    private async Task<bool> TrySaveAsync<T>(T replica, CancellationToken ct) where T : class
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Set<T>().Entry(replica).State = EntityState.Detached;
            return false;
        }
    }

    private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}
