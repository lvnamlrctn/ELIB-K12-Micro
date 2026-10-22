using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Holdings.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Một dòng lịch sử di chuyển của bản sách, đã dịch mã kho/giá/ngăn sang tên.</summary>
public sealed record ItemMovementDto(
    long Id, string Kind, string KindName, DateTimeOffset At, long? ActorId, string? ActorName, string? FromStatus, string? ToStatus,
    string? FromStore, string? ToStore, string? FromShelf, string? ToShelf, string? FromRow, string? ToRow, string? Reason);

/// <summary>
/// Ghi và đọc lịch sử di chuyển từng bản sách (xếp giá, xếp vào giá/ngăn, xếp giá lại, chuyển kho, đăng ký lại mã vạch). Ghi cùng transaction
/// với thay đổi bản sách — nơi gọi SaveChanges sau cùng.
/// </summary>
public sealed class ItemMovements(ICrudDbContext db, ICurrentActor actor, IHttpContextAccessor http, TimeProvider clock)
{
    /// <summary>Ghi di chuyển của <paramref name="item"/> so với <paramref name="from"/>/<paramref name="fromStatus"/> chụp trước khi đổi.</summary>
    public void Record(Item item, string kind, ItemLocation from, string fromStatus, string? reason = null) =>
        db.Set<ItemMovement>().Add(ItemMovement.Between(item, kind, from, fromStatus, actor.Id, AuditActor.Name(http.HttpContext), clock.GetUtcNow(), reason));

    public async Task<IReadOnlyList<ItemMovementDto>> ListAsync(Guid itemPublicId, CancellationToken ct)
    {
        var itemId = await db.Set<Item>().AsNoTracking().Where(i => i.PublicId == itemPublicId).Select(i => (long?)i.Id).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Bản sách", itemPublicId);
        var rows = await db.Set<ItemMovement>().AsNoTracking().Where(m => m.ItemId == itemId).OrderByDescending(m => m.At).ThenByDescending(m => m.Id).Take(200).ToListAsync(ct);
        var stores = await db.Set<Store>().AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var shelves = await db.Set<ShelfReplica>().AsNoTracking().ToDictionaryAsync(s => s.ShelfPublicId, s => s.Deleted ? s.Name + " (đã xoá)" : s.Name, ct);
        var rowsByPublic = await db.Set<ShelfRowReplica>().AsNoTracking().ToDictionaryAsync(r => r.RowPublicId, r => $"Ngăn {r.RowIndex} ({r.DdcStart} – {r.DdcEnd})", ct);
        string? Store(long? id) => id is { } v ? stores.GetValueOrDefault(v) : null;
        string? Shelf(Guid? id) => id is { } v ? shelves.GetValueOrDefault(v, "Giá đã xoá") : null;
        string? Row(Guid? id) => id is { } v ? rowsByPublic.GetValueOrDefault(v, "Ngăn đã xoá") : null;
        return rows.Select(m => new ItemMovementDto(m.Id, m.Kind, MovementKind.Names.GetValueOrDefault(m.Kind, m.Kind), m.At, m.ActorId, m.ActorName,
            m.FromStatus, m.ToStatus, Store(m.FromStoreId), Store(m.ToStoreId), Shelf(m.FromShelfPublicId), Shelf(m.ToShelfPublicId),
            Row(m.FromRowPublicId), Row(m.ToRowPublicId), m.Reason)).ToList();
    }
}
