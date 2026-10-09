using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class ClassController : GenericController<Class, ClassSearchRequest, ClassRequest>
{
    public ClassController(IGenericRepository<Class, ClassSearchRequest, ClassRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CLASSES", "add")]
    public override async Task<IActionResult> Add([FromBody] ClassRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CLASSES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ClassRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CLASSES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CLASSES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpPost("Import")]
    [Permission("CLASSES", "add")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Import(IFormFile file)
        => ImportFromExcel(file, row =>
        {
            var name = row["Name"]?.ToString();
            return string.IsNullOrWhiteSpace(name) ? null : new ClassRequest { Name = name.Trim() };
        });

    [HttpGet("{id:long}")]
    [Permission("CLASSES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CLASSES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CLASSES", "view")]
    public override async Task<IActionResult> Search([FromBody] ClassSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CLASSES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ClassSearchRequest request) => await base.SearchAll(request);
}

