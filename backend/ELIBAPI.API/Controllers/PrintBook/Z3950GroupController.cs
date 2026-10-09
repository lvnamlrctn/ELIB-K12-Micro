using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class Z3950GroupController : GenericController<Z3950Group, Z3950GroupSearchRequest, Z3950GroupRequest>
{
    public Z3950GroupController(IGenericRepository<Z3950Group, Z3950GroupSearchRequest, Z3950GroupRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("Z3950_GROUPS", "add")]
    public override async Task<IActionResult> Add([FromBody] Z3950GroupRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("Z3950_GROUPS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] Z3950GroupRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("Z3950_GROUPS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("Z3950_GROUPS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("Z3950_GROUPS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("Z3950_GROUPS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("Z3950_GROUPS", "view")]
    public override async Task<IActionResult> Search([FromBody] Z3950GroupSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("Z3950_GROUPS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] Z3950GroupSearchRequest request) => await base.SearchAll(request);
}
