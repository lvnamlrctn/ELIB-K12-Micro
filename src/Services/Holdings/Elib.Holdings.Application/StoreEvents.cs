using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Holdings;
using Elib.Holdings.Domain;
using MassTransit;

namespace Elib.Holdings.Application;

/// <summary>
/// Phát <see cref="StoreChanged"/> cho mọi thay đổi kho (outbox — gọi trước SaveChanges để event cùng transaction với dữ
/// liệu, đúng khuôn ItemEvents). Space giữ bản sao để gợi ý kho khi đặt giá sách trên sơ đồ tầng.
/// </summary>
public sealed class StoreEvents(IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor)
{
    public Task PublishAsync(Store store, bool deleted, CancellationToken ct)
    {
        if (store.PublicId == Guid.Empty) store.PublicId = Guid.CreateVersion7(); // bản mới: interceptor chỉ gán khi còn trống, cần có trước khi phát event
        return publisher.Publish(new StoreChanged
        {
            TenantId = tenant.RequireTenantId(),
            Actor = new EventActor(actor.Id, actor.Kind),
            StorePublicId = store.PublicId,
            Code = store.Code,
            Name = store.Name,
            Deleted = deleted,
        }, ct);
    }
}
