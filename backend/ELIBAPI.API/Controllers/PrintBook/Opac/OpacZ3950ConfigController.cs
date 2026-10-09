using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Opac/Z3950Config")]
public class OpacZ3950ConfigController : GenericController<Z3950Config, Z3950ConfigSearchRequest, Z3950ConfigRequest>
{
    public OpacZ3950ConfigController(IGenericRepository<Z3950Config, Z3950ConfigSearchRequest, Z3950ConfigRequest> repo) : base(repo) { }

    [HttpPost("Add")] [Permission("Z3950_CONFIGS", "add")]
    public override async Task<IActionResult> Add([FromBody] Z3950ConfigRequest r) => await base.Add(r);

    [HttpPut("Update/{publicId:guid}")] [Permission("Z3950_CONFIGS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] Z3950ConfigRequest r) => await base.Update(publicId, r);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("Z3950_CONFIGS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("Z3950_CONFIGS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest r) => await base.ChangeStatus(r);

    [HttpGet("{id:long}")] [Permission("Z3950_CONFIGS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("Z3950_CONFIGS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")] [Permission("Z3950_CONFIGS", "view")]
    public override async Task<IActionResult> Search([FromBody] Z3950ConfigSearchRequest r) => await base.Search(r);

    [HttpPost("SearchAll")] [Permission("Z3950_CONFIGS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] Z3950ConfigSearchRequest r) => await base.SearchAll(r);
}
