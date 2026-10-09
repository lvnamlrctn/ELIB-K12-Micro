using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class CounterController : GenericController<Counter, CounterSearchRequest, CounterRequest>
{
    public CounterController(IGenericRepository<Counter, CounterSearchRequest, CounterRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("COUNTER", "add")]
    public override async Task<IActionResult> Add([FromBody] CounterRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("COUNTER", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CounterRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("COUNTER", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("COUNTER", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("COUNTER", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("COUNTER", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("COUNTER", "view")]
    public override async Task<IActionResult> Search([FromBody] CounterSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("COUNTER", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CounterSearchRequest request) => await base.SearchAll(request);
}

