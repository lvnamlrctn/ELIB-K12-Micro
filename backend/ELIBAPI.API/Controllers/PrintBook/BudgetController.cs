using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class BudgetController : GenericController<Budget, BudgetSearchRequest, BudgetRequest>
{
    public BudgetController(IGenericRepository<Budget, BudgetSearchRequest, BudgetRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("BUDGETS", "add")]
    public override async Task<IActionResult> Add([FromBody] BudgetRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("BUDGETS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BudgetRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("BUDGETS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("BUDGETS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("BUDGETS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("BUDGETS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("BUDGETS", "view")]
    public override async Task<IActionResult> Search([FromBody] BudgetSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("BUDGETS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BudgetSearchRequest request) => await base.SearchAll(request);
}
