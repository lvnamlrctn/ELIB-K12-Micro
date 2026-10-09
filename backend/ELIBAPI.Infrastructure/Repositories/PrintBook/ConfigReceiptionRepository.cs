using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ConfigReceiptionRepository
    : BaseRepository<ConfigReceiption, ConfigReceiptionSearchRequest, ConfigReceiptionRequest>
{
    public ConfigReceiptionRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<ConfigReceiption> BuildQuery(ConfigReceiptionSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.UserId.HasValue) q = q.Where(x => x.UserId == r.UserId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ConfigReceiptionRequest r, ConfigReceiption e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew)
        {
            e.UserId       = userId;
            e.CreatedRowBy = userId;
            e.CreatedRowDate = DateTime.Now;
        }
    }

    protected override void SoftDelete(ConfigReceiption e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(ConfigReceiption e, int status, long userId) { }
}
