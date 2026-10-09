using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class PhotoAlbumRepository : BaseRepository<PhotoAlbum, PhotoAlbumSearchRequest, PhotoAlbumRequest>, IPhotoAlbumRepository
{
    public PhotoAlbumRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<PhotoAlbum> BuildQuery(PhotoAlbumSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (!string.IsNullOrEmpty(r.Types)) q = q.Where(x => x.Types == r.Types);
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PhotoAlbumRequest r, PhotoAlbum e, long userId, bool isNew)
    {
        e.PortalId = r.PortalId; e.Name = r.Name; e.Description = r.Description;
        e.Image = r.Image; e.Code = r.Code; e.Language = r.Language; e.Status = r.Status;
        e.SortOrder = r.SortOrder; e.Types = r.Types; e.Postions = r.Postions;
        e.IsSpecial = r.IsSpecial == true ? 2 : 1;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(PhotoAlbum e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(PhotoAlbum e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    public async Task ChangeIsSpecialAsync(ChangeIsSpecialRequest request)
    {
        var entity = await GetByPublicIdAsync(request.PublicId) ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        entity.IsSpecial = request.IsSpecial ? 2 : 1;
        entity.UpdateRowBy = userId; entity.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
        try
        {
            var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
            _context.UserLogs.Add(new UserLog
            {
                UserId = userId, ActionType = "ChangeIsSpecial", Object = "PhotoAlbum",
                Action = $"ChangeIsSpecial PhotoAlbum #{entity.Id}", Submited = DateTime.Now,
                Ip = ip, Application = "ELIBAPI", PortalId = entity.PortalId,
                TenantId = GetCurrentTenantId()
            });
            await _context.SaveChangesAsync();
        }
        catch { }
    }
}
