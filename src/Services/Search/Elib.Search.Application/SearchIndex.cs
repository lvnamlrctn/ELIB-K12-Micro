using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Platform;
using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Search.Application;

/// <summary>Trạng thái hiện tại lấy từ service gốc qua /internal (service token) theo trang — dựng chỉ mục lần đầu / dựng lại.</summary>
public interface ISearchSources
{
    Task<StatePage<BibChanged>> BibsAsync(long tenantId, long after, CancellationToken ct);
    Task<StatePage<ItemChanged>> ItemsAsync(long tenantId, long after, CancellationToken ct);
    Task<StatePage<LoanChanged>> OpenLoansAsync(long tenantId, long after, CancellationToken ct);
}

/// <summary>
/// Ghi chỉ mục từ event (docs 04 §6: search sở hữu read model, dựng lại được hoàn toàn từ service nguồn). Mỗi bản ghi chỉ nhận
/// Version lớn hơn bản đang có. Chưa lưu — nơi gọi SaveChanges.
/// </summary>
public sealed class SearchIndex(ISearchDb db, TimeProvider clock)
{
    public async Task ApplyAsync(BibChanged e, CancellationToken ct, Guid? syncRun = null)
    {
        ArgumentNullException.ThrowIfNull(e);
        var bib = await db.Set<SearchBib>().FirstOrDefaultAsync(x => x.BibPublicId == e.BibPublicId, ct);
        if (bib is null) db.Set<SearchBib>().Add(bib = new SearchBib { BibPublicId = e.BibPublicId, Version = -1 });
        if (syncRun is not null) bib.SyncRun = syncRun;
        if (e.Version <= bib.Version) return;

        bib.Mfn = e.Mfn;
        bib.BibTypeId = e.BibTypeId;
        bib.MaterialType = TextFold.Cut(e.BibTypeName, 250);
        bib.Title = TextFold.Cut(e.Title, 1000) ?? "";
        bib.Author = TextFold.Cut(e.Author, 500);
        bib.OtherAuthors = e.OtherAuthors.Count > 0 ? TextFold.Cut(string.Join("; ", e.OtherAuthors), 2000) : null;
        bib.Publisher = TextFold.Cut(e.Publisher, 500);
        bib.PublishPlace = TextFold.Cut(e.PublishPlace, 250);
        bib.PublishYear = TextFold.Cut(e.PublishYear, 4);
        bib.Year = SearchBib.ParseYear(e.PublishYear);
        bib.Isbns = e.Isbns.Count > 0 ? TextFold.Cut(string.Join("; ", e.Isbns), 500) : null;
        bib.Ddc = TextFold.Cut(e.Ddc, 50);
        bib.Cutter = TextFold.Cut(e.Cutter, 50);
        bib.Keywords = TextFold.Cut(e.Keywords, 2000);
        bib.Language = TextFold.Cut(e.Language, 20);
        bib.Summary = TextFold.Cut(e.Summary, 4000);
        bib.Edition = TextFold.Cut(e.Edition, 250);
        bib.PhysicalDescription = TextFold.Cut(e.PhysicalDescription, 250);
        bib.Series = TextFold.Cut(e.Series, 500);
        bib.Status = e.Status;
        bib.Deleted = e.Deleted;
        bib.Version = e.Version;

        bib.TitleFold = TextFold.Cut(TextFold.Fold(bib.Title), 1000)!;
        bib.AuthorFold = TextFold.Cut(TextFold.Fold($"{bib.Author} {bib.OtherAuthors}"), 2000)!;
        bib.PublisherFold = TextFold.Cut(TextFold.Fold(bib.Publisher), 500)!;
        bib.KeywordFold = TextFold.Cut(TextFold.Fold($"{bib.Keywords} {bib.Series}"), 2000)!;
        bib.IsbnKey = string.Join(' ', e.Isbns);
        bib.SearchText = TextFold.Cut(TextFold.Fold(string.Join(' ',
            bib.Title, bib.Author, bib.OtherAuthors, bib.Publisher, bib.Keywords, bib.Series, bib.Summary, bib.Ddc, bib.IsbnKey, bib.MaterialType)), 8000)!;
        bib.IndexedAt = clock.GetUtcNow();
    }

    public async Task ApplyAsync(ItemChanged e, CancellationToken ct, Guid? syncRun = null)
    {
        ArgumentNullException.ThrowIfNull(e);
        var item = await db.Set<SearchItem>().FirstOrDefaultAsync(x => x.ItemPublicId == e.ItemPublicId, ct);
        if (item is null) db.Set<SearchItem>().Add(item = new SearchItem { ItemPublicId = e.ItemPublicId, Version = -1 });
        if (syncRun is not null) item.SyncRun = syncRun;
        if (e.Version <= item.Version) return;
        item.BibPublicId = e.BibPublicId;
        item.Mfn = e.Mfn;
        item.Barcode = TextFold.Cut(e.Barcode.Trim(), 50)!;
        item.BarcodeKey = SearchItem.Key(item.Barcode);
        item.StoreId = e.StoreId;
        item.StoreName = TextFold.Cut(e.StoreName, 250);
        item.Status = TextFold.Cut(e.Status, 1) ?? "";
        item.Deleted = e.Deleted;
        item.Version = e.Version;
    }

