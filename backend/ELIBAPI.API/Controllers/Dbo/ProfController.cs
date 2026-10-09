using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Core.Common;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class ProfController : GenericController<Prof, ProfSearchRequest, ProfRequest>
{
    public ProfController(IGenericRepository<Prof, ProfSearchRequest, ProfRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("PROFS", "add")]
    public override async Task<IActionResult> Add([FromBody] ProfRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("PROFS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ProfRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("PROFS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("PROFS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPost("Import")]
    [Permission("PROFS", "add")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Import(IFormFile file)
        => ImportFromExcel(file, row =>
        {
            var name = row["Name"]?.ToString();
            return string.IsNullOrWhiteSpace(name) ? null : new ProfRequest { Name = name.Trim() };
        });

    [HttpGet("{id:long}")]
    [Permission("PROFS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("PROFS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("PROFS", "view")]
    public override async Task<IActionResult> Search([FromBody] ProfSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("PROFS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ProfSearchRequest request) => await base.SearchAll(request);
}

