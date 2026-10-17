using System.Globalization;
using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Platform;
using Microsoft.EntityFrameworkCore;

namespace Elib.Acquisition.Application;

/// <summary>Trạng thái biểu ghi lấy thẳng từ catalog (/internal, service token) — khi bản sao chưa có, hoặc dựng bản sao lần đầu.</summary>
public interface ICatalogBibs
{
    Task<BibChanged?> GetAsync(long tenantId, long mfn, CancellationToken ct);

    Task<StatePage<BibChanged>> PageAsync(long tenantId, long after, CancellationToken ct);
}

/// <summary>Biểu ghi chọn được cho dòng đơn đặt / phiếu nhận.</summary>
public sealed record BibOption(Guid BibPublicId, long Mfn, string Title, string? Author, string? Publisher, string? PublishYear, string? Isbns);

/// <summary>Điều kiện "Tra trùng" (ELIB-LRC: MFN từ, Đến, Năm xuất bản, Nhà XB, Nhan đề, Tác giả, Loại tài liệu). Chữ: không phân biệt dấu.</summary>
public sealed record BibSearchRequest(long? MfnFrom = null, long? MfnTo = null, string? PublishYear = null, string? Publisher = null, string? Title = null,
    string? Author = null, long? BibTypeId = null, int PageIndex = 1, int PageSize = 10);

/// <summary>Một biểu ghi tìm được — <see cref="HasReceipt"/>/<see cref="HasOrder"/>: đã có trong phiếu nhận / đơn đặt nào chưa.</summary>
public sealed record BibSearchRow(Guid BibPublicId, long Mfn, string Title, string? Author, string? Publisher, string? PublishYear, string? Isbns,
    string? BibTypeName, bool HasReceipt, bool HasOrder);

public sealed record BibTypeOption(long Id, string Name);

/// <summary>Bản sao biểu ghi (BibSnapshot): ghi từ event <see cref="BibChanged"/>, thiếu thì hỏi catalog.</summary>
public sealed class BibSnapshots(ICrudDbContext db, ICatalogBibs catalog, ITenantContext tenant)
{
    /// <summary>Upsert theo BibPublicId; event cũ hơn bản đang có bị bỏ qua. Chưa lưu — nơi gọi SaveChanges.</summary>
    public async Task<BibSnapshot> ApplyAsync(BibChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var snapshot = db.Set<BibSnapshot>().Local.FirstOrDefault(x => x.BibPublicId == e.BibPublicId)
            ?? await db.Set<BibSnapshot>().FirstOrDefaultAsync(x => x.BibPublicId == e.BibPublicId, ct);
        if (snapshot is null)
        {
            snapshot = new BibSnapshot { BibPublicId = e.BibPublicId, Version = -1 };
            db.Set<BibSnapshot>().Add(snapshot);
        }
        // Cùng version mà dòng chưa đồng bộ (có từ trước khi bản sao có loại biểu ghi/khoá tìm kiếm) thì vẫn ghi lại.
        if (e.Version < snapshot.Version || (e.Version == snapshot.Version && snapshot.Synced)) return snapshot;

        snapshot.Mfn = e.Mfn;
        snapshot.Title = Cut(e.Title, 1000) ?? "";
        snapshot.Author = Cut(e.Author, 500);
        snapshot.Publisher = Cut(e.Publisher, 500);
        snapshot.PublishYear = Cut(e.PublishYear, 4);
        snapshot.Isbns = e.Isbns.Count > 0 ? Cut(string.Join(", ", e.Isbns), 1000) : null;
        snapshot.Ddc = Cut(e.Ddc, 50);
        snapshot.Cutter = Cut(e.Cutter, 50);
        snapshot.BibTypeId = e.BibTypeId;
        snapshot.BibTypeName = Cut(e.BibTypeName, 250);
        snapshot.TitleKey = Cut(BibSnapshot.Fold(e.Title), 1000) ?? "";
        snapshot.AuthorKey = Cut(BibSnapshot.Fold(e.Author), 500) ?? "";
        snapshot.PublisherKey = Cut(BibSnapshot.Fold(e.Publisher), 500) ?? "";
        snapshot.Synced = true;
        snapshot.CatalogedAt = e.CatalogedAt;
        snapshot.Deleted = e.Deleted;
        snapshot.Version = e.Version;
        return snapshot;
    }

    public const int MaxSearchPageSize = 100;

