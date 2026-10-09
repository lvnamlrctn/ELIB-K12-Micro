using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class BibTypeController : GenericController<BibType, BibTypeSearchRequest, BibTypeRequest>
{
    public BibTypeController(IGenericRepository<BibType, BibTypeSearchRequest, BibTypeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("BIB_TYPES", "add")]
    public override async Task<IActionResult> Add([FromBody] BibTypeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("BIB_TYPES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BibTypeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("BIB_TYPES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("BIB_TYPES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("BIB_TYPES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("BIB_TYPES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("BIB_TYPES", "view")]
    public override async Task<IActionResult> Search([FromBody] BibTypeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("BIB_TYPES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BibTypeSearchRequest request) => await base.SearchAll(request);
}
