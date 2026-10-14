using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Holdings;
using Elib.Holdings.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>
/// Phát <see cref="ItemChanged"/> cho mọi thay đổi bản sách (outbox — gọi trước SaveChanges để event cùng transaction với dữ liệu).
/// Tên kho đọc một lần mỗi scope.
/// </summary>
public sealed class ItemEvents(ICrudDbContext db, IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor)
{
    private Dictionary<long, string>? _stores;

    public async Task PublishAsync(Item item, bool deleted, CancellationToken ct)
    {
        if (item.PublicId == Guid.Empty) item.PublicId = Guid.CreateVersion7(); // bản mới: interceptor chỉ gán khi còn trống
        await publisher.Publish(await ToEventAsync(item, deleted, ct), ct);
    }

    public async Task<ItemChanged> ToEventAsync(Item item, bool deleted, CancellationToken ct)
    {
        var stores = _stores ??= await db.Set<Store>().AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return new ItemChanged
        {
            TenantId = tenant.RequireTenantId(),
            Actor = new EventActor(actor.Id, actor.Kind),
            ItemPublicId = item.PublicId,
            Barcode = item.Barcode,
            BibPublicId = item.BibPublicId,
            Mfn = item.BibId,
            StoreId = item.StoreId,
            StoreName = item.StoreId is { } s && stores.TryGetValue(s, out var name) ? name : null,
            Status = item.Status,
            Deleted = deleted,
            Version = item.Version,
        };
    }
}

/// <summary>Bản sách bị bỏ qua trong thao tác hàng loạt, kèm lý do (đang mượn, đã thanh lý…).</summary>
public sealed record SkippedItem(string Barcode, string Reason);

/// <summary>Kết quả thao tác hàng loạt theo ĐKCB: số bản đã xử lý, mã không có, bản bị bỏ qua.</summary>
public sealed record ItemBatchResult(int Done, IReadOnlyList<string> NotFound, IReadOnlyList<SkippedItem> Skipped);

/// <summary>Request chỉ có một số ĐKCB (khôi phục mất, huỷ thanh lý, nhập kho…).</summary>
public sealed record BarcodeRequest(string? Barcode);

/// <summary>Tra bản sách theo danh sách ĐKCB quét/dán (không phân biệt hoa/thường, bỏ trùng).</summary>
public static class ItemBarcodes
{
    public const int MaxBatch = 2000;

    public static List<string> Clean(IEnumerable<string?>? barcodes) =>
        (barcodes ?? []).Select(b => b?.Trim() ?? "").Where(b => b.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Bản sách (đang theo dõi để sửa) theo mã + danh sách mã không có. Quá <see cref="MaxBatch"/> mã hoặc rỗng → lỗi.</summary>
    public static async Task<(List<Item> Items, List<string> NotFound)> LoadAsync(ICrudDbContext db, IEnumerable<string?>? barcodes, CancellationToken ct)
    {
        var codes = Clean(barcodes);
        if (codes.Count is 0 or > MaxBatch)
            throw new BusinessRuleException("BARCODES_EMPTY", $"Nhập từ 1 đến {MaxBatch} số ĐKCB.");
        var keys = codes.Select(Item.Key).ToList();
        var items = new List<Item>();
        foreach (var chunk in keys.Chunk(500))
            items.AddRange(await db.Set<Item>().Where(x => chunk.Contains(x.BarcodeKey)).ToListAsync(ct));
        var found = items.Select(i => i.BarcodeKey).ToHashSet(StringComparer.Ordinal);
        return (items.OrderBy(i => i.BarcodeKey, StringComparer.Ordinal).ToList(), codes.Where(c => !found.Contains(Item.Key(c))).ToList());
    }

    /// <summary>Một bản sách theo mã — không có thì 404.</summary>
    public static async Task<Item> RequireAsync(ICrudDbContext db, string? barcode, CancellationToken ct)
    {
        var key = Item.Key(barcode ?? "");
        if (key.Length == 0) throw new BusinessRuleException("BARCODE_REQUIRED", "Nhập số ĐKCB.");
        return await db.Set<Item>().FirstOrDefaultAsync(x => x.BarcodeKey == key, ct)
            ?? throw new NotFoundException("Bản sách", barcode!.Trim());
    }
}
