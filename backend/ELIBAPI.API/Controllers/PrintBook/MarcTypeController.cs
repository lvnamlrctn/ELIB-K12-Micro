using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class MarcTypeController : GenericController<MarcType, MarcTypeSearchRequest, MarcTypeRequest>
{
    public MarcTypeController(IGenericRepository<MarcType, MarcTypeSearchRequest, MarcTypeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("BIB_TYPES", "add")]
    public override async Task<IActionResult> Add([FromBody] MarcTypeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("BIB_TYPES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MarcTypeRequest request) => await base.Update(publicId, request);

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
    public override async Task<IActionResult> Search([FromBody] MarcTypeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("BIB_TYPES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MarcTypeSearchRequest request) => await base.SearchAll(request);
}
