using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Evaluate;

[Route("api/Evaluate/[controller]")]
public class EvaluateProgramController : GenericController<EvaluateProgram, EvaluateProgramSearchRequest, EvaluateProgramRequest>
{
    public EvaluateProgramController(IGenericRepository<EvaluateProgram, EvaluateProgramSearchRequest, EvaluateProgramRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EVALUATEPROGRAM", "add")]
    public override async Task<IActionResult> Add([FromBody] EvaluateProgramRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EVALUATEPROGRAM", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EvaluateProgramRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EVALUATEPROGRAM", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EVALUATEPROGRAM", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("EVALUATEPROGRAM", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EVALUATEPROGRAM", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EVALUATEPROGRAM", "view")]
    public override async Task<IActionResult> Search([FromBody] EvaluateProgramSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EVALUATEPROGRAM", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EvaluateProgramSearchRequest request) => await base.SearchAll(request);
}

