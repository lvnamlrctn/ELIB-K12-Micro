using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class AbDelivererStatusController : GenericController<AbDelivererStatus, AbDelivererStatusSearchRequest, AbDelivererStatusRequest>
{
    public AbDelivererStatusController(IGenericRepository<AbDelivererStatus, AbDelivererStatusSearchRequest, AbDelivererStatusRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("AB_DELIVERER_STATUS", "add")]
    public override async Task<IActionResult> Add([FromBody] AbDelivererStatusRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_DELIVERER_STATUS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AbDelivererStatusRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_DELIVERER_STATUS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AB_DELIVERER_STATUS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AB_DELIVERER_STATUS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_DELIVERER_STATUS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_DELIVERER_STATUS", "view")]
    public override async Task<IActionResult> Search([FromBody] AbDelivererStatusSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_DELIVERER_STATUS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AbDelivererStatusSearchRequest request) => await base.SearchAll(request);
}
