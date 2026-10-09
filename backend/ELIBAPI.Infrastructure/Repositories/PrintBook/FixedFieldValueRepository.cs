using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class FixedFieldValueRepository : BaseRepository<FixedFieldValue, FixedFieldValueSearchRequest, FixedFieldValueRequest>
{
    public FixedFieldValueRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<FixedFieldValue> BuildQuery(FixedFieldValueSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Field!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(FixedFieldValueRequest r, FixedFieldValue e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(FixedFieldValue e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(FixedFieldValue e, int status, long userId) { }
}
