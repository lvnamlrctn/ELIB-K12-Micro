using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class TaiLieuRepository : BaseRepository<TaiLieu, TaiLieuSearchRequest, TaiLieuRequest>
{
    public TaiLieuRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<TaiLieu> BuildQuery(TaiLieuSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Title!.Contains(r.Keyword) || x.Author!.Contains(r.Keyword));
        if (r.MonHocId.HasValue)              q = q.Where(x => x.MonHocId == r.MonHocId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(TaiLieuRequest r, TaiLieu e, long userId, bool isNew)
    {
        e.BibId = r.BibId; e.Title = r.Title; e.Author = r.Author; e.Publisher = r.Publisher;
        e.Url = r.Url; e.PublishDate = r.PublishDate; e.LoaiTaiLieu = r.LoaiTaiLieu;
        e.LanXuatBan = r.LanXuatBan; e.Note = r.Note; e.EBookId = r.EBookId; e.MonHocId = r.MonHocId;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(TaiLieu e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(TaiLieu e, int status, long userId) { }
}
