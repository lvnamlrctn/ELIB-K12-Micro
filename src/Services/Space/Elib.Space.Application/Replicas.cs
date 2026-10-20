using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Space.Domain;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Patron;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

/// <summary>Trạng thái bạn đọc hiện tại lấy thẳng từ patron (/internal, service token) — chỉ khi bản sao chưa có.</summary>
public interface IReplicaSources
{
    Task<ReaderChanged?> ReaderAsync(long tenantId, string cardNo, CancellationToken ct);
}

/// <summary>
/// Bản sao bạn đọc (docs ADR-007, khuôn digital's Replicas): ghi từ event, chỉ khi Version lớn hơn bản đang có. Bản sao chưa có
/// thì hỏi patron một lần rồi lưu lại.
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
        r.Email = Cut(e.Email, 250);
        r.Phone = Cut(e.Phone, 30);
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

    /// <summary>Bạn đọc theo ReaderPublicId (token bạn đọc mang claim này) — không hỏi lại patron, chỉ đọc bản sao.</summary>
    public Task<PatronReplica?> ByReaderPublicIdAsync(Guid readerPublicId, CancellationToken ct) =>
        db.Set<PatronReplica>().FirstOrDefaultAsync(x => x.ReaderPublicId == readerPublicId && !x.Deleted, ct);

    /// <summary>
    /// Lưu bản sao vừa hỏi được từ patron. Consumer có thể vừa ghi cùng bản ghi (event tới đúng lúc) → trùng khoá: bỏ bản của mình,
    /// nơi gọi đọc lại bản consumer đã ghi.
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

    /// <summary>Upsert kho. Chưa lưu — nơi gọi SaveChanges. Không Version như bạn đọc — ghi đè theo lần phát gần nhất (xem StoreReplica).</summary>
    public async Task<StoreReplica> ApplyAsync(StoreChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var r = await db.Set<StoreReplica>().FirstOrDefaultAsync(x => x.StorePublicId == e.StorePublicId, ct);
        if (r is null) db.Set<StoreReplica>().Add(r = new StoreReplica { StorePublicId = e.StorePublicId });
        r.Code = Cut(e.Code, 20) ?? "";
        r.Name = Cut(e.Name, 250) ?? "";
        r.Deleted = e.Deleted;
        return r;
    }

    /// <summary>Danh sách kho (chưa xoá) cho dropdown khi đặt giá sách trên sơ đồ tầng.</summary>
    public Task<List<StoreOptionDto>> StoresAsync(CancellationToken ct) => db.Set<StoreReplica>().AsNoTracking().Where(x => !x.Deleted)
        .OrderBy(x => x.Code).Select(x => new StoreOptionDto(x.StorePublicId, x.Code, x.Name)).ToListAsync(ct);
}

public sealed record StoreOptionDto(Guid StorePublicId, string Code, string Name);
