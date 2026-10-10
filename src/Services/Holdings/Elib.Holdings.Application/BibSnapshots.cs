using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Catalog;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Trạng thái biểu ghi lấy thẳng từ catalog (/internal, service token) — chỉ khi bản sao chưa có biểu ghi.</summary>
public interface ICatalogBibs
{
    Task<BibChanged?> GetAsync(long tenantId, long mfn, CancellationToken ct);
}

/// <summary>Bản sao biểu ghi (BibSnapshot): ghi từ event <see cref="BibChanged"/>, thiếu thì hỏi catalog.</summary>
public sealed class BibSnapshots(ICrudDbContext db, ICatalogBibs catalog, ITenantContext tenant)
{
    /// <summary>Upsert theo BibPublicId; event cũ hơn bản đang có bị bỏ qua. Chưa lưu — nơi gọi SaveChanges.</summary>
    public async Task<BibSnapshot> ApplyAsync(BibChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var snapshot = await db.Set<BibSnapshot>().FirstOrDefaultAsync(x => x.BibPublicId == e.BibPublicId, ct);
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
        snapshot.Ddc = Cut(e.Ddc, 50);
        snapshot.Status = e.Status;
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
            await db.SaveChangesAsync(ct);
        }
        return snapshot is { Deleted: false } ? snapshot : throw new NotFoundException("Biểu ghi", mfn);
    }

    private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}
