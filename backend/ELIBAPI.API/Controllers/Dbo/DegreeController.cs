using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Core.Common;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class DegreeController : GenericController<Degree, DboDegreeSearchRequest, DboDegreeRequest>
{
    public DegreeController(IGenericRepository<Degree, DboDegreeSearchRequest, DboDegreeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("DEGREES", "add")]
    public override async Task<IActionResult> Add([FromBody] DboDegreeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DEGREES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DboDegreeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DEGREES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DEGREES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPost("Import")]
    [Permission("DEGREES", "add")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Import(IFormFile file)
        => ImportFromExcel(file, row =>
        {
            var name = row["Name"]?.ToString();
            return string.IsNullOrWhiteSpace(name) ? null : new DboDegreeRequest { Name = name.Trim() };
        });

    [HttpGet("{id:long}")]
    [Permission("DEGREES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DEGREES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DEGREES", "view")]
    public override async Task<IActionResult> Search([FromBody] DboDegreeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DEGREES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DboDegreeSearchRequest request) => await base.SearchAll(request);
}

