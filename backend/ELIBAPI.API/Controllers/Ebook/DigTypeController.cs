using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class DigTypeController : GenericController<DigType, DigTypeSearchRequest, DigTypeRequest>
{
    public DigTypeController(IGenericRepository<DigType, DigTypeSearchRequest, DigTypeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EBOOK_DIG_TYPE", "add")]
    public override async Task<IActionResult> Add([FromBody] DigTypeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EBOOK_DIG_TYPE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DigTypeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EBOOK_DIG_TYPE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EBOOK_DIG_TYPE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("EBOOK_DIG_TYPE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EBOOK_DIG_TYPE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EBOOK_DIG_TYPE", "view")]
    public override async Task<IActionResult> Search([FromBody] DigTypeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EBOOK_DIG_TYPE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DigTypeSearchRequest request) => await base.SearchAll(request);
}

