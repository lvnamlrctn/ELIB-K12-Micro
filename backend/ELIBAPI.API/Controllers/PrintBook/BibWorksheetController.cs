using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class BibWorksheetController : GenericController<BibWorksheet, BibWorksheetSearchRequest, BibWorksheetRequest>
{
    public BibWorksheetController(IGenericRepository<BibWorksheet, BibWorksheetSearchRequest, BibWorksheetRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("WORKSHEETS", "add")]
    public override async Task<IActionResult> Add([FromBody] BibWorksheetRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("WORKSHEETS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BibWorksheetRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("WORKSHEETS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("WORKSHEETS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("WORKSHEETS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("WORKSHEETS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("WORKSHEETS", "view")]
    public override async Task<IActionResult> Search([FromBody] BibWorksheetSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("WORKSHEETS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BibWorksheetSearchRequest request) => await base.SearchAll(request);
}