    /// <summary>Lượt mượn — bản sách chưa có trong chỉ mục (ItemChanged tới sau) thì bỏ qua: lần dựng lại sẽ lấy lượt đang mở.</summary>
    public async Task ApplyAsync(LoanChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var item = await db.Set<SearchItem>().FirstOrDefaultAsync(x => x.ItemPublicId == e.ItemPublicId, ct);
        item?.ApplyLoan(e.LoanPublicId, e.Version, e.LoanedAt, e.DueAt, e.ReturnedAt is not null);
    }
}

public sealed record SearchIndexStatus(string Status, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, int Bibs, int Items, int Loans, string? Error,
    int IndexedBibs, int VisibleBibs, int IndexedItems);

/// <summary>
/// Dựng lại chỉ mục của đơn vị hiện tại từ catalog, holdings, circulation (/internal theo trang): ghi trạng thái hiện tại của mọi biểu
/// ghi/bản sách, đánh dấu xoá cái không còn ở service gốc, đặt lại "đang mượn" theo các lượt đang mở. Chạy khi service mới triển khai
/// (seeder lúc dựng bản sao đơn vị) và khi quản trị bấm "Dựng lại chỉ mục".
/// </summary>
public sealed class IndexRebuilder(ISearchDb db, SearchIndex index, ISearchSources sources, ITenantContext tenant, TimeProvider clock)
{
    public async Task<SearchIndexStatus> RebuildAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var state = await db.Set<SearchSyncState>().FirstOrDefaultAsync(ct);
        if (state is null) db.Set<SearchSyncState>().Add(state = new SearchSyncState());
        state.Status = SearchSyncState.Running;
        state.StartedAt = clock.GetUtcNow();
        state.Error = null;
        await db.SaveChangesAsync(ct);

        var run = Guid.CreateVersion7();
        try
        {
            var (bibs, items, loans) = (0, 0, 0);
            for (long? after = 0; after is not null;)
            {
                var page = await sources.BibsAsync(tenantId, after.Value, ct);
                foreach (var e in page.Items) await index.ApplyAsync(e, ct, run);
                await FlushAsync(ct);
                bibs += page.Items.Count;
                after = page.Next;
            }
            for (long? after = 0; after is not null;)
            {
                var page = await sources.ItemsAsync(tenantId, after.Value, ct);
                foreach (var e in page.Items) await index.ApplyAsync(e, ct, run);
                await FlushAsync(ct);
                items += page.Items.Count;
                after = page.Next;
            }
            // Không còn ở service gốc (bị xoá khi search chưa chạy) → đánh dấu xoá.
            await db.Set<SearchBib>().Where(b => b.SyncRun != run && !b.Deleted)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Deleted, true), ct);
            await db.Set<SearchItem>().Where(i => i.SyncRun != run && !i.Deleted)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Deleted, true), ct);

            var open = new HashSet<Guid>();
            for (long? after = 0; after is not null;)
            {
                StatePage<LoanChanged> page;
                try
                {
                    page = await sources.OpenLoansAsync(tenantId, after.Value, ct);
                }
                catch (HttpRequestException) when (after == 0)
                {
                    break; // đơn vị/hệ chưa có circulation — bỏ phần đang mượn
                }
                foreach (var e in page.Items)
                {
                    open.Add(e.ItemPublicId);
                    await index.ApplyAsync(e, ct);
                }
                await FlushAsync(ct);
                loans += page.Items.Count;
                after = page.Next;
            }
            var stale = await db.Set<SearchItem>().Where(i => i.OnLoan).Select(i => i.ItemPublicId).ToListAsync(ct);
            var returned = stale.Where(id => !open.Contains(id)).ToList();
            if (returned.Count > 0)
                await db.Set<SearchItem>().Where(i => returned.Contains(i.ItemPublicId)).ExecuteUpdateAsync(s => s.SetProperty(i => i.OnLoan, false), ct);

            state = await db.Set<SearchSyncState>().FirstAsync(ct);
            (state.Status, state.FinishedAt, state.Bibs, state.Items, state.Loans) = (SearchSyncState.Done, clock.GetUtcNow(), bibs, items, loans);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            state = await db.Set<SearchSyncState>().FirstAsync(CancellationToken.None);
            (state.Status, state.FinishedAt, state.Error) = (SearchSyncState.Failed, clock.GetUtcNow(), TextFold.Cut(ex.Message, 1000));
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        return await StatusAsync(ct);
    }

    public async Task<SearchIndexStatus> StatusAsync(CancellationToken ct)
    {
        var state = await db.Set<SearchSyncState>().AsNoTracking().FirstOrDefaultAsync(ct) ?? new SearchSyncState { Status = "None" };
        return new SearchIndexStatus(state.Status, state.StartedAt, state.FinishedAt, state.Bibs, state.Items, state.Loans, state.Error,
            await db.Set<SearchBib>().CountAsync(b => !b.Deleted, ct),
            await db.Set<SearchBib>().CountAsync(b => !b.Deleted && b.Status == SearchBib.OpacVisible, ct),
            await db.Set<SearchItem>().CountAsync(i => !i.Deleted, ct));
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear(); // chỉ mục lớn: không giữ hàng nghìn entity trong change tracker
    }
}
