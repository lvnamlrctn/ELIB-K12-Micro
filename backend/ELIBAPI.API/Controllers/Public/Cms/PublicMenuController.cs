using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicMenuController : PublicBaseController
{
    private readonly IPublicMenuRepository _repo;

    public PublicMenuController(IPublicMenuRepository repo)
        => _repo = repo;

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicMenuSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicMenuResponse>>.Ok(new PagedResult<PublicMenuResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicMenuSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicMenuResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    [HttpGet("BuildCmsMenu")]
    public async Task<IActionResult> BuildCmsMenu([FromQuery] PublicCmsMenuRequest r)
    {
        var flat     = await _repo.BuildMenusync(r.DepartmentCode);
        var byParent = flat.GroupBy(x => x.ParentId ?? 0).ToDictionary(g => g.Key, g => g.ToList());

        List<PublicCmsMenuNode> BuildNodes(long parentId) =>
            byParent.TryGetValue(parentId, out var children)
                ? children.Select(m => new PublicCmsMenuNode
                  {
                      PublicId = m.PublicId,
                      Label    = m.Name,
                      Url      = m.Link,
                      Children = BuildNodes(m.Id) is { Count: > 0 } ch ? ch : null
                  }).ToList()
                : [];

        return Ok(ApiResponse<List<PublicCmsMenuNode>>.Ok(BuildNodes(0)));
    }

    private static PublicMenuResponse ToResponse(Menu x) => new()
    {
        Id        = x.Id,
        Name      = x.Name,
        MenuType  = x.MenuType,
        Link      = x.Link,
        FriendUrl = x.FriendUrl,
        SortOrder = x.SortOrder,
        Status    = x.Status,
        OpenType  = x.OpenType,
        PortalId  = x.PortalId,
        Language  = x.Language,
        ParentId  = x.ParentId,
        LinkType  = x.LinkType,
        SubId     = x.SubId,
        IsLogIn   = x.IsLogIn,
        Icon      = x.Icon,
    };
}
