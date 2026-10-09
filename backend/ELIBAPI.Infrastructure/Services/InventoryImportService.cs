using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Kết quả quét/ghi nhận 1 mã vạch trong 1 phiên kiểm kê. <see cref="Entity"/> null khi
/// <c>previewOnly</c> (chưa ghi gì) hoặc khi thất bại. <see cref="Unregistered"/>: mã không có trong CSDL (vẫn ghi nhận).</summary>
public record InventoryScanOutcome(bool Success, string? Error, InventoryBarcode? Entity, bool AlreadyScanned, bool Unregistered = false);

/// <summary>Dòng "đã quét" kèm nhan đề + tên kho (trước đây danh sách không có nhan đề).</summary>
public sealed record InventoryScanRow(long Id, Guid PublicId, long? InventoryId, string? Barcode, long? StoreId, string? StoreName,
    string? BibTitle, int? CheckStoreStatus, int? CheckBorrow, int? CheckStatus, int? CheckRegisteter, DateTime? CreatedRowDate);

/// <summary>Tổng hợp đợt kiểm kê. <c>Lost</c> = "Thiếu (nghi mất)", xem <see cref="InventoryImportService.MissingQuery"/>.</summary>
public sealed record InventoryTally(int Scanned, int NotCorrectStore, int NotRegister, int OnLoan, int Damaged, int Lost);

public sealed record InventoryMissingRow(string Barcode, long? BibId, string? BibTitle, int? StoreId, string? StoreName, string? Status);

/// <summary>Kiểm kê kho — dùng chung giữa <c>StoreInventoryController</c> (quét tay, danh sách, tổng hợp, báo cáo) và tác vụ
/// nền "inventory-import" (Đợt 22.5). Port các sửa lỗi ELIB-LRC 10-04, giữ lọc đơn vị của K12: mọi thao tác theo đơn vị của
/// phiên kiểm kê; mã ĐKCB tra theo (mã, đơn vị).
/// Mã cờ giữ theo dữ liệu ELIB cũ: 1 = bình thường, 2 = có vấn đề (sai kho / đang mượn / chưa đăng ký).</summary>
public class InventoryImportService(ELIBAPIDbContext db)
{
    public const int Yes = 1, No = 2;
    public const int ClosedStatus = 2;
    public const int MaxPageSize = 200;

    /// <summary>Trạng thái ĐKCB không tính là "thiếu" khi chưa quét: L đã ghi nhận mất, S đã thanh lý.</summary>
    private static readonly string[] SettledStatuses = ["L", "S"];

    /// <summary>Đơn vị của phiên kiểm kê (null nếu không tìm thấy / thuộc đơn vị khác người gọi).</summary>
    public async Task<(long? TenantId, bool Found)> ResolveTenantAsync(long inventoryId, long? callerTenantId)
    {
        var inv = await db.Inventories.AsNoTracking()
            .Where(x => x.Id == inventoryId && x.IsDelete != 2 && (!callerTenantId.HasValue || x.TenantId == callerTenantId))
            .Select(x => new { x.TenantId }).FirstOrDefaultAsync();
        return inv == null ? (null, false) : (inv.TenantId, true);
    }

    /// <summary>Đợt đã kết thúc (Status = 2) không nhận thêm mã — trước đây vẫn nhận quét / nhập Excel.</summary>
    public async Task<bool> IsClosedAsync(long inventoryId) =>
        await db.Inventories.AnyAsync(x => x.Id == inventoryId && x.Status == ClosedStatus);

