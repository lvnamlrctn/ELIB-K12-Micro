using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class DKeyController : GenericController<DKey, DKeySearchRequest, DKeyRequest>
{
    public DKeyController(IGenericRepository<DKey, DKeySearchRequest, DKeyRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("D_KEY", "add")]
    public override async Task<IActionResult> Add([FromBody] DKeyRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("D_KEY", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DKeyRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("D_KEY", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("D_KEY", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("D_KEY", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("D_KEY", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("D_KEY", "view")]
    public override async Task<IActionResult> Search([FromBody] DKeySearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("D_KEY", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DKeySearchRequest request) => await base.SearchAll(request);
}
