using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class PolicyCircDocGroupRepository : BaseRepository<PolicyCircDocGroup, PolicyCircDocGroupSearchRequest, PolicyCircDocGroupRequest>
{
    public PolicyCircDocGroupRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<PolicyCircDocGroup> BuildQuery(PolicyCircDocGroupSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.PolicyCircId.HasValue && r.PolicyCircId > 0) q = q.Where(x => x.PolicyCircId == r.PolicyCircId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PolicyCircDocGroupRequest r, PolicyCircDocGroup e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(PolicyCircDocGroup e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(PolicyCircDocGroup e, int status, long userId) { }
}
