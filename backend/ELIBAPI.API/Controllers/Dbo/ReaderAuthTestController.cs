using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

/// <summary>Kiểm tra cấu hình xác thực bạn đọc qua LDAP / API (port ELIB-LRC 10-04) bằng 1 tài khoản thử — không đăng nhập, không lưu
/// gì. K12: dùng cấu hình của đơn vị người thao tác (READER_AUTH_CONFIG, thiếu thì appsettings ReaderAuth); tài khoản hệ thống
/// (không thuộc đơn vị) kiểm tra cấu hình chung.</summary>
[Route("api/admin/reader-auth")]
public class ReaderAuthTestController(ExternalReaderAuth external) : BaseApiController
{
    [HttpGet]
    [Permission("READERS", "view")]
    public async Task<IActionResult> Status()
    {
        var s = await external.ForTenantAsync(GetTenantId());
        return Ok(ApiResponse<object>.Ok(new
        {
            provider = s.Provider, external = s.IsExternal, fallbackToLocal = s.FallbackToLocal,
            matchBy = s.MatchByEmail ? "Email" : "Cardno", source = s.Source, configKey = ExternalReaderAuth.TenantConfigKey,
        }));
    }

    [HttpPost("Test")]
    [Permission("READERS", "edit")]
    public async Task<IActionResult> Test([FromBody] ReaderAuthTestRequest request, CancellationToken ct)
    {
        var s = await external.ForTenantAsync(GetTenantId());
        if (!s.IsExternal) return BadRequest(ApiResponse<object>.Fail("Đang dùng xác thực nội bộ (Provider = Local)."));
        var r = await external.AuthenticateAsync(s, request?.LoginName ?? "", request?.Password ?? "", ct);
        return Ok(ApiResponse<object>.Ok(new { outcome = r.Outcome.ToString(), cardNo = r.CardNo, email = r.Email, message = r.Message }));
    }
}

public class ReaderAuthTestRequest
{
    public string? LoginName { get; set; }
    public string? Password  { get; set; }
}
