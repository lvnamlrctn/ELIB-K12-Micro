using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicZ3950ConfigController : PublicBaseController
{
    private readonly IPublicGenericRepository<Z3950Config, PublicZ3950ConfigSearchRequest> _repo;
    private readonly IStringLocalizer<SharedResource> _loc;

    public PublicZ3950ConfigController(IPublicGenericRepository<Z3950Config, PublicZ3950ConfigSearchRequest> repo, IStringLocalizer<SharedResource> loc)
    {
        _repo = repo;
        _loc  = loc;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicZ3950ConfigSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicZ3950ConfigResponse>>.Ok(new PagedResult<PublicZ3950ConfigResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicZ3950ConfigSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicZ3950ConfigResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private static PublicZ3950ConfigResponse ToResponse(Z3950Config x) => new()
    {
        PublicId = x.PublicId,
        Name     = x.Name,
        Systax   = x.Systax,
    };
}
