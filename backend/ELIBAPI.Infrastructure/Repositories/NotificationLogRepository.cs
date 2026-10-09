using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

// Chỉ đọc — insert duy nhất từ NotificationDispatcher, không có UI Add/Edit/Delete (mẫu
// EbookItemReservationController: controller không override Add/Update/Delete/ChangeStatus).
public class NotificationLogRepository : BaseRepository<NotificationLog, NotificationLogSearchRequest, NotificationLogRequest>
{
    public NotificationLogRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<NotificationLog> BuildQuery(NotificationLogSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Channel))   q = q.Where(x => x.Channel == r.Channel);
        if (!string.IsNullOrEmpty(r.EventCode)) q = q.Where(x => x.EventCode == r.EventCode);
        if (r.Success.HasValue)                 q = q.Where(x => x.Success == r.Success.Value);
        if (r.DateFrom.HasValue)                q = q.Where(x => x.SentAt >= r.DateFrom.Value);
        if (r.DateTo.HasValue)                  q = q.Where(x => x.SentAt <= r.DateTo.Value);
        if (!string.IsNullOrEmpty(r.Keyword))   q = q.Where(x => x.Recipient!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.SentAt);
    }

    protected override void MapRequestToEntity(NotificationLogRequest r, NotificationLog e, long userId, bool isNew) { }
    protected override void SoftDelete(NotificationLog e, long userId) { e.IsDelete = 2; }
    protected override void SetStatus(NotificationLog e, int status, long userId) { }
}
