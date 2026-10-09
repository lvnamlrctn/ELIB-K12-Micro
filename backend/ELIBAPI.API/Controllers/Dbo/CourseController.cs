using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Core.Common;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class CourseController : GenericController<Course, DboCoursSearchRequest, DboCoursRequest>
{
    public CourseController(IGenericRepository<Course, DboCoursSearchRequest, DboCoursRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("COURSES", "add")]
    public override async Task<IActionResult> Add([FromBody] DboCoursRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("COURSES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DboCoursRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("COURSES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("COURSES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPost("Import")]
    [Permission("COURSES", "add")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Import(IFormFile file)
        => ImportFromExcel(file, row =>
        {
            var name = row["Name"]?.ToString();
            return string.IsNullOrWhiteSpace(name) ? null : new DboCoursRequest { Name = name.Trim() };
        });

    [HttpGet("{id:long}")]
    [Permission("COURSES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("COURSES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("COURSES", "view")]
    public override async Task<IActionResult> Search([FromBody] DboCoursSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("COURSES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DboCoursSearchRequest request) => await base.SearchAll(request);
}

