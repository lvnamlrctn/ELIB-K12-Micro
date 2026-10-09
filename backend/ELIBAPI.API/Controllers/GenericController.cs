using ELIBAPI.API;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Controllers;

public abstract class GenericController<TEntity, TSearch, TRequest> : BaseApiController
    where TEntity  : class
    where TSearch  : SearchRequest
    where TRequest : class
{
    protected readonly IGenericRepository<TEntity, TSearch, TRequest> _repo;

    protected GenericController(IGenericRepository<TEntity, TSearch, TRequest> repo)
        => _repo = repo;

    protected IStringLocalizer Localizer =>
        HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();

    [HttpGet("{id:long}")]
    public virtual async Task<IActionResult> GetById(long id)
    {
        var entity = await _repo.GetByIdAsync(id);
        return entity == null
            ? NotFound(ApiResponse<TEntity>.Fail(Localizer["NotFound"], 404))
            : Ok(ApiResponse<TEntity>.Ok(entity));
    }

    [HttpGet("GetById/{publicId:guid}")]
    public virtual async Task<IActionResult> GetByPublicId(Guid publicId)
    {
        var entity = await _repo.GetByPublicIdAsync(publicId);
        return entity == null
            ? NotFound(ApiResponse<TEntity>.Fail(Localizer["NotFound"], 404))
            : Ok(ApiResponse<TEntity>.Ok(entity));
    }

    [HttpPost("Search")]
    public virtual async Task<IActionResult> Search([FromBody] TSearch request)
    {
        var result = await _repo.SearchAsync(request);
        return Ok(ApiResponse<PagedResult<TEntity>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    public virtual async Task<IActionResult> SearchAll([FromBody] TSearch request)
    {
        var items = await _repo.SearchAllAsync(request);
        return Ok(ApiResponse<List<TEntity>>.Ok(items));
    }

    [HttpPost("Add")]
    public virtual async Task<IActionResult> Add([FromBody] TRequest request)
    {
        var entity = await _repo.AddAsync(request);
        return Ok(ApiResponse<TEntity>.Ok(entity, Localizer["AddSuccess"]));
    }

    [HttpPut("Update/{publicId:guid}")]
    public virtual async Task<IActionResult> Update(Guid publicId, [FromBody] TRequest request)
    {
        try
        {
            var entity = await _repo.UpdateAsync(publicId, request);
            return Ok(ApiResponse<TEntity>.Ok(entity, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<TEntity>.Fail(Localizer["NotFound"], 404));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, ApiResponse<TEntity>.Fail(Localizer["ForbiddenDepartment"], 403));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<TEntity>.Fail(ex.Message));
        }
    }

    [HttpDelete("Delete/{publicId:guid}")]
    public virtual async Task<IActionResult> Delete(Guid publicId)
    {
        try
        {
            await _repo.DeleteAsync(publicId);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["DeleteSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("ChangeStatus")]
    public virtual async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
    {
        try
        {
            await _repo.ChangeStatusAsync(request);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["ChangeStatusSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    protected async Task<IActionResult> ImportFromExcel(
        IFormFile file,
        Func<System.Data.DataRow, TRequest?> rowMapper)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"]));

        var requests = new List<TRequest>();
        using var stream = file.OpenReadStream();
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var result = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        if (result.Tables.Count > 0)
        {
            var table = result.Tables[0];
            if (!table.Columns.Contains("Name"))
                return BadRequest(ApiResponse<object>.Fail(Localizer["ColumnNameNotFound"]));

            foreach (System.Data.DataRow row in table.Rows)
            {
                var req = rowMapper(row);
                if (req != null) requests.Add(req);
            }
        }

        if (requests.Count == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["NoDataToImport"]));

        await _repo.AddRangeAsync(requests);
        return Ok(ApiResponse<object>.Ok(null!, string.Format(Localizer["ImportSuccess"], requests.Count)));
    }

    protected async Task<IActionResult> ImportFromExcel(
        string serverFilePath,
        Func<System.Data.DataRow, TRequest?> rowMapper)
    {
        if (!System.IO.File.Exists(serverFilePath))
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"]));

        try
        {
            var requests = new List<TRequest>();
            await using (var stream = System.IO.File.OpenRead(serverFilePath))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });

                if (result.Tables.Count > 0)
                {
                    var table = result.Tables[0];
                    if (!table.Columns.Contains("Name"))
                    {
                        DeleteServerFile(serverFilePath);
                        return BadRequest(ApiResponse<object>.Fail(Localizer["ColumnNameNotFound"]));
                    }

                    foreach (System.Data.DataRow row in table.Rows)
                    {
                        var req = rowMapper(row);
                        if (req != null) requests.Add(req);
                    }
                }
            }

            if (requests.Count == 0)
            {
                DeleteServerFile(serverFilePath);
                return BadRequest(ApiResponse<object>.Fail(Localizer["NoDataToImport"]));
            }

            await _repo.AddRangeAsync(requests);
            DeleteServerFile(serverFilePath);
            return Ok(ApiResponse<object>.Ok(null!, string.Format(Localizer["ImportSuccess"], requests.Count)));
        }
        catch (Exception ex)
        {
            DeleteServerFile(serverFilePath);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private static void DeleteServerFile(string path)
    {
        try { System.IO.File.Delete(path); } catch { /* ignore cleanup failure */ }
    }

    // ── Image Upload ──────────────────────────────────────────────────────────
    protected async Task<IActionResult> UploadImageAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"]));

        // Validate via magic bytes — never trust Content-Type header
        var header = new byte[12];
        int bytesRead;
        await using (var peek = file.OpenReadStream())
            bytesRead = await peek.ReadAsync(header.AsMemory());

        var ext = DetectImageExtension(header, bytesRead);
        if (ext == null)
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidImageType"]));

        // Resolve save path from config
        var config   = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var rootPath = config["FileSettings:RootPath"] ?? string.Empty;
        var subPath  = config["FileSettings:UploadPath"] ?? "Uploads/Images";
        var now      = DateTime.Now;
        var datePart = $"{now.Year}/{now.Month:D2}";
        var fullDir  = Path.Combine(rootPath, subPath.Replace('/', Path.DirectorySeparatorChar),
                                    now.Year.ToString(), now.Month.ToString("D2"));
        Directory.CreateDirectory(fullDir);

        // UUID filename — never use original name to prevent path traversal
        var fileName = $"{Guid.NewGuid():N}.{ext}";
        await using (var dest = new FileStream(Path.Combine(fullDir, fileName), FileMode.Create, FileAccess.Write))
            await file.CopyToAsync(dest);

        var relativePath = $"{subPath}/{datePart}/{fileName}";
        return Ok(ApiResponse<object>.Ok(new { path = relativePath }, Localizer["UploadSuccess"]));
    }

    // Detects image type from first 12 magic bytes — ignores Content-Type
    private static string? DetectImageExtension(byte[] h, int len)
    {
        if (len >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            return "jpg";
        if (len >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                     && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A)
        {
            return "png";
        }
        if (len >= 4 && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38)
            return "gif";
        if (len >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                      && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50)
        {
            return "webp";
        }
        return null;
    }
}
