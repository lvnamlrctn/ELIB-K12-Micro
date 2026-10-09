using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class AbOrderDetailController : GenericController<AbOrderDetail, AbOrderDetailSearchRequest, AbOrderDetailRequest>
{
    public AbOrderDetailController(IGenericRepository<AbOrderDetail, AbOrderDetailSearchRequest, AbOrderDetailRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("AB_ORDERS", "add")]
    public override async Task<IActionResult> Add([FromBody] AbOrderDetailRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_ORDERS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AbOrderDetailRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_ORDERS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AB_ORDERS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AB_ORDERS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_ORDERS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_ORDERS", "view")]
    public override async Task<IActionResult> Search([FromBody] AbOrderDetailSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_ORDERS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AbOrderDetailSearchRequest request) => await base.SearchAll(request);
}
