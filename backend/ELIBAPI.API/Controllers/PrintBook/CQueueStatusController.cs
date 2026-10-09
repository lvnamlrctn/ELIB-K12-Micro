using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class CQueueStatusController : GenericController<CQueueStatus, CQueueStatusSearchRequest, CQueueStatusRequest>
{
    public CQueueStatusController(IGenericRepository<CQueueStatus, CQueueStatusSearchRequest, CQueueStatusRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("C_QUEUE_STATUS", "add")]
    public override async Task<IActionResult> Add([FromBody] CQueueStatusRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("C_QUEUE_STATUS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CQueueStatusRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("C_QUEUE_STATUS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("C_QUEUE_STATUS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("C_QUEUE_STATUS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("C_QUEUE_STATUS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("C_QUEUE_STATUS", "view")]
    public override async Task<IActionResult> Search([FromBody] CQueueStatusSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("C_QUEUE_STATUS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CQueueStatusSearchRequest request) => await base.SearchAll(request);
}
