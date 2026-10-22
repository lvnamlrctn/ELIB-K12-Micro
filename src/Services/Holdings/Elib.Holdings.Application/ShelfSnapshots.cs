using Elib.BuildingBlocks.Crud;
using Elib.Contracts.Events.Space;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>
/// Dựng bản sao giá/ngăn/tầng từ event của space. Ghi đè theo lần phát gần nhất (<c>OccurredAt</c>) — event cũ tới lệch thứ tự bị bỏ qua.
/// Chưa lưu — nơi gọi SaveChanges.
/// </summary>
public sealed class ShelfSnapshots(ICrudDbContext db)
{
    private static string Cut(string? value, int max) => value is null ? "" : value.Length <= max ? value : value[..max];

    public async Task ApplyAsync(ShelfChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var r = await db.Set<ShelfReplica>().FirstOrDefaultAsync(x => x.ShelfPublicId == e.ShelfPublicId, ct);
        if (r is null) db.Set<ShelfReplica>().Add(r = new ShelfReplica { ShelfPublicId = e.ShelfPublicId });
        else if (e.OccurredAt < r.SourceAt) return;
        (r.Code, r.Name, r.FloorPublicId, r.StorePublicId) = (Cut(e.Code, 20), Cut(e.Name, 250), e.FloorPublicId, e.StorePublicId);
        (r.PositionX, r.PositionY, r.Width, r.Height, r.ColorHex) = (e.PositionX, e.PositionY, e.Width, e.Height, e.ColorHex is null ? null : Cut(e.ColorHex, 20));
        (r.Deleted, r.SourceAt) = (e.Deleted, e.OccurredAt);

        db.Set<ShelfRowReplica>().RemoveRange(await db.Set<ShelfRowReplica>().Where(x => x.ShelfPublicId == e.ShelfPublicId).ToListAsync(ct));
        if (e.Deleted) return;
        foreach (var row in e.Rows)
            db.Set<ShelfRowReplica>().Add(new ShelfRowReplica
            {
                ShelfPublicId = e.ShelfPublicId, RowPublicId = row.RowPublicId, RowIndex = row.RowIndex, DdcStart = Cut(row.DdcStart, 30), DdcEnd = Cut(row.DdcEnd, 30),
                Description = row.Description is null ? null : Cut(row.Description, 500),
            });
    }

    public async Task ApplyAsync(FloorChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var r = await db.Set<FloorReplica>().FirstOrDefaultAsync(x => x.FloorPublicId == e.FloorPublicId, ct);
        if (r is null) db.Set<FloorReplica>().Add(r = new FloorReplica { FloorPublicId = e.FloorPublicId });
        else if (e.OccurredAt < r.SourceAt) return;
        (r.Name, r.FloorNumber, r.BuildingName) = (Cut(e.Name, 250), e.FloorNumber, e.BuildingName is null ? null : Cut(e.BuildingName, 250));
        (r.CanvasWidth, r.CanvasHeight, r.LayoutImageUrl) = (e.CanvasWidth, e.CanvasHeight, e.LayoutImageUrl is null ? null : Cut(e.LayoutImageUrl, 500));
        (r.Deleted, r.SourceAt) = (e.Deleted, e.OccurredAt);
    }
}
