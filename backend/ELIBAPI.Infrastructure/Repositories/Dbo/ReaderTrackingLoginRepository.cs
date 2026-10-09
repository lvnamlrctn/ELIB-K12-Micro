using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ReaderTrackingLoginRepository : BaseRepository<ReaderTrackingLogin, ReaderTrackingLoginSearchRequest, ReaderTrackingLoginRequest>
{
    public ReaderTrackingLoginRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<ReaderTrackingLogin> BuildQuery(ReaderTrackingLoginSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.ReaderId.HasValue) q = q.Where(x => x.ReaderId == r.ReaderId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ReaderTrackingLoginRequest r, ReaderTrackingLogin e, long userId, bool isNew)
    {
        e.ReaderId = r.ReaderId; e.Cardnumber = r.Cardnumber; e.LoginTime = r.LoginTime;
        e.LogOutTime = r.LogOutTime; e.Ip = r.Ip; e.SessionId = r.SessionId;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(ReaderTrackingLogin e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(ReaderTrackingLogin e, int status, long userId) { }
}
