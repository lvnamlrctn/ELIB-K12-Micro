using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Evaluate;

[Route("api/Evaluate/[controller]")]
public class CourseOptionController : GenericController<CourseOption, CourseOptionSearchRequest, CourseOptionRequest>
{
    public CourseOptionController(IGenericRepository<CourseOption, CourseOptionSearchRequest, CourseOptionRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("MONHOC", "add")]
    public override async Task<IActionResult> Add([FromBody] CourseOptionRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("MONHOC", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CourseOptionRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("MONHOC", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("MONHOC", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> Search([FromBody] CourseOptionSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("MONHOC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CourseOptionSearchRequest request) => await base.SearchAll(request);
}

