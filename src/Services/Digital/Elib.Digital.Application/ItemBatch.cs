using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

/// <summary>
/// Chọn tài liệu để thao tác hàng loạt: danh sách mã (các dòng đã tích) HOẶC toàn bộ kết quả của một bộ lọc ("chọn tất cả kết quả lọc").
/// </summary>
public sealed record ItemSelection(IReadOnlyList<Guid>? PublicIds = null, EbookItemSearch? Search = null);

public sealed record ItemSelectionPreview(int Count);

public sealed record SetCoverRequest(string? CoverUrl);

public sealed record ItemBulkMoveRequest(ItemSelection Selection, long CollectionId);

public sealed record ItemBulkStatusRequest(ItemSelection Selection, int Status);

public sealed record ItemSkipped(Guid PublicId, string Title, string Code, string Reason);

public sealed record ItemBulkResult(int Updated, IReadOnlyList<ItemSkipped> Skipped);

/// <summary>Chuyển bộ sưu tập / đổi trạng thái / xoá nhiều tài liệu (LRC: BulkMoveCollection, xoá nhiều trên màn Tài liệu số).</summary>
public sealed partial class EbookItemResource
{
    /// <summary>Trần số tài liệu một lần — mỗi tài liệu phát một sự kiện trạng thái cho search nên không để một lệnh quét cả kho.</summary>
    public const int MaxSelection = 2000;

    /// <summary>Đặt ảnh bìa của một tài liệu (ảnh đã tải lên media công khai của đơn vị).</summary>
    public async Task<EbookItemDto> SetCoverAsync(Guid publicId, string? coverUrl, CancellationToken ct)
    {
        var item = await LoadAsync(publicId, ct);
        item.SetCover(coverUrl, _options.MediaPublicPrefix, tenant.RequireTenantId(), clock.GetUtcNow());
        await OnSavingAsync(item, CrudChange.Updated, ct);
        await AuditAsync(item, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<ItemSelectionPreview> PreviewSelectionAsync(ItemSelection selection, CancellationToken ct) =>
        new((await ResolveIdsAsync(selection, ct)).Count);

    public async Task<ItemBulkResult> BulkMoveAsync(ItemBulkMoveRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await Db.Set<EbookCollection>().AnyAsync(x => x.Id == request.CollectionId, ct))
            throw new BusinessRuleException("COLLECTION_NOT_FOUND", "Bộ sưu tập đích không còn.");
        var now = clock.GetUtcNow();
        return await ApplyAsync(request.Selection, CrudChange.Updated, (item, _) =>
        {
            if (item.CollectionId != request.CollectionId) item.MoveToCollection(request.CollectionId, now);
            return Task.FromResult<ItemSkipped?>(null);
        }, ct);
    }

    public async Task<ItemBulkResult> BulkStatusAsync(ItemBulkStatusRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = StatusRules.Validate(request.Status);
        return await ApplyAsync(request.Selection, CrudChange.StatusChanged, (item, _) =>
        {
            if (item.Status != status) item.ChangeStatus(status);
            return Task.FromResult<ItemSkipped?>(null);
        }, ct);
    }

    /// <summary>Xoá nhiều: tài liệu còn file đính kèm bị bỏ qua và nêu lý do từng tài liệu (khác LRC — LRC xoá luôn, để file mồ côi).</summary>
    public async Task<ItemBulkResult> BulkDeleteAsync(ItemSelection selection, CancellationToken ct) =>
        await ApplyAsync(selection, CrudChange.Deleted, async (item, token) =>
        {
            try
            {
                await EnsureDeletableAsync(item, token);
            }
            catch (ConflictException e)
            {
                return new ItemSkipped(item.PublicId, item.Title, e.Code, e.Message);
            }
            Set.Remove(item); // interceptor chuyển thành xoá mềm
            return null;
        }, ct);

    private async Task<ItemBulkResult> ApplyAsync(ItemSelection selection, CrudChange change, Func<EbookItem, CancellationToken, Task<ItemSkipped?>> apply, CancellationToken ct)
    {
        var ids = await ResolveIdsAsync(selection, ct);
        var skipped = new List<ItemSkipped>();
        var updated = 0;
        var strategy = Db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async token =>
        {
            skipped.Clear();
            updated = 0;
            await using var transaction = await Db.Database.BeginTransactionAsync(token);
            foreach (var chunk in ids.Chunk(200))
            {
                foreach (var item in await Set.Where(x => chunk.Contains(x.PublicId)).ToListAsync(token))
                {
                    if (await apply(item, token) is { } skip)
                    {
                        skipped.Add(skip);
                        continue;
                    }
                    await OnSavingAsync(item, change, token);
                    await AuditAsync(item, change, token);
                    updated++;
                }
            }
            await Db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }, ct);
        return new ItemBulkResult(updated, skipped);
    }

    /// <summary>Mã các tài liệu được chọn (đã loại tài liệu xoá mềm nhờ bộ lọc toàn cục), tối đa <see cref="MaxSelection"/>.</summary>
    private async Task<List<Guid>> ResolveIdsAsync(ItemSelection? selection, CancellationToken ct)
    {
        if (selection is null || (selection.PublicIds is null) == (selection.Search is null))
            throw new BusinessRuleException("SELECTION_INVALID", "Chọn các dòng hoặc dùng bộ lọc hiện tại (một trong hai).");
        if (selection.PublicIds is { Count: > MaxSelection })
            throw new BusinessRuleException("SELECTION_TOO_MANY", $"Chỉ thao tác tối đa {MaxSelection} tài liệu một lần — hãy thu hẹp bộ lọc.");
        var ids = selection.PublicIds is { } given
            ? await Set.AsNoTracking().Where(x => given.Contains(x.PublicId)).Select(x => x.PublicId).Take(MaxSelection + 1).ToListAsync(ct)
            : await Query(selection.Search!).Select(x => x.PublicId).Take(MaxSelection + 1).ToListAsync(ct);
        if (ids.Count > MaxSelection)
            throw new BusinessRuleException("SELECTION_TOO_MANY", $"Chỉ thao tác tối đa {MaxSelection} tài liệu một lần — hãy thu hẹp bộ lọc.");
        if (ids.Count == 0) throw new BusinessRuleException("SELECTION_EMPTY", "Chưa có tài liệu nào được chọn.");
        return ids;
    }
}
