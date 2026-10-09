using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class SerialItemController : GenericController<SerialItem, SerialItemSearchRequest, SerialItemRequest>
{
    public SerialItemController(IGenericRepository<SerialItem, SerialItemSearchRequest, SerialItemRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("SERIAL_ISSUES", "add")]
    public override async Task<IActionResult> Add([FromBody] SerialItemRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SERIAL_ISSUES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] SerialItemRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SERIAL_ISSUES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("SERIAL_ISSUES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("SERIAL_ISSUES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SERIAL_ISSUES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("SERIAL_ISSUES", "view")]
    public override async Task<IActionResult> Search([FromBody] SerialItemSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("SERIAL_ISSUES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] SerialItemSearchRequest request) => await base.SearchAll(request);
}
