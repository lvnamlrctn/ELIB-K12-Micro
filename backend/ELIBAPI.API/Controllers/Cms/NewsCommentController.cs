using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class NewsCommentController : GenericController<NewsComment, NewsCommentSearchRequest, NewsCommentRequest>
{
    public NewsCommentController(IGenericRepository<NewsComment, NewsCommentSearchRequest, NewsCommentRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("NEWS_MANAGE", "add")]
    public override async Task<IActionResult> Add([FromBody] NewsCommentRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] NewsCommentRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("NEWS_MANAGE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> Search([FromBody] NewsCommentSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("NEWS_MANAGE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] NewsCommentSearchRequest request) => await base.SearchAll(request);
}

