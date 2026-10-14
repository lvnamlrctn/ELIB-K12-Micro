using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Đổi số ĐKCB của một bản sách; <see cref="StoreId"/> có giá trị thì chuyển luôn kho.</summary>
public sealed record ReRegisterRequest(string? OldBarcode, string? NewBarcode, long? StoreId = null);

/// <summary>Một dòng đổi mã (nhập Excel, xem trước). Error khác null = dòng lỗi.</summary>
public sealed record ReRegisterLine(int Row, string? OldBarcode, string? NewBarcode, string? StoreCode, string? Error);

/// <summary>
/// Kết quả nhập Excel đổi mã. <see cref="DryRun"/> hoặc có dòng lỗi → chưa đổi mã nào (<see cref="Applied"/> = 0) — sửa file rồi nhập lại
/// cả file, như nhập danh mục.
/// </summary>
public sealed record ReRegisterImportResult(int Total, int Applied, bool DryRun, IReadOnlyList<ReRegisterLine> Lines)
{
    public int ErrorCount => Lines.Count(l => l.Error is not null);
}

/// <summary>
/// Đăng ký lại mã vạch (monolith: StoreReRegisterBarcodeController + BarcodeReRegisterService, quyền RE_REGISTER). Mã mới không trùng
/// mã khác trong đơn vị (không phân biệt hoa/thường; đổi hoa/thường của chính nó được). Bản đang mượn không đổi được — lượt mượn ở
/// circulation giữ mã cũ để quầy trả bằng mã quét (monolith đổi luôn mã trong mọi bảng lịch sử; ở đây lịch sử là ảnh chụp lúc mượn,
/// bản sao ở circulation/search cập nhật qua ItemChanged).
/// </summary>
public sealed class BarcodeReRegistration(ICrudDbContext db, ItemEvents events, ItemResource items, ICrudAuditSink? audit = null)
{
    private static readonly IReadOnlyList<CrudImportColumn> Columns =
    [
        new("old", "ĐKCB cũ", true, "Số ĐKCB đang dùng.", "Mã vạch cũ", "Ma vach cu", "Old barcode", "OldBarcode"),
        new("new", "ĐKCB mới", true, "Số ĐKCB thay thế, không trùng mã khác trong đơn vị.", "Mã vạch mới", "Ma vach moi", "New barcode", "NewBarcode"),
        new("store", "Mã kho", false, "Chuyển luôn sang kho này (trống = giữ kho).", "Kho", "Store"),
    ];

    public static byte[] Template() => CrudExcel.Template("Dang ky lai ma vach", Columns);

    public async Task<ItemDto> ReRegisterAsync(ReRegisterRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await ItemBarcodes.RequireAsync(db, request.OldBarcode, ct);
        var newBarcode = Item.NormalizeBarcode(request.NewBarcode);
        if (request.StoreId is { } store && !await db.Set<Store>().AnyAsync(s => s.Id == store, ct))
            throw new BusinessRuleException("STORE_NOT_FOUND", "Kho đã chọn không còn.");
        await EnsureFreeAsync(item, newBarcode, ct);
        await ApplyAsync(item, newBarcode, request.StoreId, ct);
        await db.SaveChangesAsync(ct);
        return await items.GetAsync(item.PublicId, ct);
    }

    /// <summary>Đổi mã hàng loạt từ Excel (monolith: Import + tác vụ nền xem trước). Một transaction cho cả file.</summary>
    public async Task<ReRegisterImportResult> ImportAsync(Stream file, bool dryRun, CancellationToken ct)
    {
        var rows = CrudExcel.Read(file, Columns);
        var stores = await db.Set<Store>().AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Id, StringComparer.OrdinalIgnoreCase, ct);
        var oldKeys = rows.Select(r => r.Get("old")).Where(v => v is not null).Select(v => Item.Key(v!)).Distinct().ToList();
        var newKeys = rows.Select(r => r.Get("new")).Where(v => v is not null).Select(v => Item.Key(v!)).Distinct().ToList();
        var byKey = new Dictionary<string, Item>(StringComparer.Ordinal);
        foreach (var chunk in oldKeys.Concat(newKeys).Distinct().Chunk(500))
            foreach (var item in await db.Set<Item>().Where(i => chunk.Contains(i.BarcodeKey)).ToListAsync(ct))
                byKey[item.BarcodeKey] = item;