    public async Task<InventoryScanOutcome> ScanAsync(long inventoryId, long? tenantId, string barcodeValue,
        long? storeId, long? userId, bool previewOnly)
    {
        var code = (barcodeValue ?? "").Trim();
        if (code == "") return new(false, "Vui lòng nhập mã vạch", null, false);
        if (await IsClosedAsync(inventoryId)) return new(false, "Đợt kiểm kê đã kết thúc, không nhận thêm mã vạch", null, false);

        var (barcode, ambiguous) = await BarcodeTenantLookup.ByValueAsync(db.Barcodes, code, tenantId, normalize: true);
        if (ambiguous) return new(false, BarcodeTenantLookup.AmbiguousMessage, null, false);
        var value = barcode?.BarcodeValue ?? code;
        var lower = value.ToLower();

        var existing = await db.InventoryBarcodes.FirstOrDefaultAsync(x => x.InventoryId == inventoryId && x.IsDelete != 2
            && x.Barcode != null && (x.Barcode == value || x.Barcode.Trim().ToLower() == lower));
        if (existing != null) return new(true, null, existing, true, existing.CheckRegisteter == No);

        if (previewOnly) return new(true, null, null, false, barcode == null);

        // Đang mượn = phiếu mượn mở thật (PrintLoans.Open: Status khác "R" và chưa có BookIn) — trước đây chỉ xét Status,
        // nên phiếu đã trả theo cách ghi cũ ("1"/"O" + BookIn) bị tính là đang mượn.
        var onLoan = barcode != null && await PrintLoans.Open(db).AnyAsync(o =>
            o.Reg_Seq_Id == barcode.Id || (o.Barcode == barcode.BarcodeValue && (o.TenantId ?? 0) == (barcode.TenantId ?? 0)));

        var entity = new InventoryBarcode
        {
            Barcode          = value,
            StoreId          = (long?)barcode?.Store,
            InventoryId      = inventoryId,
            // Không chọn kho khi quét thì không đối chiếu kho (trước đây mọi bản bị tính "sai kho"); mã chưa đăng ký cũng vậy.
            CheckStoreStatus = barcode == null || storeId == null || barcode.Store == (int?)storeId ? Yes : No,
            CheckStatus      = Yes, // Barcode.Status là trạng thái lưu thông (R/L/B/...), không phải tình trạng vật lý
            CheckBorrow      = onLoan ? No : Yes,
            // Mã không có trong CSDL vẫn ghi nhận là "chưa đăng ký" (như ELIB cũ) — trước đây chỉ báo lỗi.
            CheckRegisteter  = barcode != null ? Yes : No,
            TenantId         = barcode?.TenantId ?? tenantId,
            PublicId         = Guid.NewGuid(),
            CreatedRowDate   = LibraryClock.Now,
            CreatedRowBy     = userId,
        };
        db.InventoryBarcodes.Add(entity);
        await db.SaveChangesAsync();
        return new(true, null, entity, false, barcode == null);
    }

    /// <summary>Danh sách đã quét. Bộ lọc ≤ 0 (frontend gửi -1 = "Tất cả") là không lọc.</summary>
    public async Task<(List<InventoryScanRow> Items, int Total)> SearchScannedAsync(long inventoryId, int? checkStoreStatus, int? checkBorrow,
        int? checkStatus, int? pageIndex, int? pageSize)
    {
        var q = db.InventoryBarcodes.Where(x => x.InventoryId == inventoryId && x.IsDelete != 2);
        if (checkStoreStatus > 0) q = q.Where(x => x.CheckStoreStatus == checkStoreStatus);
        if (checkBorrow > 0) q = q.Where(x => x.CheckBorrow == checkBorrow);
        if (checkStatus > 0) q = q.Where(x => x.CheckStatus == checkStatus);

        var size = Math.Clamp(pageSize ?? 20, 1, MaxPageSize);
        var page = Math.Max(pageIndex ?? 1, 1);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync();
        return (await ToRowsAsync(items), total);
    }

    public async Task<List<InventoryScanRow>> ScannedAsync(long inventoryId) =>
        await ToRowsAsync(await db.InventoryBarcodes.Where(x => x.InventoryId == inventoryId && x.IsDelete != 2).OrderBy(x => x.Id).ToListAsync());

    /// <summary>Tổng hợp — trước đây "Chưa đăng ký" và "Thiếu (mất)" luôn bằng 0 vì backend không trả.</summary>
    public async Task<InventoryTally> SummaryAsync(long inventoryId, long? tenantId)
    {
        var flags = await db.InventoryBarcodes.Where(x => x.InventoryId == inventoryId && x.IsDelete != 2)
            .Select(x => new { x.CheckStoreStatus, x.CheckBorrow, x.CheckStatus, x.CheckRegisteter }).ToListAsync();
        return new InventoryTally(
            Scanned: flags.Count,
            NotCorrectStore: flags.Count(x => x.CheckStoreStatus == No),
            NotRegister: flags.Count(x => x.CheckRegisteter == No),
            OnLoan: flags.Count(x => x.CheckBorrow == No),
            Damaged: flags.Count(x => x.CheckStatus != Yes),
            Lost: await MissingQuery(inventoryId, tenantId).CountAsync());
    }

