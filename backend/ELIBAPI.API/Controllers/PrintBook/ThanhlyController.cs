using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class ThanhlyController : GenericController<Thanhly, ThanhlySearchRequest, ThanhlyRequest>
{
    public ThanhlyController(IGenericRepository<Thanhly, ThanhlySearchRequest, ThanhlyRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("LIQUIDATES", "add")]
    public override async Task<IActionResult> Add([FromBody] ThanhlyRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("LIQUIDATES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ThanhlyRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("LIQUIDATES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("LIQUIDATES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("LIQUIDATES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("LIQUIDATES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("LIQUIDATES", "view")]
    public override async Task<IActionResult> Search([FromBody] ThanhlySearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("LIQUIDATES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ThanhlySearchRequest request) => await base.SearchAll(request);
}
