using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class CheckOutController : GenericController<CheckOut, CheckOutSearchRequest, CheckOutRequest>
{
    public CheckOutController(IGenericRepository<CheckOut, CheckOutSearchRequest, CheckOutRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CHECK_IN_OUT", "add")]
    public override async Task<IActionResult> Add([FromBody] CheckOutRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CHECK_IN_OUT", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CheckOutRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CHECK_IN_OUT", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CHECK_IN_OUT", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("CHECK_IN_OUT", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CHECK_IN_OUT", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CHECK_IN_OUT", "view")]
    public override async Task<IActionResult> Search([FromBody] CheckOutSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CHECK_IN_OUT", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CheckOutSearchRequest request) => await base.SearchAll(request);
}
