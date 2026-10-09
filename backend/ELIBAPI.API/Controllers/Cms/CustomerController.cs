using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class CustomerController : GenericController<Customer, CustomerSearchRequest, CustomerRequest>
{
    public CustomerController(IGenericRepository<Customer, CustomerSearchRequest, CustomerRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CUSTOMER", "add")]
    public override async Task<IActionResult> Add([FromBody] CustomerRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CUSTOMER", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CustomerRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CUSTOMER", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CUSTOMER", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("CUSTOMER", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CUSTOMER", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CUSTOMER", "view")]
    public override async Task<IActionResult> Search([FromBody] CustomerSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CUSTOMER", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CustomerSearchRequest request) => await base.SearchAll(request);
}

