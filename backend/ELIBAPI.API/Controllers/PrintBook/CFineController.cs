using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class CFineController : GenericController<CFine, CFineSearchRequest, CFineRequest>
{
    public CFineController(IGenericRepository<CFine, CFineSearchRequest, CFineRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("FINES", "add")]
    public override async Task<IActionResult> Add([FromBody] CFineRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("FINES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CFineRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("FINES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("FINES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> Search([FromBody] CFineSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("FINES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CFineSearchRequest request) => await base.SearchAll(request);
}
