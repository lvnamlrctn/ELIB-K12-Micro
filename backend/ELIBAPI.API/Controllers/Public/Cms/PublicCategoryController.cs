using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicCategoryController : PublicBaseController
{
    private readonly IPublicGenericRepository<Category, PublicCategorySearchRequest> _repo;
    private readonly IStringLocalizer<SharedResource> _loc;

    public PublicCategoryController(IPublicGenericRepository<Category, PublicCategorySearchRequest> repo, IStringLocalizer<SharedResource> loc)
    {
        _repo = repo;
        _loc  = loc;
    }

    [HttpGet("{CategoryId}")]
    public async Task<IActionResult> GetCategoryDetail(Guid CategoryId, [FromQuery] Guid? tenantId = null)
    {
        var item = await _repo.GetByPublicIdAsync(CategoryId, tenantId ?? default);
        if (item == null)
            return NotFound(ApiResponse<PublicCategoryResponse>.Fail(_loc["NotFound"]));

        return Ok(ApiResponse<PublicCategoryResponse>.Ok(ToResponse(item)));
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicCategorySearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicCategoryResponse>>.Ok(new PagedResult<PublicCategoryResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicCategorySearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicCategoryResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private static PublicCategoryResponse ToResponse(Category x) => new()
    {
        Id              = x.Id,
        Name            = x.Name,
        ParentId        = x.ParentId,
        Level           = x.Level,
        Status          = x.Status,
        Order           = x.Order,
        PortalId        = x.PortalId,
        IsLogin         = x.IsLogin,
        Description     = x.Description,
        Keyword         = x.Keyword,
        PageTitle       = x.PageTitle,
        MetaDescription = x.MetaDescription,
        Language        = x.Language,
        Link            = x.Link,
    };
}
