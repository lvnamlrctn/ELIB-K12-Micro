using System.Security.Claims;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers;

// [AllowAnonymous] cố ý KHÔNG đặt ở mức class: theo cách ASP.NET Core resolve authorization,
// [AllowAnonymous] ở class sẽ vô hiệu hoá MỌI [Authorize] đặt trên action bên trong (không phải
// action ghi đè được class) — nên phải đặt [AllowAnonymous] riêng cho từng action công khai
// (Login), để [Authorize] của Preference thật sự chặn được người chưa đăng nhập.
[ApiController]
[Route("api/public/[controller]")]
public class PublicReaderController(IReaderAuthService auth, ELIBAPIDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("Login")]
    [EnableRateLimiting(RateLimitingExtensions.ReaderLoginPolicy)]
    public async Task<IActionResult> Login([FromBody] ReaderLoginRequest request)
    {
        var result = await auth.LoginAsync(request);
        if (!result.Success)
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.ErrorMessage!, result.StatusCode));
        return Ok(ApiResponse<ReaderLoginResponse>.Ok(result.Data!));
    }

    /// Phát hành 1 thử thách CAPTCHA (lớp bảo mật đăng nhập bạn đọc thứ 1). tenantId là PublicId (Guid)
    /// của tenant — dùng để đọc cờ READER_LOGIN_CAPTCHA_ENABLED theo đúng tenant (đa tenant, không dùng
    /// chung 1 cờ toàn hệ thống như bản gốc). enabled=false khi tenant chưa bật cờ này — FE khi đó không
    /// hiển thị ô CAPTCHA và không cần gửi CaptchaId/CaptchaAnswer khi Login.
    [AllowAnonymous]
    [HttpGet("Captcha")]
    public async Task<IActionResult> GetCaptcha(
        [FromQuery] Guid? tenantId,
        [FromServices] ISystemParameterService sysParam,
        [FromServices] ICaptchaService captcha)
    {
        long? tenantInternalId = null;
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            tenantInternalId = await db.Tenants
                .Where(t => t.PublicId == tenantId.Value && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();
        }

        if (!await sysParam.IsEnabledAsync("READER_LOGIN_CAPTCHA_ENABLED", tenantInternalId))
            return Ok(ApiResponse<object>.Ok(new { enabled = false }));

        var (id, svg) = captcha.Generate();
        return Ok(ApiResponse<object>.Ok(new { enabled = true, captchaId = id, svg }));
    }

    /// Xác thực OTP (lớp bảo mật đăng nhập bạn đọc thứ 2) — gọi sau khi Login trả về OtpRequired=true.
    /// Thành công thì trả về ReaderLoginResponse có Token thật, y hệt Login khi không cần OTP.
    [AllowAnonymous]
    [HttpPost("VerifyOtp")]
    [EnableRateLimiting(RateLimitingExtensions.OtpPolicy)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var result = await auth.VerifyOtpAsync(request);
        if (!result.Success)
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.ErrorMessage!, result.StatusCode));
        return Ok(ApiResponse<ReaderLoginResponse>.Ok(result.Data!));
    }

    /// <summary>Hồ sơ thẻ bạn đọc (Đợt 22.6 — thẻ bạn đọc điện tử offline) — Login/VerifyOtp không trả
    /// <see cref="Core.Entities.Dbo.Reader.ExpireDate"/> (chỉ có hạn JWT, khác hạn thẻ thật), nên trang thẻ
    /// gọi endpoint riêng này để làm mới mỗi lần mở trang; lỗi/mất mạng thì FE vẫn hiện được bản đã lưu
    /// cache (localStorage) từ lần gọi thành công gần nhất — xem opac/services/auth.service.ts.</summary>
    [Authorize]
    [HttpGet("Profile")]
    public async Task<IActionResult> GetProfile()
    {
        var readerId = await ResolveReaderIdAsync();
        if (readerId == null)
            return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var reader = await db.Readers.AsNoTracking().Where(r => r.Id == readerId.Value)
            .Select(r => new { r.Cardno, r.FirstName, r.LastName, r.IssueDate, r.ExpireDate }).FirstOrDefaultAsync();
        if (reader == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy bạn đọc.", 404));

        return Ok(ApiResponse<ReaderCardProfileResponse>.Ok(new ReaderCardProfileResponse(
            reader.Cardno ?? "", $"{reader.FirstName} {reader.LastName}".Trim(), reader.IssueDate, reader.ExpireDate)));
    }

    /// <summary>Chủ đề/môn học bạn đọc quan tâm — dùng để chatbot gợi ý tài liệu theo hồ sơ.</summary>
    [Authorize]
    [HttpGet("Preference")]
    public async Task<IActionResult> GetPreference()
    {
        var readerId = await ResolveReaderIdAsync();
        if (readerId == null)
            return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var pref = await db.ReaderPreferences
            .Where(p => p.ReaderId == readerId.Value)
            .Select(p => new { p.SubjectId, p.TopicId })
            .FirstOrDefaultAsync();

        string? subjectName = null, topicName = null;
        if (pref?.SubjectId != null)
            subjectName = await db.EbookSubjects.Where(s => s.Id == pref.SubjectId).Select(s => s.Name).FirstOrDefaultAsync();
        if (pref?.TopicId != null)
            topicName = await db.EbookTopics.Where(t => t.Id == pref.TopicId).Select(t => t.Name).FirstOrDefaultAsync();

        return Ok(ApiResponse<ReaderPreferenceResponse>.Ok(new ReaderPreferenceResponse(
            pref?.SubjectId, subjectName, pref?.TopicId, topicName)));
    }

    [Authorize]
    [HttpPut("Preference")]
    public async Task<IActionResult> UpdatePreference([FromBody] ReaderPreferenceRequest request)
    {
        var readerId = await ResolveReaderIdAsync();
        if (readerId == null)
            return Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

        var pref = await db.ReaderPreferences.FirstOrDefaultAsync(p => p.ReaderId == readerId.Value);
        if (pref == null)
        {
            var tenantId = await db.Readers.Where(r => r.Id == readerId.Value).Select(r => r.TenantId).FirstOrDefaultAsync();
            pref = new Core.Entities.Dbo.ReaderPreference { ReaderId = readerId.Value, TenantId = tenantId };
            db.ReaderPreferences.Add(pref);
        }
        pref.SubjectId = request.SubjectId;
        pref.TopicId = request.TopicId;
        pref.UpdatedRowDate = DateTime.Now;
        await db.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("OK"));
    }

    // Cùng cách MyLibraryController xác định bạn đọc: NameIdentifier = Reader.PublicId (Guid),
    // phân biệt với nhân viên qua claim "Type" == "Reader".
    private async Task<long?> ResolveReaderIdAsync()
    {
        if (User.FindFirstValue("Type") != "Reader") return null;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var publicId)) return null;
        return await db.Readers
            .Where(r => r.PublicId == publicId && r.IsDelete != 2)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync();
    }
}

public record ReaderPreferenceResponse(long? SubjectId, string? SubjectName, long? TopicId, string? TopicName);
public record ReaderCardProfileResponse(string Cardno, string FullName, DateTime? IssueDate, DateTime? ExpireDate);
public class ReaderPreferenceRequest
{
    public long? SubjectId { get; set; }
    public long? TopicId { get; set; }
}
