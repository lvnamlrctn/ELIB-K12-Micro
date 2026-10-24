using System.Globalization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Platform;
using Elib.Serials.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Serials.Application;

/// <summary>Trạng thái biểu ghi lấy thẳng từ catalog (/internal, service token) — khi bản sao chưa có, hoặc dựng bản sao lần đầu.</summary>
public interface ICatalogBibs
{
    Task<BibChanged?> GetAsync(long tenantId, long mfn, CancellationToken ct);

    Task<StatePage<BibChanged>> PageAsync(long tenantId, long after, CancellationToken ct);
}

/// <summary>Biểu ghi chọn được cho đăng ký đặt báo.</summary>
public sealed record BibOption(Guid BibPublicId, long Mfn, string Title, string? Author, string? Publisher, string? Issn);

/// <summary>Bản sao biểu ghi (BibSnapshot): ghi từ event <see cref="BibChanged"/>, thiếu thì hỏi catalog.</summary>
public sealed class BibSnapshots(ICrudDbContext db, ICatalogBibs catalog, ITenantContext tenant)
{
    public const int MaxLookup = 50;

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
        snapshot.TitleKey = Cut(BibSnapshot.Fold(e.Title), 1000) ?? "";
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

    public async Task<BibSnapshot> RequireAsync(Guid bibPublicId, CancellationToken ct) =>
        await db.Set<BibSnapshot>().FirstOrDefaultAsync(x => x.BibPublicId == bibPublicId && !x.Deleted, ct) ?? throw new NotFoundException("Biểu ghi", bibPublicId);

    /// <summary>Tìm biểu ghi để chọn: từ khoá là số → đúng MFN (hỏi catalog nếu chưa có bản sao); ngược lại theo nhan đề/tác giả (không dấu).</summary>
    public async Task<IReadOnlyList<BibOption>> LookupAsync(string? keyword, CancellationToken ct)
    {
        var term = keyword?.Trim();
        if (string.IsNullOrEmpty(term)) return [];
        var result = new List<BibOption>();
        if (long.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out var mfn))
        {
            try
            {
                result.Add(Option(await RequireAsync(mfn, ct)));
            }
            catch (NotFoundException)
            {
                // Không có MFN này — vẫn tìm theo nhan đề có chuỗi số.
            }
        }
        var key = BibSnapshot.Fold(term);
        if (key.Length > 0)
        {
            var found = await db.Set<BibSnapshot>().AsNoTracking()
                .Where(b => !b.Deleted && b.TitleKey.Contains(key)).OrderByDescending(b => b.Mfn).Take(MaxLookup).ToListAsync(ct);
            result.AddRange(found.Where(b => result.All(r => r.BibPublicId != b.BibPublicId)).Select(Option));
        }
        return result;
    }

    /// <summary>Dựng bản sao biểu ghi của đơn vị hiện tại từ catalog — đọc theo trang tới hết. Trả số biểu ghi đã ghi.</summary>
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

    private static BibOption Option(BibSnapshot b) => new(b.BibPublicId, b.Mfn, b.Title, b.Author, b.Publisher, b.Issn);

    private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}
