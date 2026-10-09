using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class DFixFieldPostController : GenericController<DFixFieldPost, DFixFieldPostSearchRequest, DFixFieldPostRequest>
{
    public DFixFieldPostController(IGenericRepository<DFixFieldPost, DFixFieldPostSearchRequest, DFixFieldPostRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("DFIX_FIELD_POST", "add")]
    public override async Task<IActionResult> Add([FromBody] DFixFieldPostRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DFIX_FIELD_POST", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DFixFieldPostRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DFIX_FIELD_POST", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DFIX_FIELD_POST", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("DFIX_FIELD_POST", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DFIX_FIELD_POST", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DFIX_FIELD_POST", "view")]
    public override async Task<IActionResult> Search([FromBody] DFixFieldPostSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DFIX_FIELD_POST", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DFixFieldPostSearchRequest request) => await base.SearchAll(request);
}
