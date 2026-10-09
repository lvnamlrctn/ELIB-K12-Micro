using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class MetaDataValueRepository : BaseRepository<MetaDataValue, MetaDataValueSearchRequest, MetaDataValueRequest>
{
    public MetaDataValueRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<MetaDataValue> BuildQuery(MetaDataValueSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Value!.Contains(r.Keyword));
        if (r.ItemId.HasValue)                 q = q.Where(x => x.ItemId          == r.ItemId);
        if (r.MetaDataFieldId.HasValue)        q = q.Where(x => x.MetaDataFieldId == r.MetaDataFieldId);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(MetaDataValueRequest r, MetaDataValue e, long userId, bool isNew)
    {
        e.MetaDataFieldId = r.MetaDataFieldId; e.Value = r.Value; e.Language = r.Language;
        e.ItemId = r.ItemId; e.Value_UnSign = r.Value_UnSign; e.SortOrder = r.SortOrder;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MetaDataValue e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MetaDataValue e, int status, long userId) { }
}
