using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Gamification;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

// Danh mục huy hiệu — tenant-scoped bình thường (không phải IncludeNull như BibType): mỗi tenant tự
// định nghĩa bộ huy hiệu riêng, dùng BuildQuery + ApplyTenantFilter mặc định của BaseRepository.
public class BadgeRepository : BaseRepository<Badge, BadgeSearchRequest, BadgeRequest>
{
    public BadgeRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Badge> BuildQuery(BadgeSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(BadgeRequest r, Badge e, long userId, bool isNew)
    {
        PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; e.Status ??= 2; }
    }

    protected override void SoftDelete(Badge e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Badge e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
