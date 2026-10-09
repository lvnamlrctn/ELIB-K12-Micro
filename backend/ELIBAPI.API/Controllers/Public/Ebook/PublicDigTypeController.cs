using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

/// <summary>Danh mục "loại tài liệu số" (DigType) công khai -- dùng đổ dropdown lọc ở trang tìm
/// kiếm OPAC. Quản trị loại này qua trang admin có sẵn /admin/ebook-dig-types.</summary>
[Route("api/public/[controller]")]
public class PublicDigTypeController : PublicBaseController
{
    private readonly IPublicGenericRepository<DigType, PublicDigTypeSearchRequest> _repo;

    public PublicDigTypeController(IPublicGenericRepository<DigType, PublicDigTypeSearchRequest> repo)
    {
        _repo = repo;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicDigTypeSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicDigTypeResponse>>.Ok(new PagedResult<PublicDigTypeResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicDigTypeSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicDigTypeResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private static PublicDigTypeResponse ToResponse(DigType x) => new()
    {
        Id            = x.Id,
        PublicId      = x.PublicId,
        Code          = x.Code,
        DescriptionVn = x.DescriptionVn,
        DescriptionEn = x.DescriptionEn,
        SortOrder     = x.SortOrder,
        PortalId      = x.PortalId,
        Language      = x.Language,
    };
}
