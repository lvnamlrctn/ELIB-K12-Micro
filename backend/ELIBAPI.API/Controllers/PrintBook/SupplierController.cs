using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class SupplierController : GenericController<Supplier, SupplierSearchRequest, SupplierRequest>
{
    public SupplierController(IGenericRepository<Supplier, SupplierSearchRequest, SupplierRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("SUPPLIERS", "add")]
    public override async Task<IActionResult> Add([FromBody] SupplierRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SUPPLIERS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] SupplierRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SUPPLIERS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("SUPPLIERS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("SUPPLIERS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SUPPLIERS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("SUPPLIERS", "view")]
    public override async Task<IActionResult> Search([FromBody] SupplierSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("SUPPLIERS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] SupplierSearchRequest request) => await base.SearchAll(request);
}
