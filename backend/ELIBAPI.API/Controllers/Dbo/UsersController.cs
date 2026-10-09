using System.Security.Claims;
using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class UsersController : GenericController<Users, UsersSearchRequest, UsersRequest>
{
    private readonly IUsersRepository _usersRepo;
    private readonly IMinioService    _minio;

    public UsersController(IUsersRepository repo, IMinioService minio) : base(repo)
    {
        _usersRepo = repo;
        _minio     = minio;
    }

    [HttpPost("Add")]    [Permission("USERS", "add")]
    public override Task<IActionResult> Add([FromBody] UsersRequest request) => base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("USERS", "edit")]
    public override Task<IActionResult> Update(Guid publicId, [FromBody] UsersRequest request) => base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("USERS", "delete")]
    public override Task<IActionResult> Delete(Guid publicId) => base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("USERS", "edit")]
    public override Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("USERS", "view")]
    public override Task<IActionResult> GetById(long id) => base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("USERS", "view")]
    public override Task<IActionResult> GetByPublicId(Guid publicId) => base.GetByPublicId(publicId);

    [HttpGet("CheckExist")]
    [Permission("USERS", "view")]
    public async Task<IActionResult> CheckExist([FromQuery] string loginName, [FromQuery] Guid? excludePublicId = null)
    {
        if (string.IsNullOrWhiteSpace(loginName))
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidRequest"], 400));
        var exists = await _usersRepo.CheckLoginNameExistsAsync(loginName, excludePublicId);
        return Ok(ApiResponse<bool>.Ok(exists));
    }

    [HttpPost("Search")]
    [Permission("USERS", "view")]
    public override Task<IActionResult> Search([FromBody] UsersSearchRequest request) => base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("USERS", "view")]
    public override Task<IActionResult> SearchAll([FromBody] UsersSearchRequest request) => base.SearchAll(request);

    // ── MyPermission ──────────────────────────────────────────────────────────

    [HttpGet("MyPermission")]
    public async Task<IActionResult> MyPermission()
    {
        var raw = User.FindFirstValue("PublicId");
        if (!Guid.TryParse(raw, out var publicId))
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized", 401));
        var list = await _usersRepo.GetPermissionsAsync(publicId);
        return Ok(ApiResponse<List<UserPermissionResponse>>.Ok(list));
    }

    // ── GetPermission ─────────────────────────────────────────────────────────

    [HttpGet("GetPermission/{publicId:guid}")]
    [Permission("USERS", "view")]
    public async Task<IActionResult> GetPermission(Guid publicId)
    {
        try
        {
            var result = await _usersRepo.GetPermissionsAsync(publicId);
            return Ok(ApiResponse<List<UserPermissionResponse>>.Ok(result));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    // ── SavePermission ────────────────────────────────────────────────────────

    [HttpPost("SavePermission")]
    [Permission("USERS", "edit")]
    public async Task<IActionResult> SavePermission([FromBody] SavePermissionRequest request)
    {
        try
        {
            var editorId = GetCurrentUserId();
            await _usersRepo.SavePermissionsAsync(request, editorId);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    // ── UploadPhoto ───────────────────────────────────────────────────────────

    [HttpPost("UploadPhoto")]
    [Permission("USERS", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhoto(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidFile"], 400));

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var header = ms.ToArray()[..Math.Min(12, (int)ms.Length)];
        var ext    = DetectImageExt(header);
        if (ext == null)
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidFile"], 400));

        ms.Seek(0, SeekOrigin.Begin);
        var url = await _minio.UploadAsync(ms, $"img.{ext}", file.ContentType);
        return Ok(ApiResponse<object>.Ok(new { path = url }, Localizer["UploadSuccess"]));
    }

    // ── ChangePassword (user tự đổi mật khẩu) ─────────────────────────────────

    [HttpPut("ChangePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu cũ và mới không được trống"));

        var userId = GetCurrentUserId();

        try
        {
            await _usersRepo.ChangePasswordAsync(userId, request.OldPassword, request.NewPassword);
            return Ok(ApiResponse<object>.Ok(null!, "Đổi mật khẩu thành công"));
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu cũ không đúng"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    // ── ResetPassword ─────────────────────────────────────────────────────────

    [HttpPut("ResetPassword/{publicId:guid}")]
    [Permission("USERS", "edit")]
    public async Task<IActionResult> ResetPassword(Guid publicId, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu không được trống"));

        var hashed  = PasswordHasher.Hash(request.Password);
        var userId  = GetCurrentUserId();

        try
        {
            await _usersRepo.ResetPasswordAsync(publicId, hashed, userId);
            return Ok(ApiResponse<object>.Ok(null!, "Đặt lại mật khẩu thành công"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    // ── ExportExcel ───────────────────────────────────────────────────────────

    [HttpPost("ExportExcel")]
    [Permission("USERS", "view")]
    public async Task<IActionResult> ExportExcel([FromBody] UsersSearchRequest request, [FromServices] ISystemParameterService sysParam)
    {
        var users = await _repo.SearchAllAsync(request);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Tài khoản người dùng");

        var headers = new[] { "STT", "Họ tên", "Tên đăng nhập", "Email", "Điện thoại", "Địa chỉ", "Vai trò", "Trạng thái" };
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH TÀI KHOẢN NGƯỜI DÙNG", headers.Length);
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(startRow, i + 1).Value = headers[i];

        var headerRow = ws.Row(startRow);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

        for (int r = 0; r < users.Count; r++)
        {
            var u = users[r];
            ws.Cell(startRow + 1 + r, 1).Value = r + 1;
            ws.Cell(startRow + 1 + r, 2).Value = u.FullName;
            ws.Cell(startRow + 1 + r, 3).Value = u.LoginName;
            ws.Cell(startRow + 1 + r, 4).Value = u.Email;
            ws.Cell(startRow + 1 + r, 5).Value = u.Phone;
            ws.Cell(startRow + 1 + r, 6).Value = u.Address;
            ws.Cell(startRow + 1 + r, 7).Value = u.RoleId;
            ws.Cell(startRow + 1 + r, 8).Value = u.Status == 2 ? "Hoạt động" : "Ẩn";
        }

        ExcelReportHelper.ApplyTableBorders(ws, startRow, startRow + users.Count, 1, headers.Length);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"danh-sach-tai-khoan-{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    // ── Import ────────────────────────────────────────────────────────────────

    [HttpPost("Import")]
    [Permission("USERS", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"]));

        var requests = new List<UsersRequest>();

        await using var stream = file.OpenReadStream();
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        if (dataSet.Tables.Count > 0)
        {
            var table = dataSet.Tables[0];
            foreach (System.Data.DataRow row in table.Rows)
            {
                var loginName = row.Table.Columns.Contains("LoginName")
                    ? row["LoginName"]?.ToString()?.Trim() : null;
                if (string.IsNullOrEmpty(loginName)) continue;

                requests.Add(new UsersRequest
                {
                    FullName  = row.Table.Columns.Contains("FullName")  ? row["FullName"]?.ToString()?.Trim()  : null,
                    LoginName = loginName,
                    Password  = row.Table.Columns.Contains("Password")  ? row["Password"]?.ToString()?.Trim()  : "123456",
                    Email     = row.Table.Columns.Contains("Email")     ? row["Email"]?.ToString()?.Trim()     : null,
                    Phone     = row.Table.Columns.Contains("Phone")     ? row["Phone"]?.ToString()?.Trim()     : null,
                    Address   = row.Table.Columns.Contains("Address")   ? row["Address"]?.ToString()?.Trim()   : null,
                    PortalId  = row.Table.Columns.Contains("PortalId")  ? row["PortalId"]?.ToString()?.Trim()  : null,
                    Language  = row.Table.Columns.Contains("Language")  ? row["Language"]?.ToString()?.Trim()  : null,
                    RoleId    = row.Table.Columns.Contains("RoleId")    && int.TryParse(row["RoleId"]?.ToString(), out var rid) ? rid : null,
                    Status    = row.Table.Columns.Contains("Status")    && int.TryParse(row["Status"]?.ToString(), out var st)  ? st  : 1,
                });
            }
        }

        if (requests.Count == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["NoDataToImport"]));

        await _repo.AddRangeAsync(requests);
        return Ok(ApiResponse<object>.Ok(null!, string.Format(Localizer["ImportSuccess"], requests.Count)));
    }

    private static string? DetectImageExt(byte[] h) =>
        h.Length >= 3  && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF ? "jpg" :
        h.Length >= 8  && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 ? "png" :
        h.Length >= 4  && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 ? "gif" :
        h.Length >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                       && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50 ? "webp" : null;
}
