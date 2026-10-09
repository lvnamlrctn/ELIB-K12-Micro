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
public class WorksheetSubfieldController : GenericController<WorksheetSubfield, WorksheetSubfieldSearchRequest, WorksheetSubfieldRequest>
{
    private readonly ELIBAPIDbContext _db;

    public WorksheetSubfieldController(
        IGenericRepository<WorksheetSubfield, WorksheetSubfieldSearchRequest, WorksheetSubfieldRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    [HttpPost("GetByFields")]
    [Permission("WORKSHEETS", "view")]
    public async Task<IActionResult> GetByFields([FromBody] List<long> worksheetFieldIds)
    {
        var tenantId   = GetTenantId();
        var privileged = IsPrivilegedRole();
        var list = await _db.WorksheetSubfields
            .Where(x => worksheetFieldIds.Contains(x.Worksheet_Field_Id ?? 0) && x.IsDelete != 2 && (privileged || x.TenantId == null || x.TenantId == tenantId))
            .OrderBy(x => x.Subfield)
            .ToListAsync();
        return Ok(ApiResponse<List<WorksheetSubfield>>.Ok(list));
    }

    [HttpPost("Add")]
    [Permission("WORKSHEETS", "add")]
    public override async Task<IActionResult> Add([FromBody] WorksheetSubfieldRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("WORKSHEETS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] WorksheetSubfieldRequest request) => await base.Update(publicId, request);

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
    public override async Task<IActionResult> Search([FromBody] WorksheetSubfieldSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("WORKSHEETS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] WorksheetSubfieldSearchRequest request) => await base.SearchAll(request);
}
