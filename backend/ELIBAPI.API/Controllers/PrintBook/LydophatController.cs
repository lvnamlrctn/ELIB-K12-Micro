using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class LydophatController : GenericController<Lydophat, LydophatSearchRequest, LydophatRequest>
{
    public LydophatController(IGenericRepository<Lydophat, LydophatSearchRequest, LydophatRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("FINE_REASONS", "add")]
    public override async Task<IActionResult> Add([FromBody] LydophatRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("FINE_REASONS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] LydophatRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("FINE_REASONS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("FINE_REASONS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("FINE_REASONS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("FINE_REASONS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("FINE_REASONS", "view")]
    public override async Task<IActionResult> Search([FromBody] LydophatSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("FINE_REASONS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] LydophatSearchRequest request) => await base.SearchAll(request);
}
