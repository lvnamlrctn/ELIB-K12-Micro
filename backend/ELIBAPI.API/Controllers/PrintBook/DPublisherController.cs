using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class DPublisherController : GenericController<DPublisher, DPublisherSearchRequest, DPublisherRequest>
{
    public DPublisherController(IGenericRepository<DPublisher, DPublisherSearchRequest, DPublisherRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("D_PUBLISHER", "add")]
    public override async Task<IActionResult> Add([FromBody] DPublisherRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("D_PUBLISHER", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DPublisherRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("D_PUBLISHER", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("D_PUBLISHER", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("D_PUBLISHER", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("D_PUBLISHER", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("D_PUBLISHER", "view")]
    public override async Task<IActionResult> Search([FromBody] DPublisherSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("D_PUBLISHER", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DPublisherSearchRequest request) => await base.SearchAll(request);
}
