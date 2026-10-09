using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Catalogue/WorkSheet")]
public class CatalogueWorkSheetController : GenericController<BibWorksheet, BibWorksheetSearchRequest, BibWorksheetRequest>
{
    private readonly ELIBAPIDbContext _db;

    public CatalogueWorkSheetController(
        IGenericRepository<BibWorksheet, BibWorksheetSearchRequest, BibWorksheetRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

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

    [HttpGet("GetByBibType/{bibTypeId:long}")]
    [Permission("WORKSHEETS", "view")]
    public async Task<IActionResult> GetByBibType(long bibTypeId)
    {
        var tenantId   = GetTenantId();
        var privileged = IsPrivilegedRole();
        var list = await _db.BibWorksheets
            .Where(x => x.Bib_Type_Id == bibTypeId && x.IsDelete != 2 && (privileged || x.TenantId == null || x.TenantId == tenantId))
            .OrderBy(x => x.Name)
            .ToListAsync();
        return Ok(ApiResponse<List<BibWorksheet>>.Ok(list));
    }
}
