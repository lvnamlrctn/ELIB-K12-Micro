using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Core.Common;
using Microsoft.AspNetCore.Mvc;
using ExcelDataReader;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class TenantController : GenericController<Tenant, TenantSearchRequest, TenantRequest>
{
    public TenantController(IGenericRepository<Tenant, TenantSearchRequest, TenantRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("DEPARTMENTS", "add")]
    public override async Task<IActionResult> Add([FromBody] TenantRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DEPARTMENTS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] TenantRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DEPARTMENTS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DEPARTMENTS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPost("Import")]
    [Permission("DEPARTMENTS", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"]));

        var requests = new List<TenantRequest>();
        using (var stream = file.OpenReadStream())
        using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
        {
            var result = reader.AsDataSet(new ExcelDataReader.ExcelDataSetConfiguration
            {
                ConfigureDataTable = (_) => new ExcelDataReader.ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            if (result.Tables.Count > 0)
            {
                var table = result.Tables[0];
                if (!table.Columns.Contains("Name"))
                    return BadRequest(ApiResponse<object>.Fail(Localizer["ColumnNameNotFound"]));

                foreach (System.Data.DataRow row in table.Rows)
                {
                    var name = row["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var code = table.Columns.Contains("Code") ? row["Code"]?.ToString() : null;
                        requests.Add(new TenantRequest { Code = code?.Trim(), Name = name.Trim() });
                    }
                }
            }
        }

        if (requests.Count == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["NoDataToImport"]));

        await _repo.AddRangeAsync(requests);
        return Ok(ApiResponse<object>.Ok(null!, string.Format(Localizer["ImportSuccess"], requests.Count)));
    }

    [HttpGet("{id:long}")]
    [Permission("DEPARTMENTS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DEPARTMENTS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DEPARTMENTS", "view")]
    public override async Task<IActionResult> Search([FromBody] TenantSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DEPARTMENTS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] TenantSearchRequest request) => await base.SearchAll(request);
}
