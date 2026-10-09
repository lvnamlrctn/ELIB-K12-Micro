using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class CurrencyController : GenericController<Currency, CurrencySearchRequest, CurrencyRequest>
{
    public CurrencyController(IGenericRepository<Currency, CurrencySearchRequest, CurrencyRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CURRENCIES", "add")]
    public override async Task<IActionResult> Add([FromBody] CurrencyRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CURRENCIES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CurrencyRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CURRENCIES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CURRENCIES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("CURRENCIES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CURRENCIES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CURRENCIES", "view")]
    public override async Task<IActionResult> Search([FromBody] CurrencySearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CURRENCIES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CurrencySearchRequest request) => await base.SearchAll(request);
}

