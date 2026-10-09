using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Evaluate;

[Route("api/Evaluate/[controller]")]
public class EvaluateCourseController : GenericController<EvaluateCourse, EvaluateCourseSearchRequest, EvaluateCourseRequest>
{
    public EvaluateCourseController(IGenericRepository<EvaluateCourse, EvaluateCourseSearchRequest, EvaluateCourseRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EVALUATECOURSE", "add")]
    public override async Task<IActionResult> Add([FromBody] EvaluateCourseRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EVALUATECOURSE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EvaluateCourseRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EVALUATECOURSE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EVALUATECOURSE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("EVALUATECOURSE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EVALUATECOURSE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EVALUATECOURSE", "view")]
    public override async Task<IActionResult> Search([FromBody] EvaluateCourseSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EVALUATECOURSE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EvaluateCourseSearchRequest request) => await base.SearchAll(request);
}

