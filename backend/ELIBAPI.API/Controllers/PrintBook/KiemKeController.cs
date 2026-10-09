using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class KiemKeController : GenericController<KiemKe, KiemKeSearchRequest, KiemKeRequest>
{
    public KiemKeController(IGenericRepository<KiemKe, KiemKeSearchRequest, KiemKeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("INVENTORY", "add")]
    public override async Task<IActionResult> Add([FromBody] KiemKeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("INVENTORY", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] KiemKeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("INVENTORY", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("INVENTORY", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("INVENTORY", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("INVENTORY", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("INVENTORY", "view")]
    public override async Task<IActionResult> Search([FromBody] KiemKeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("INVENTORY", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] KiemKeSearchRequest request) => await base.SearchAll(request);
}
