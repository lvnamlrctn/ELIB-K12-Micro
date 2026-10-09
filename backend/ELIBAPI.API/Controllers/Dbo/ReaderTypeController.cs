using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Core.Common;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class ReaderTypeController : GenericController<ReaderType, ReaderTypeSearchRequest, ReaderTypeRequest>
{
    public ReaderTypeController(IGenericRepository<ReaderType, ReaderTypeSearchRequest, ReaderTypeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("READER_TYPES", "add")]
    public override async Task<IActionResult> Add([FromBody] ReaderTypeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("READER_TYPES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ReaderTypeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("READER_TYPES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("READER_TYPES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPost("Import")]
    [Permission("READER_TYPES", "add")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Import(IFormFile file)
        => ImportFromExcel(file, row =>
        {
            var name = row["Name"]?.ToString();
            return string.IsNullOrWhiteSpace(name) ? null : new ReaderTypeRequest { Name = name.Trim() };
        });

    [HttpGet("{id:long}")]
    [Permission("READER_TYPES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("READER_TYPES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("READER_TYPES", "view")]
    public override async Task<IActionResult> Search([FromBody] ReaderTypeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("READER_TYPES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ReaderTypeSearchRequest request) => await base.SearchAll(request);
}

