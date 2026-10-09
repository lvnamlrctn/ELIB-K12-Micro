using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class ChucVuController : GenericController<ChucVu, ChucVuSearchRequest, ChucVuRequest>
{
    public ChucVuController(IGenericRepository<ChucVu, ChucVuSearchRequest, ChucVuRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("POSITIONS", "add")]
    public override async Task<IActionResult> Add([FromBody] ChucVuRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("POSITIONS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ChucVuRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("POSITIONS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("POSITIONS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPost("Import")]
    [Permission("POSITIONS", "add")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Import(IFormFile file)
        => ImportFromExcel(file, row =>
        {
            var name = row["Name"]?.ToString();
            return string.IsNullOrWhiteSpace(name) ? null : new ChucVuRequest { Name = name.Trim() };
        });

    [HttpGet("{id:long}")]
    [Permission("POSITIONS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("POSITIONS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("POSITIONS", "view")]
    public override async Task<IActionResult> Search([FromBody] ChucVuSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("POSITIONS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ChucVuSearchRequest request) => await base.SearchAll(request);
}

