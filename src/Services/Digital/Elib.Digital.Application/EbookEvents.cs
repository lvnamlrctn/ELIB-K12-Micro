using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Digital;
using Elib.Digital.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

/// <summary>
/// Dựng và phát <see cref="EbookChanged"/> (outbox, CÙNG transaction với thay đổi — gọi TRƯỚC SaveChanges). Phần chưa lưu (metadata mới, file vừa
/// thêm/xoá) truyền qua tham số vì lúc phát chưa có trong DB.
/// </summary>
public sealed class EbookStatePublisher(ICrudDbContext db, IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor)
{
    public const int MaxStatePage = 500;
    private const int MaxMetadataValues = 200;

    public async Task PublishAsync(EbookItem item, bool deleted, CancellationToken ct, IReadOnlyList<string>? metadata = null, bool? hasFile = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.PublicId == Guid.Empty) item.PublicId = Guid.CreateVersion7(); // tài liệu mới: interceptor chỉ gán khi lưu
        await publisher.Publish(await StateAsync(item, deleted, ct, metadata, hasFile), ct);
    }

    /// <summary>Trạng thái mọi tài liệu (kể cả đang ẩn) theo Id tăng dần, sau <paramref name="afterId"/> — search dựng chỉ mục lần đầu / dựng lại.</summary>
    public async Task<IReadOnlyList<EbookChanged>> StatesAsync(long afterId, int take, CancellationToken ct)
    {
        var items = await db.Set<EbookItem>().AsNoTracking().Where(x => x.Id > afterId).OrderBy(x => x.Id).Take(Math.Clamp(take, 1, MaxStatePage)).ToListAsync(ct);
        var events = new List<EbookChanged>(items.Count);
        foreach (var item in items) events.Add(await StateAsync(item, deleted: false, ct));
        return events;
    }

    private async Task<EbookChanged> StateAsync(EbookItem item, bool deleted, CancellationToken ct, IReadOnlyList<string>? metadata = null, bool? hasFile = null)
    {
        async Task<string?> NameAsync<T>(long? id) where T : TreeCatalogItem =>
            id is null ? null : await db.Set<T>().AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).FirstOrDefaultAsync(ct);

        metadata ??= item.Id == 0 ? [] : await db.Set<EbookMetadataValue>().AsNoTracking()
            .Where(v => v.EbookItemId == item.Id && db.Set<MetadataField>().Any(f => f.Id == v.FieldId && f.Status == IHasStatus.Active))
            .OrderBy(v => v.FieldId).ThenBy(v => v.SortOrder).Select(v => v.Value).Take(MaxMetadataValues).ToListAsync(ct);
        hasFile ??= item.Id != 0 && await db.Set<EbookFile>().AnyAsync(f => f.EbookItemId == item.Id, ct);
        var type = item.TypeId is { } typeId
            ? await db.Set<EbookDigType>().AsNoTracking().Where(t => t.Id == typeId).Select(t => t.DescriptionVn ?? t.Code).FirstOrDefaultAsync(ct) : null;

        return new EbookChanged
        {
            TenantId = tenant.RequireTenantId(),
            Actor = new EventActor(actor.Id, actor.Kind),
            EbookPublicId = item.PublicId,
            EbookId = item.Id,
            Title = item.Title,
            Author = item.Author,
            OtherTitle = item.OtherTitle,
            Publisher = item.Publisher,
            PublishYear = item.PublishDate,
            Keywords = item.Keyword,
            Language = item.Language,
            CollectionName = await NameAsync<EbookCollection>(item.CollectionId),
            SubjectName = await NameAsync<EbookSubject>(item.SubjectId),
            TopicName = await NameAsync<EbookTopic>(item.TopicId),
            TypeName = type,
            Metadata = metadata.Take(MaxMetadataValues).ToList(),
            CoverUrl = item.CoverUrl,
            Free = item.Free,
            HasFile = hasFile.Value,
            LinkedBibPublicId = item.LinkedBibPublicId,
            // Bản sao đơn vị đã ẩn thì search coi như không xuất bản (nhận Version mới để bỏ bản đang hiện).
            Status = item.HiddenByTenant ? IHasStatus.Inactive : item.Status,
            Deleted = deleted,
            Version = item.Version + (deleted ? 1 : 0),
        };
    }
}