        var lines = new List<ReRegisterLine>();
        var plan = new List<(Item Item, string NewBarcode, long? StoreId)>();
        var usedOld = new HashSet<string>(StringComparer.Ordinal);
        var usedNew = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var (oldValue, newValue, storeCode) = (row.Get("old"), row.Get("new"), row.Get("store"));
            try
            {
                var oldKey = Item.Key(row.Required("old", "ĐKCB cũ"));
                var newBarcode = Item.NormalizeBarcode(row.Required("new", "ĐKCB mới"));
                var newKey = Item.Key(newBarcode);
                var item = byKey.GetValueOrDefault(oldKey) ?? throw new BusinessRuleException("NOT_FOUND", $"Không có ĐKCB '{oldValue}'.");
                if (!usedOld.Add(oldKey)) throw new BusinessRuleException("DUPLICATE_ROW", $"ĐKCB '{oldValue}' xuất hiện nhiều lần trong file.");
                if (!usedNew.Add(newKey)) throw new BusinessRuleException("DUPLICATE_ROW", $"ĐKCB mới '{newBarcode}' xuất hiện nhiều lần trong file.");
                // Không đổi chéo trong cùng file (A→B, B→A): mã mới phải chưa thuộc bản nào khác.
                if (byKey.TryGetValue(newKey, out var other) && other.Id != item.Id)
                    throw new ConflictException("BARCODE_EXISTS", $"Số ĐKCB '{newBarcode}' đã có.");
                long? store = storeCode is { } code
                    ? stores.TryGetValue(code.Trim(), out var id) ? id : throw new BusinessRuleException("STORE_NOT_FOUND", $"Không có kho mã '{code}'.")
                    : null;
                if (item.OnLoan)
                    throw new BusinessRuleException("ITEM_ON_LOAN", $"ĐKCB {item.Barcode} đang được bạn đọc mượn — trả sách trước khi đăng ký lại mã.");
                plan.Add((item, newBarcode, store));
                lines.Add(new ReRegisterLine(row.Number, oldValue, newBarcode, storeCode, null));
            }
            catch (BusinessRuleException ex)
            {
                lines.Add(new ReRegisterLine(row.Number, oldValue, newValue, storeCode, ex.Message));
            }
        }
        if (dryRun || lines.Any(l => l.Error is not null)) return new ReRegisterImportResult(rows.Count, 0, dryRun, lines);

        foreach (var (item, newBarcode, store) in plan) await ApplyAsync(item, newBarcode, store, ct);
        await db.SaveChangesAsync(ct);
        return new ReRegisterImportResult(rows.Count, plan.Count, DryRun: false, lines);
    }

    private async Task EnsureFreeAsync(Item item, string newBarcode, CancellationToken ct)
    {
        var key = Item.Key(newBarcode);
        if (await db.Set<Item>().AnyAsync(x => x.BarcodeKey == key && x.Id != item.Id, ct))
            throw new ConflictException("BARCODE_EXISTS", $"Số ĐKCB '{newBarcode}' đã có.");
    }

    private async Task ApplyAsync(Item item, string newBarcode, long? storeId, CancellationToken ct)
    {
        var old = item.Barcode;
        item.ReRegister(newBarcode, storeId);
        await events.PublishAsync(item, deleted: false, ct);
        if (audit is not null)
            await audit.RecordAsync(new CrudAuditEntry(nameof(Item), "Bản sách", item.PublicId, CrudChange.Updated, $"Đăng ký lại mã vạch {old} → {item.Barcode}",
                new System.Text.Json.Nodes.JsonObject { ["Barcode"] = new System.Text.Json.Nodes.JsonArray(old, item.Barcode) }), ct);
    }
}
