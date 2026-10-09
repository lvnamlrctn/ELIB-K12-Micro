using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

/// <summary>Danh mục "chủ đề" (Ebook.Topic) công khai -- dùng đổ dropdown chọn hồ sơ quan tâm của
/// bạn đọc (trang Hồ sơ OPAC), để chatbot gợi ý tài liệu theo hồ sơ.</summary>
[Route("api/public/[controller]")]
public class PublicEbookTopicController : PublicBaseController
{
    private readonly IPublicGenericRepository<EbookTopic, PublicEbookTopicSearchRequest> _repo;

    public PublicEbookTopicController(IPublicGenericRepository<EbookTopic, PublicEbookTopicSearchRequest> repo)
    {
        _repo = repo;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicEbookTopicSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicEbookTopicResponse>>.Ok(new PagedResult<PublicEbookTopicResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicEbookTopicSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicEbookTopicResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private static PublicEbookTopicResponse ToResponse(EbookTopic x) => new()
    {
        Id       = x.Id,
        PublicId = x.PublicId,
        Name     = x.Name,
        ParentId = x.ParentId,
        Level    = x.Level,
        Order    = x.Order,
    };
}