    public async Task<List<InventoryMissingRow>> MissingAsync(long inventoryId, long? tenantId)
    {
        var rows = await MissingQuery(inventoryId, tenantId)
            .OrderBy(b => b.Store).ThenBy(b => b.BarcodeValue)
            .Select(b => new { b.BarcodeValue, b.BibId, b.Store, b.Status }).ToListAsync();
        var bibIds = rows.Where(r => r.BibId != null).Select(r => r.BibId!.Value).Distinct().ToList();
        var titles = await TitlesAsync(bibIds);
        var stores = await StoreNamesAsync(rows.Select(r => (long?)r.Store));
        return rows.Select(r => new InventoryMissingRow(r.BarcodeValue!, r.BibId, r.BibId is long b ? titles.GetValueOrDefault(b) : null,
            r.Store, r.Store is int s ? stores.GetValueOrDefault(s) : null, r.Status)).ToList();
    }

    /// <summary>"Nghi mất": bản của đơn vị phiên kiểm kê, thuộc các kho có bản đã quét trong đợt (đợt chưa quét gì thì rỗng —
    /// không coi cả thư viện là mất), chưa được quét, không có phiếu mượn mở, chưa ghi nhận mất/thanh lý. Chạy trong CSDL.
    /// Trước đây tải MỌI ĐKCB của đơn vị vào bộ nhớ và coi mọi bản chưa quét là nghi mất, dù đợt chỉ kiểm 1 kho.</summary>
    public IQueryable<Barcode> MissingQuery(long inventoryId, long? tenantId)
    {
        var scanned = db.InventoryBarcodes.Where(x => x.InventoryId == inventoryId && x.IsDelete != 2);
        var stores = scanned.Where(x => x.StoreId != null).Select(x => x.StoreId);
        return db.Barcodes.Where(b => b.IsDelete != 2 && b.BarcodeValue != null && b.Store != null
            && b.TenantId == tenantId
            && stores.Contains((long?)b.Store)
            && !scanned.Any(x => x.Barcode == b.BarcodeValue)
            && (b.Status == null || !SettledStatuses.Contains(b.Status))
            && !PrintLoans.Open(db).Any(o => o.Barcode != null && o.Barcode == b.BarcodeValue && (o.TenantId ?? 0) == (b.TenantId ?? 0)));
    }

    public async Task<List<InventoryScanRow>> ToRowsAsync(List<InventoryBarcode> items)
    {
        var codes = items.Where(x => x.Barcode != null).Select(x => x.Barcode!).Distinct().ToList();
        var bibByCode = (await db.Barcodes.Where(b => b.BarcodeValue != null && codes.Contains(b.BarcodeValue) && b.IsDelete != 2 && b.BibId != null)
                .Select(b => new { b.BarcodeValue, b.TenantId, b.BibId }).ToListAsync())
            .GroupBy(b => (b.BarcodeValue!, b.TenantId ?? 0)).ToDictionary(g => g.Key, g => g.First().BibId!.Value);
        var titles = await TitlesAsync(bibByCode.Values.Distinct().ToList());
        var stores = await StoreNamesAsync(items.Select(x => x.StoreId));
        return items.Select(x => new InventoryScanRow(
            x.Id, x.PublicId, x.InventoryId, x.Barcode, x.StoreId, x.StoreId is long s ? stores.GetValueOrDefault(s) : null,
            x.Barcode != null && bibByCode.TryGetValue((x.Barcode, x.TenantId ?? 0), out var bib) ? titles.GetValueOrDefault(bib) : null,
            x.CheckStoreStatus, x.CheckBorrow, x.CheckStatus, x.CheckRegisteter, x.CreatedRowDate)).ToList();
    }

    private async Task<Dictionary<long, string?>> TitlesAsync(List<long> bibIds)
    {
        var titles = new Dictionary<long, string?>();
        foreach (var chunk in bibIds.Chunk(5000))   // tránh danh sách IN quá dài khi báo cáo kho lớn
            foreach (var x in await db.BibXmls.Where(x => chunk.Contains(x.BibId)).Select(x => new { x.BibId, x.Title }).ToListAsync())
                titles.TryAdd(x.BibId, x.Title);   // BibXml trùng BibId (dữ liệu cũ) không làm hỏng báo cáo
        return titles;
    }

    private async Task<Dictionary<long, string?>> StoreNamesAsync(IEnumerable<long?> ids)
    {
        var list = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        return list.Count == 0 ? [] : await db.Stores.Where(s => list.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
    }
}
