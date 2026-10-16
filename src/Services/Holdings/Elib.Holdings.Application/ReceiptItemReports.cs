using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>
/// Bản sách cho báo cáo của acquisition (phân bổ kho, sổ ĐKCB). <see cref="ReceiptPublicIds"/> = các phiếu nhận khớp bộ lọc ở
/// acquisition (service sở hữu phiếu); null = mọi bản kể cả bản không qua phiếu nhận (như monolith). Bộ lọc biểu ghi (MFN, nhan đề,
/// tác giả, NXB, năm) áp trên bản sao biểu ghi của holdings.
/// </summary>
public sealed record ReceiptItemReportRequest(
    IReadOnlyList<Guid>? ReceiptPublicIds = null, long? StoreId = null, int Skip = 0, int Take = 500, long? MfnFrom = null, long? MfnTo = null,
    string? Title = null, string? Author = null, string? Publisher = null, string? PublishYear = null);

public sealed record ReceiptStoreName(long Id, string Name);

public sealed record ReceiptItemReportRow(
    string Barcode, long Mfn, Guid? ReceiptPublicId, Guid? LinePublicId, long? StoreId, string? StoreName, string? Title, string? Author, string? Publisher,
    string? PublishYear, string? Ddc, DateOnly RegisteredOn);

public sealed record ReceiptItemReportPage(IReadOnlyList<ReceiptItemReportRow> Items, int Total);

public sealed record ReceiptItemStoreCount(long? StoreId, string? StoreName, int Copies);

/// <summary>Truy vấn bản sách theo phiếu nhận cho báo cáo bổ sung (monolith: AcquisitionReportService — StoreAllocation, AccessionRegister).</summary>
public sealed class ReceiptItemReports(ICrudDbContext db)
{
    public const int MaxReceipts = 5000;
    public const int MaxTake = 20_000;

    private IQueryable<Item> Items(ReceiptItemReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var items = db.Set<Item>().AsNoTracking();
        if (request.ReceiptPublicIds is { } ids)
        {
            if (ids.Count > MaxReceipts)
                throw new BusinessRuleException("REPORT_TOO_WIDE", $"Bộ lọc khớp {ids.Count:N0} phiếu nhận (tối đa {MaxReceipts:N0}) — thu hẹp khoảng ngày hoặc bộ lọc.");
            var set = ids.ToList();
            items = items.Where(i => i.ReceiptPublicId != null && set.Contains(i.ReceiptPublicId.Value));
        }
        if (request.StoreId is { } store) items = items.Where(i => i.StoreId == store);
        if (request.MfnFrom is { } from) items = items.Where(i => i.BibId >= from);
        if (request.MfnTo is { } to) items = items.Where(i => i.BibId <= to);
        var (title, author, publisher, year) = (Term(request.Title), Term(request.Author), Term(request.Publisher), Term(request.PublishYear));
        if (title is not null || author is not null || publisher is not null || year is not null)
        {
            var bibs = db.Set<BibSnapshot>().AsNoTracking().Where(b => !b.Deleted);
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            if (title is not null) bibs = bibs.Where(b => b.Title.ToLower().Contains(title));
            if (author is not null) bibs = bibs.Where(b => b.Author != null && b.Author.ToLower().Contains(author));
            if (publisher is not null) bibs = bibs.Where(b => b.Publisher != null && b.Publisher.ToLower().Contains(publisher));
            if (year is not null) bibs = bibs.Where(b => b.PublishYear != null && b.PublishYear.Contains(year));
#pragma warning restore CA1862, CA1304, CA1311
            items = items.Where(i => bibs.Any(b => b.BibPublicId == i.BibPublicId));
        }
        return items;
    }

    private static string? Term(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    /// <summary>Tên kho của đơn vị — acquisition chỉ giữ id kho trên phiếu nhận.</summary>
    public async Task<IReadOnlyList<ReceiptStoreName>> StoresAsync(CancellationToken ct) =>
        await db.Set<Store>().AsNoTracking().OrderBy(s => s.Name).Select(s => new ReceiptStoreName(s.Id, s.Name)).ToListAsync(ct);

    /// <summary>Một trang bản sách theo thứ tự số ĐKCB (monolith: BarcodeNumber rồi Id).</summary>
    public async Task<ReceiptItemReportPage> PageAsync(ReceiptItemReportRequest request, CancellationToken ct)
    {
        var items = Items(request);
        var rows = await (
            from i in items
            join b in db.Set<BibSnapshot>() on i.BibPublicId equals b.BibPublicId into bj
            from b in bj.DefaultIfEmpty()
            join st in db.Set<Store>() on i.StoreId equals (long?)st.Id into sj
            from st in sj.DefaultIfEmpty()
            orderby i.BarcodeKey, i.Id
            select new
            {
                i.Barcode, Mfn = i.BibId, i.ReceiptPublicId, i.ReceiptLinePublicId, i.StoreId, StoreName = st == null ? null : st.Name,
                Title = b == null ? null : b.Title, Author = b == null ? null : b.Author, Publisher = b == null ? null : b.Publisher,
                PublishYear = b == null ? null : b.PublishYear, Ddc = b == null ? null : b.Ddc, i.CreatedAt,
            }).Skip(Math.Max(0, request.Skip)).Take(Math.Clamp(request.Take, 1, MaxTake)).ToListAsync(ct);
        return new ReceiptItemReportPage(
            [.. rows.Select(r => new ReceiptItemReportRow(r.Barcode, r.Mfn, r.ReceiptPublicId, r.ReceiptLinePublicId, r.StoreId, r.StoreName, r.Title, r.Author,
                r.Publisher, r.PublishYear, r.Ddc, VietnamDate.Of(r.CreatedAt)))],
            await items.CountAsync(ct));
    }

    /// <summary>Số bản theo kho (kho chưa chọn → StoreId null).</summary>
    public async Task<IReadOnlyList<ReceiptItemStoreCount>> StoreCountsAsync(ReceiptItemReportRequest request, CancellationToken ct)
    {
        var stores = await db.Set<Store>().AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var groups = await Items(request).GroupBy(i => i.StoreId).Select(g => new { StoreId = g.Key, Copies = g.Count() }).ToListAsync(ct);
        return [.. groups.OrderBy(g => g.StoreId ?? long.MaxValue)
            .Select(g => new ReceiptItemStoreCount(g.StoreId, g.StoreId is { } id && stores.TryGetValue(id, out var name) ? name : null, g.Copies))];
    }
}
