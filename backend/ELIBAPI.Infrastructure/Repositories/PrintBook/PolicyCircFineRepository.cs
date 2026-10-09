using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class PolicyCircFineRepository : BaseRepository<PolicyCircFine, PolicyCircFineSearchRequest, PolicyCircFineRequest>
{
    public PolicyCircFineRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<PolicyCircFine> BuildQuery(PolicyCircFineSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.PolicyCircId.HasValue && r.PolicyCircId > 0) q = q.Where(x => x.PolicyCircId == r.PolicyCircId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PolicyCircFineRequest r, PolicyCircFine e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(PolicyCircFine e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(PolicyCircFine e, int status, long userId) { }
}
