using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

/// <summary>Danh mục "môn học/lĩnh vực" (Ebook.Subject) công khai -- dùng đổ dropdown chọn hồ sơ
/// quan tâm của bạn đọc (trang Hồ sơ OPAC), để chatbot gợi ý tài liệu theo hồ sơ.</summary>
[Route("api/public/[controller]")]
public class PublicEbookSubjectController : PublicBaseController
{
    private readonly IPublicGenericRepository<EbookSubject, PublicEbookSubjectSearchRequest> _repo;

    public PublicEbookSubjectController(IPublicGenericRepository<EbookSubject, PublicEbookSubjectSearchRequest> repo)
    {
        _repo = repo;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicEbookSubjectSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicEbookSubjectResponse>>.Ok(new PagedResult<PublicEbookSubjectResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicEbookSubjectSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicEbookSubjectResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private static PublicEbookSubjectResponse ToResponse(EbookSubject x) => new()
    {
        Id        = x.Id,
        PublicId  = x.PublicId,
        Name      = x.Name,
        ParentId  = x.ParentId,
        Level     = x.Level,
        SortOrder = x.SortOrder,
    };
}
