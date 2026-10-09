using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class WorksheetFieldController : GenericController<WorksheetField, WorksheetFieldSearchRequest, WorksheetFieldRequest>
{
    private readonly ELIBAPIDbContext _db;

    public WorksheetFieldController(
        IGenericRepository<WorksheetField, WorksheetFieldSearchRequest, WorksheetFieldRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    [HttpGet("GetByWorksheet/{bibWorksheetId:long}")]
    [Permission("WORKSHEETS", "view")]
    public async Task<IActionResult> GetByWorksheet(long bibWorksheetId)
    {
        var tenantId   = GetTenantId();
        var privileged = IsPrivilegedRole();
        var list = await _db.WorksheetFields
            .Where(x => x.Bib_Worksheet_Id == bibWorksheetId && x.IsDelete != 2 && (privileged || x.TenantId == null || x.TenantId == tenantId))
            .OrderBy(x => x.Field)
            .ToListAsync();
        return Ok(ApiResponse<List<WorksheetField>>.Ok(list));
    }

    [HttpPost("Add")]
    [Permission("WORKSHEETS", "add")]
    public override async Task<IActionResult> Add([FromBody] WorksheetFieldRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("WORKSHEETS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] WorksheetFieldRequest request) => await base.Update(publicId, request);

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
    public override async Task<IActionResult> Search([FromBody] WorksheetFieldSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("WORKSHEETS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] WorksheetFieldSearchRequest request) => await base.SearchAll(request);
}
