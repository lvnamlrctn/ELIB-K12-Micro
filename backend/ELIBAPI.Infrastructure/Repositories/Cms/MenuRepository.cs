using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class MenuRepository
    : BaseRepository<Menu, MenuSearchRequest, MenuRequest>,
      IMenuRepository
{
    public MenuRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Menu> BuildQuery(MenuSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.MenuType.HasValue)
        {
            var menuTypeId = _context.Set<MenuType>()
                .Where(mt => mt.PublicId == r.MenuType.Value)
                .Select(mt => (long?)mt.Id)
                .FirstOrDefault();
            if (menuTypeId.HasValue)
                q = q.Where(x => x.MenuType == menuTypeId);
        }
        if (r.ParentId.HasValue) q = q.Where(x => x.ParentId == r.ParentId);
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(MenuRequest r, Menu e, long userId, bool isNew)
    {
        e.Name = r.Name; e.MenuType = r.MenuType; e.Link = r.Link;
        e.FriendUrl = r.FriendUrl; e.SortOrder = r.SortOrder; e.Status = r.Status;
        e.OpenType = r.OpenType; e.PortalId = r.PortalId; e.Language = r.Language;
        e.ParentId = r.ParentId; e.LinkType = r.LinkType; e.SubId = r.SubId;
        e.IsLogIn = r.IsLogIn; e.Icon = r.Icon;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Menu e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Menu e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    public async Task<List<MenuTreeResponse>> GetTreeAsync(MenuSearchRequest request)
    {
        var treeRequest = new MenuSearchRequest
        {
            Keyword  = request.Keyword,
            PortalId = request.PortalId,
            Language = request.Language,
            Status   = request.Status,
            MenuType = request.MenuType,
            TenantId = request.TenantId
        };
        var all = await SearchAllAsync(treeRequest);
        var childCount = all.GroupBy(m => m.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Count());
        var flat = all.Select(m => ToTreeResponse(m, childCount)).ToList();
        return BuildTree(flat, null);
    }

    private static MenuTreeResponse ToTreeResponse(Menu m, Dictionary<long, int> childCount)
    {
        var count = childCount.GetValueOrDefault(m.Id, 0);
        return new MenuTreeResponse
        {
            Id = m.Id, Name = m.Name, ParentId = m.ParentId, MenuType = m.MenuType,
            Link = m.Link, FriendUrl = m.FriendUrl, SortOrder = m.SortOrder,
            Status = m.Status, OpenType = m.OpenType, PortalId = m.PortalId,
            Language = m.Language, LinkType = m.LinkType, SubId = m.SubId,
            IsLogIn = m.IsLogIn, Icon = m.Icon, PublicId = m.PublicId,
            TenantId = m.TenantId, TenantName = m.TenantName,
            HasChildren = count > 0, ChildCount = count
        };
    }

    private List<MenuTreeResponse> BuildTree(List<MenuTreeResponse> flat, long? parentId)
        => flat
            .Where(m => parentId == null ? (m.ParentId == null || m.ParentId == 0) : m.ParentId == parentId)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .Select(m => new MenuTreeResponse
            {
                Id = m.Id, Name = m.Name, ParentId = m.ParentId, MenuType = m.MenuType,
                Link = m.Link, FriendUrl = m.FriendUrl, SortOrder = m.SortOrder,
                Status = m.Status, OpenType = m.OpenType, PortalId = m.PortalId,
                Language = m.Language, LinkType = m.LinkType, SubId = m.SubId,
                IsLogIn = m.IsLogIn, Icon = m.Icon, PublicId = m.PublicId,
                TenantId = m.TenantId, TenantName = m.TenantName,
                HasChildren = m.HasChildren, ChildCount = m.ChildCount,
                Children = BuildTree(flat, m.Id)
            })
            .ToList();
}
