using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ELIBAPIDbContext _db;

    public AuthController(IAuthService auth, ELIBAPIDbContext db)
    {
        _auth = auth;
        _db   = db;
    }

    [HttpPost("Login")]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        if (!result.Success)
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.ErrorMessage!, result.StatusCode));
        return Ok(ApiResponse<LoginResponse>.Ok(result.Data!));
    }

    /// Phát hành 1 thử thách CAPTCHA (lớp bảo mật đăng nhập admin thứ 1) — mirror
    /// PublicReaderController.GetCaptcha. tenantId là PublicId (Guid) của tenant — dùng để đọc cờ
    /// ADMIN_LOGIN_CAPTCHA_ENABLED theo đúng tenant. enabled=false khi tenant chưa bật cờ này — FE khi đó
    /// không hiển thị ô CAPTCHA và không cần gửi CaptchaId/CaptchaAnswer khi Login.
    [HttpGet("Captcha")]
    public async Task<IActionResult> GetCaptcha(
        [FromQuery] Guid? tenantId,
        [FromServices] ISystemParameterService sysParam,
        [FromServices] ICaptchaService captcha)
    {
        long? tenantInternalId = null;
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            tenantInternalId = await _db.Tenants
                .Where(t => t.PublicId == tenantId.Value && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();
        }

        if (!await sysParam.IsEnabledAsync("ADMIN_LOGIN_CAPTCHA_ENABLED", tenantInternalId))
            return Ok(ApiResponse<object>.Ok(new { enabled = false }));

        var (id, svg) = captcha.Generate();
        return Ok(ApiResponse<object>.Ok(new { enabled = true, captchaId = id, svg }));
    }

    /// Xác thực OTP (lớp bảo mật đăng nhập admin thứ 2) — gọi sau khi Login trả về OtpRequired=true.
    /// Thành công thì trả về LoginResponse có Token thật, y hệt Login khi không cần OTP. Mirror
    /// PublicReaderController.VerifyOtp.
    [HttpPost("VerifyOtp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var result = await _auth.VerifyOtpAsync(request);
        if (!result.Success)
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.ErrorMessage!, result.StatusCode));
        return Ok(ApiResponse<LoginResponse>.Ok(result.Data!));
    }
}
