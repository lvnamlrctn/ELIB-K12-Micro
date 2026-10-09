using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class OrgController : GenericController<Org, OrgSearchRequest, OrgRequest>
{
    private readonly IOrgRepository _orgRepo;

    public OrgController(IOrgRepository repo) : base(repo)
        => _orgRepo = repo;

    [HttpPost("Add")]    [Permission("ORGS", "add")]
    public override Task<IActionResult> Add([FromBody] OrgRequest request) => base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("ORGS", "edit")]
    public override Task<IActionResult> Update(Guid publicId, [FromBody] OrgRequest request) => base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("ORGS", "delete")]
    public override Task<IActionResult> Delete(Guid publicId) => base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("ORGS", "edit")]
    public override Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => base.ChangeStatus(request);

    [HttpGet("{id:long}")]               [Permission("ORGS", "view")]
    public override Task<IActionResult> GetById(long id) => base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("ORGS", "view")]
    public override Task<IActionResult> GetByPublicId(Guid publicId) => base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("ORGS", "view")]
    public override Task<IActionResult> Search([FromBody] OrgSearchRequest request) => base.Search(request);

    [HttpPost("SearchAll")] [Permission("ORGS", "view")]
    public override Task<IActionResult> SearchAll([FromBody] OrgSearchRequest request) => base.SearchAll(request);

    // ── GetTree ───────────────────────────────────────────────────────────────

    [HttpPost("GetTree")]
    [Permission("ORGS", "view")]
    public async Task<IActionResult> GetTree([FromBody] OrgSearchRequest request)
    {
        var tree = await _orgRepo.GetTreeAsync(request);
        return Ok(ApiResponse<List<OrgTreeResponse>>.Ok(tree));
    }

    // ── UpdateOrder ───────────────────────────────────────────────────────────

    [HttpPut("UpdateOrder")]
    [Permission("ORGS", "edit")]
    public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderRequest request)
    {
        try
        {
            await _orgRepo.UpdateOrderAsync(request.PublicId, request.NewOrder);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    [HttpPut("Move/{publicId:guid}")]
    [Permission("ORGS", "edit")]
    public async Task<IActionResult> Move(Guid publicId, [FromBody] MoveCategoryRequest request)
    {
        try
        {
            var result = await _orgRepo.MoveAsync(publicId, request.NewParentId, request.NewOrder);
            return Ok(ApiResponse<Org>.Ok(result, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<Org>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<Org>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── DeleteWithChildren ────────────────────────────────────────────────────

    [HttpDelete("DeleteWithChildren/{publicId:guid}")]
    [Permission("ORGS", "delete")]
    public async Task<IActionResult> DeleteWithChildren(Guid publicId)
    {
        try
        {
            await _orgRepo.DeleteWithChildrenAsync(publicId);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["DeleteSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── Import ────────────────────────────────────────────────────────────────

    [HttpPost("Import")]
    [Permission("ORGS", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"]));

        var requests = new List<OrgRequest>();
        await using var stream = file.OpenReadStream();
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        if (dataSet.Tables.Count > 0)
        {
            var table = dataSet.Tables[0];
            if (!table.Columns.Contains("Name"))
                return BadRequest(ApiResponse<object>.Fail(Localizer["ColumnNameNotFound"]));

            foreach (System.Data.DataRow row in table.Rows)
            {
                var name = row["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name))
                    requests.Add(new OrgRequest { Name = name });
            }
        }

        if (requests.Count == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["NoDataToImport"]));

        await _repo.AddRangeAsync(requests);
        return Ok(ApiResponse<object>.Ok(null!, string.Format(Localizer["ImportSuccess"], requests.Count)));
    }
}
