using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicEbookCollectionController : PublicBaseController
{
    private readonly IPublicEbookCollectionRepository _repo;

    public PublicEbookCollectionController(IPublicEbookCollectionRepository repo)
        => _repo = repo;

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicEbookCollectionSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        var collectionIds = paged.Items.Select(x => x.Id).ToList();
        var itemCounts = await _repo.GetCollectionItemCountsAsync(collectionIds);

        return Ok(ApiResponse<PagedResult<PublicEbookCollectionResponse>>.Ok(new PagedResult<PublicEbookCollectionResponse>
        {
            Items      = paged.Items.Select(x => ToResponse(x, itemCounts)).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicEbookCollectionSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        var collectionIds = items.Select(x => x.Id).ToList();
        var itemCounts = await _repo.GetCollectionItemCountsAsync(collectionIds);

        return Ok(ApiResponse<List<PublicEbookCollectionResponse>>.Ok(items.Select(x => ToResponse(x, itemCounts)).ToList()));
    }

    private static PublicEbookCollectionResponse ToResponse(EbookCollection x, Dictionary<long, int> itemCounts) => new()
    {
        Id            = x.Id,
        PublicId      = x.PublicId,
        Name          = x.Name,
        ParentId      = x.ParentId,
        Level         = x.Level,
        Status        = x.Status,
        SortOrder     = x.SortOrder,
        PortalId      = x.PortalId,
        Language      = x.Language,
        Link          = x.Link,
        Allowdownload = x.Allowdownload,
        Images        = x.Images,
        TotalItems    = itemCounts.GetValueOrDefault(x.Id, 0)
    };
}
