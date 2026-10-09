using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ConfigAacr2Repository : BaseRepository<ConfigAacr2, ConfigAacr2SearchRequest, ConfigAacr2Request>
{
    public ConfigAacr2Repository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<ConfigAacr2> BuildQuery(ConfigAacr2SearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ConfigAacr2Request r, ConfigAacr2 e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(ConfigAacr2 e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(ConfigAacr2 e, int status, long userId) { }
}