    /// <summary>
    /// "Tra trùng" (ELIB-LRC: dialog tìm biểu ghi có sẵn khi thêm dòng phiếu nhận): MFN từ–đến, năm XB, nhà XB, nhan đề, tác giả (không
    /// phân biệt dấu), loại tài liệu; kèm cờ biểu ghi đã có trong phiếu nhận / đơn đặt nào chưa. Sắp MFN giảm dần, phân trang.
    /// </summary>
    public async Task<CrudPage<BibSearchRow>> SearchAsync(BibSearchRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var q = db.Set<BibSnapshot>().AsNoTracking().Where(b => !b.Deleted);
        if (request.MfnFrom is { } from) q = q.Where(b => b.Mfn >= from);
        if (request.MfnTo is { } to) q = q.Where(b => b.Mfn <= to);
        if (request.BibTypeId is { } type) q = q.Where(b => b.BibTypeId == type);
        if (request.PublishYear?.Trim() is { Length: > 0 } year) q = q.Where(b => b.PublishYear != null && b.PublishYear.Contains(year));
        if (BibSnapshot.Fold(request.Title) is { Length: > 0 } title) q = q.Where(b => b.TitleKey.Contains(title));
        if (BibSnapshot.Fold(request.Author) is { Length: > 0 } author) q = q.Where(b => b.AuthorKey.Contains(author));
        if (BibSnapshot.Fold(request.Publisher) is { Length: > 0 } publisher) q = q.Where(b => b.PublisherKey.Contains(publisher));

        var (page, size) = (Math.Max(1, request.PageIndex), Math.Clamp(request.PageSize, 1, MaxSearchPageSize));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(b => b.Mfn).Skip((page - 1) * size).Take(size)
            .Select(b => new BibSearchRow(b.BibPublicId, b.Mfn, b.Title, b.Author, b.Publisher, b.PublishYear, b.Isbns, b.BibTypeName,
                db.Set<ReceiptLine>().Any(l => l.BibPublicId == b.BibPublicId), db.Set<OrderLine>().Any(l => l.BibPublicId == b.BibPublicId)))
            .ToListAsync(ct);
        return new CrudPage<BibSearchRow>(rows, total, page, size);
    }

    /// <summary>Loại tài liệu có trong bản sao (ô "Loại tài liệu" của tra trùng — không cần quyền danh mục catalog).</summary>
    public async Task<IReadOnlyList<BibTypeOption>> TypesAsync(CancellationToken ct)
    {
        var types = await db.Set<BibSnapshot>().AsNoTracking().Where(b => !b.Deleted && b.BibTypeId != null)
            .Select(b => new { Id = b.BibTypeId!.Value, b.BibTypeName }).Distinct().ToListAsync(ct);
        // Vài loại — gộp theo id và sắp ở bộ nhớ (tên lấy bản khác rỗng nếu catalog vừa đổi tên giữa chừng).
        return [.. types.GroupBy(t => t.Id).Select(g => new BibTypeOption(g.Key, g.Select(t => t.BibTypeName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? ""))
            .OrderBy(t => t.Name, StringComparer.CurrentCulture)];
    }

    /// <summary>Biểu ghi còn hiệu lực theo MFN — bản sao chưa có thì lấy từ catalog rồi lưu lại.</summary>
    public async Task<BibSnapshot> RequireAsync(long mfn, CancellationToken ct)
    {
        var snapshot = await db.Set<BibSnapshot>().FirstOrDefaultAsync(x => x.Mfn == mfn, ct);
        if (snapshot is null && await catalog.GetAsync(tenant.RequireTenantId(), mfn, ct) is { } state)
        {
            snapshot = await ApplyAsync(state, ct);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Consumer BibChanged vừa ghi cùng biểu ghi (trùng khoá) → dùng bản của consumer.
                db.Set<BibSnapshot>().Entry(snapshot).State = EntityState.Detached;
                snapshot = await db.Set<BibSnapshot>().FirstOrDefaultAsync(x => x.BibPublicId == state.BibPublicId, ct);
            }
        }
        return snapshot is { Deleted: false } ? snapshot : throw new NotFoundException("Biểu ghi", mfn);
    }

    public const int MaxLookup = 50;

    /// <summary>
    /// Tìm biểu ghi để chọn: từ khoá là số → đúng MFN (hỏi catalog nếu bản sao chưa có) và nhan đề có số đó; ngược lại theo nhan đề,
    /// tác giả, ISBN.
    /// </summary>
    public async Task<IReadOnlyList<BibOption>> LookupAsync(string? keyword, CancellationToken ct)
    {
        var term = keyword?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(term)) return [];
        var result = new List<BibOption>();
        if (long.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out var mfn))
        {
            try
            {
                var bib = await RequireAsync(mfn, ct);
                result.Add(Option(bib));
            }
            catch (NotFoundException)
            {
                // Không có MFN này — vẫn tìm theo nhan đề/ISBN có chuỗi số.
            }
        }
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        var found = await db.Set<BibSnapshot>().AsNoTracking()
            .Where(b => !b.Deleted && (b.Title.ToLower().Contains(term) || (b.Author != null && b.Author.ToLower().Contains(term))
                || (b.Isbns != null && b.Isbns.ToLower().Contains(term))))
            .OrderByDescending(b => b.Mfn).Take(MaxLookup).ToListAsync(ct);
#pragma warning restore CA1862, CA1304, CA1311
        result.AddRange(found.Where(b => result.All(r => r.BibPublicId != b.BibPublicId)).Select(Option));
        return result;
    }

    /// <summary>
    /// Dựng bản sao biểu ghi của đơn vị hiện tại từ catalog (đơn vị mới, hoặc acquisition triển khai sau khi catalog đã có dữ liệu) —
    /// đọc theo trang tới hết. Trả số biểu ghi đã ghi.
    /// </summary>
    public async Task<int> BackfillAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        long after = 0;
        var count = 0;
        while (true)
        {
            var page = await catalog.PageAsync(tenantId, after, ct);
            foreach (var bib in page.Items) await ApplyAsync(bib, ct);
            await db.SaveChangesAsync(ct);
            count += page.Items.Count;
            if (page.Next is not { } next) return count;
            after = next;
        }
    }

    private static BibOption Option(BibSnapshot b) => new(b.BibPublicId, b.Mfn, b.Title, b.Author, b.Publisher, b.PublishYear, b.Isbns);

    private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}
