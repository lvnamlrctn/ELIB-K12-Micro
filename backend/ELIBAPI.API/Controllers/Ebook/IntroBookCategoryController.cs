using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class IntroBookCategoryController : GenericController<IntroBookCategory, IntroBookCategorySearchRequest, IntroBookCategoryRequest>
{
    public IntroBookCategoryController(IGenericRepository<IntroBookCategory, IntroBookCategorySearchRequest, IntroBookCategoryRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("DOC_SEARCH", "add")]
    public override async Task<IActionResult> Add([FromBody] IntroBookCategoryRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DOC_SEARCH", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] IntroBookCategoryRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DOC_SEARCH", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DOC_SEARCH", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("DOC_SEARCH", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DOC_SEARCH", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DOC_SEARCH", "view")]
    public override async Task<IActionResult> Search([FromBody] IntroBookCategorySearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DOC_SEARCH", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] IntroBookCategorySearchRequest request) => await base.SearchAll(request);
}

