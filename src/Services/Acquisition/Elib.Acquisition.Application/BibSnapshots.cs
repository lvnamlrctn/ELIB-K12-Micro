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
        if (e.Version <= snapshot.Version) return snapshot;

        snapshot.Mfn = e.Mfn;
        snapshot.Title = Cut(e.Title, 1000) ?? "";
        snapshot.Author = Cut(e.Author, 500);
        snapshot.Publisher = Cut(e.Publisher, 500);
        snapshot.PublishYear = Cut(e.PublishYear, 4);
        snapshot.Isbns = e.Isbns.Count > 0 ? Cut(string.Join(", ", e.Isbns), 1000) : null;
        snapshot.CatalogedAt = e.CatalogedAt;
        snapshot.Deleted = e.Deleted;
        snapshot.Version = e.Version;
        return snapshot;
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
