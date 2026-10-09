using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class DicAuthorController : GenericController<DicAuthor, DicAuthorSearchRequest, DicAuthorRequest>
{
    public DicAuthorController(IGenericRepository<DicAuthor, DicAuthorSearchRequest, DicAuthorRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("DIC_AUTHORS", "add")]
    public override async Task<IActionResult> Add([FromBody] DicAuthorRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DIC_AUTHORS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DicAuthorRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DIC_AUTHORS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DIC_AUTHORS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("DIC_AUTHORS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DIC_AUTHORS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DIC_AUTHORS", "view")]
    public override async Task<IActionResult> Search([FromBody] DicAuthorSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DIC_AUTHORS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DicAuthorSearchRequest request) => await base.SearchAll(request);
}
