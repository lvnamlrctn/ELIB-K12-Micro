using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Tồn kho THỜI GIAN THỰC của tài liệu in (port từ ELIB-LRC), đối chiếu thẳng sổ mượn (BookOut/BookIn) và hàng đợi đặt
/// mượn (BookRequest) thay vì đọc <c>Barcode.Status</c> đã đóng băng trong index.
///
/// Đa đơn vị: mã ĐKCB chỉ duy nhất trong 1 đơn vị (xem <see cref="BarcodeTenantLookup"/>), nên phiếu mượn/đặt mượn chỉ
/// "chiếm" một bản khi cùng mã VÀ cùng <c>TenantId</c> — nếu không, bản "NV.00001" của đơn vị A bị coi là đang mượn chỉ
/// vì đơn vị B có phiếu mở cùng mã. Hàm không lọc đơn vị của <c>copies</c>; bên gọi truyền tập bản đã lọc.
/// </summary>
public static class PrintBookAvailability
{
    /// <summary>Số bản của một biểu ghi: tổng số và số đang thực sự cho mượn được.</summary>
    public readonly record struct Counts(int CopyCount, int AvailableCount);

    /// <summary>
    /// Bản SẴN SÀNG cho mượn: đang ở trong kho (<c>Status = "R"</c>, hoặc chưa gán trạng thái — dữ liệu cũ), không có
    /// phiếu mượn còn mở (<see cref="PrintLoans.Open"/>) và không có yêu cầu đặt mượn đang chờ, cùng mã + cùng đơn vị.
    /// </summary>
    public static IQueryable<Barcode> AvailableCopies(ELIBAPIDbContext db, IQueryable<Barcode> copies) =>
        copies.Where(c => (c.Status == "R" || c.Status == null || c.Status == "") && c.BarcodeValue != null
            && !PrintLoans.Open(db).Any(o => o.Barcode != null && o.Barcode == c.BarcodeValue && o.TenantId == c.TenantId)
            && !db.BookRequests.Any(r => r.Barcode != null && r.Barcode == c.BarcodeValue && r.TenantId == c.TenantId
                                         && r.Status == "pending" && r.IsDelete != 2));

    /// <summary>Đếm bản theo từng Bibid (2 truy vấn gộp, tránh N+1). Biểu ghi không có bản nào không xuất hiện.</summary>
    public static async Task<Dictionary<long, Counts>> CountAsync(
        ELIBAPIDbContext db, IReadOnlyCollection<long> bibIds, CancellationToken ct = default)
    {
        if (bibIds.Count == 0) return [];

        var ids = bibIds.Distinct().ToList();
        var copies = db.Barcodes.AsNoTracking().Where(c => c.BibId != null && ids.Contains(c.BibId.Value) && c.IsDelete != 2);

        var total = await copies.GroupBy(c => c.BibId!.Value).Select(g => new { BibId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var available = await AvailableCopies(db, copies).GroupBy(c => c.BibId!.Value)
            .Select(g => new { BibId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.BibId, x => x.Count, ct);

        return total.ToDictionary(t => t.BibId, t => new Counts(t.Count, available.GetValueOrDefault(t.BibId)));
    }

    /// <summary>Tồn kho của đúng một biểu ghi.</summary>
    public static async Task<Counts> CountOneAsync(ELIBAPIDbContext db, long bibId, CancellationToken ct = default)
    {
        var map = await CountAsync(db, [bibId], ct);
        return map.TryGetValue(bibId, out var c) ? c : new Counts(0, 0);
    }
}
