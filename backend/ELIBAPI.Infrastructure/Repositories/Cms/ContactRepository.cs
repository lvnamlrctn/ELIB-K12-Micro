using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ContactRepository : BaseRepository<Contact, ContactSearchRequest, ContactRequest>
{
    public ContactRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Contact> BuildQuery(ContactSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ContactGroupId.HasValue) q = q.Where(x => x.ContactGroupId == r.ContactGroupId);
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ContactRequest r, Contact e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Email = r.Email; e.Content = r.Content; e.Mobile = r.Mobile;
        e.Address = r.Address; e.CreatedDate = r.CreatedDate; e.ContactGroupId = r.ContactGroupId;
        e.Status = r.Status; e.PortalId = r.PortalId; e.Language = r.Language; e.Title = r.Title;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Contact e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Contact e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
