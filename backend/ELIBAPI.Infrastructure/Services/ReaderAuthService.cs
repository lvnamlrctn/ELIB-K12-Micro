using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ELIBAPI.Infrastructure.Services;

public class ReaderAuthService : IReaderAuthService
{
    private readonly ELIBAPIDbContext _db;
    private readonly IConfiguration _config;
    private readonly ISystemParameterService _sysParam;
    private readonly ICaptchaService _captcha;
    private readonly IOtpService _otp;
    private readonly IEmailService _email;
    private readonly ILogger<ReaderAuthService> _logger;
    private readonly ExternalReaderAuth _external;

    // Namespace cache riêng cho luồng bạn đọc — tách khỏi luồng admin dùng chung IOtpService.
    private const string OtpPurpose = "ReaderLogin";

    public ReaderAuthService(
        ELIBAPIDbContext db,
        IConfiguration config,
        ISystemParameterService sysParam,
        ICaptchaService captcha,
        IOtpService otp,
        IEmailService email,
        ILogger<ReaderAuthService> logger,
        ExternalReaderAuth external)
    {
        _external = external;
        _db = db;
        _config = config;
        _sysParam = sysParam;
        _captcha = captcha;
        _otp = otp;
        _email = email;
        _logger = logger;
    }

    public async Task<LoginResult<ReaderLoginResponse>> LoginAsync(ReaderLoginRequest request)
    {
        // Tenant nội bộ (long) được xác định sớm — cần cho cả kiểm tra CAPTCHA/OTP theo tenant lẫn kiểm
        // tra "tài khoản có thuộc đơn vị này" bên dưới. request.TenantId là PublicId (Guid) từ FE.
        long? tenantInternalId = null;
        if (request.TenantId != Guid.Empty)
        {
            tenantInternalId = await _db.Tenants
                .Where(t => t.PublicId == request.TenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();
        }

        // Lớp bảo mật 1 — CAPTCHA: kiểm tra TRƯỚC khi tra cứu bạn đọc, để một CAPTCHA sai không tiết lộ
        // việc số thẻ có tồn tại hay không. Nếu tenant không bật READER_LOGIN_CAPTCHA_ENABLED thì bỏ qua
        // hoàn toàn — hành vi giống hệt trước khi có tính năng này.
        if (await _sysParam.IsEnabledAsync("READER_LOGIN_CAPTCHA_ENABLED", tenantInternalId))
        {
            if (string.IsNullOrEmpty(request.CaptchaId) || !_captcha.Validate(request.CaptchaId, request.CaptchaAnswer))
                return new LoginResult<ReaderLoginResponse>(false, null, "Mã xác thực không đúng hoặc đã hết hạn", 400);
        }

        // Xác thực qua hệ thống ngoài (LDAP / API, port ELIB-LRC 10-04) khi đơn vị cấu hình READER_AUTH_CONFIG (hoặc appsettings
        // ReaderAuth). Thành công → khớp hồ sơ bạn đọc TRONG ĐƠN VỊ theo số thẻ (hoặc email) hệ thống ngoài trả về; sai mật khẩu /
        // không kết nối được → tuỳ FallbackToLocal mà thử mật khẩu nội bộ.
        Reader? reader = null;
        var externallyVerified = false;
        var auth = await _external.ForTenantAsync(tenantInternalId);
        if (auth.IsExternal)
        {
            var ext = await _external.AuthenticateAsync(auth, request.LoginName, request.Password);
            if (ext.Outcome == ExternalReaderAuth.Outcome.Success)
            {
                var key = (auth.MatchByEmail ? ext.Email : ext.CardNo)?.Trim().ToLower();
                if (!string.IsNullOrEmpty(key))
                    reader = await _db.Readers
                        .Where(u => u.IsDelete != 2 && (!tenantInternalId.HasValue || u.TenantId == tenantInternalId)
                            && (auth.MatchByEmail ? u.Email!.Trim().ToLower() == key : u.Cardno!.Trim().ToLower() == key))
                        .FirstOrDefaultAsync();
                if (reader == null)
                    return new LoginResult<ReaderLoginResponse>(false, null, "Tài khoản hợp lệ nhưng chưa có hồ sơ bạn đọc tại thư viện, vui lòng liên hệ thủ thư.", 403);
                externallyVerified = true;
            }
            else if (!auth.FallbackToLocal)
                return ext.Outcome == ExternalReaderAuth.Outcome.Unavailable
                    ? new LoginResult<ReaderLoginResponse>(false, null, "Hệ thống xác thực tạm thời không kết nối được, vui lòng thử lại sau.", 503)
                    : new LoginResult<ReaderLoginResponse>(false, null, "Tên đăng nhập hoặc mật khẩu không đúng", 401);
        }

        // Số thẻ chỉ duy nhất trong 1 đơn vị — biết đơn vị thì tra trong đơn vị đó (trước đây lấy dòng đầu của BẤT KỲ đơn vị nào rồi
        // báo "không thuộc đơn vị này" nếu trùng số thẻ với trường khác).
        reader ??= await _db.Readers
            .Where(u => u.Cardno!.Trim().ToLower() == request.LoginName.Trim().ToLower()
                && (!tenantInternalId.HasValue || u.TenantId == tenantInternalId))
            .OrderBy(u => u.IsDelete == 2)
            .FirstOrDefaultAsync();

        if (reader == null)
            return new LoginResult<ReaderLoginResponse>(false, null, "Tên đăng nhập không tồn tại", 401);

        if (reader.IsDelete == 2)
            return new LoginResult<ReaderLoginResponse>(false, null, "Tài khoản đã bị xóa", 403);

        if (reader.Status != 2)
            return new LoginResult<ReaderLoginResponse>(false, null, "Tài khoản đã bị khóa", 403);

        if (!externallyVerified && !PasswordHasher.Verify(request.Password, reader.Password))
            return new LoginResult<ReaderLoginResponse>(false, null, "Mật khẩu không đúng", 401);

        // Mật khẩu còn ở định dạng cũ (MD5 hex) → nâng cấp ngầm sang BCrypt ngay khi xác thực thành công,
        // không bắt bạn đọc đổi mật khẩu.
        if (!externallyVerified && !PasswordHasher.IsBCryptHash(reader.Password))
        {
            reader.Password = PasswordHasher.Hash(request.Password);
            await _db.SaveChangesAsync();
        }

        if (tenantInternalId.HasValue && reader.TenantId != tenantInternalId.Value)
            return new LoginResult<ReaderLoginResponse>(false, null, "Tài khoản không thuộc đơn vị này", 403);

        // Lớp bảo mật 2 — OTP qua email: chỉ kích hoạt SAU KHI mật khẩu đã xác thực đúng. Nếu tenant
        // không bật READER_LOGIN_OTP_ENABLED thì bỏ qua — hành vi giống hệt trước khi có tính năng này
        // (phát hành token ngay, như nhánh cuối hàm bên dưới).
        if (await _sysParam.IsEnabledAsync("READER_LOGIN_OTP_ENABLED", tenantInternalId))
        {
            if (string.IsNullOrWhiteSpace(reader.Email))
                return new LoginResult<ReaderLoginResponse>(false, null, "Tài khoản chưa có email đăng ký để nhận mã xác thực, vui lòng liên hệ thư viện", 400);

            var (otp, otpSessionToken) = _otp.GenerateAndCache(reader.Id, OtpPurpose);

            await _email.SendAsync(reader.Email!, "Mã xác thực đăng nhập ELIB",
                $"Mã xác thực đăng nhập của bạn là <b>{otp}</b>, hết hạn sau 5 phút. Vui lòng không chia sẻ mã này cho bất kỳ ai.");

            var pendingResponse = new ReaderLoginResponse(
                "", $"{reader.FirstName} {reader.LastName}", reader.Cardno!, reader.PublicId, reader.PublicId,
                null, DateTime.UtcNow, OtpRequired: true, OtpSessionToken: otpSessionToken);
            return new LoginResult<ReaderLoginResponse>(true, pendingResponse, null);
        }

        var response = await BuildLoginResponseAsync(reader);
        return new LoginResult<ReaderLoginResponse>(true, response, null);
    }

    public async Task<LoginResult<ReaderLoginResponse>> VerifyOtpAsync(VerifyOtpRequest request)
    {
        var result = _otp.Verify(request.OtpSessionToken, request.Code, OtpPurpose);
        if (!result.Success)
            return new LoginResult<ReaderLoginResponse>(false, null, result.Error, 400);

        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.Id == result.SubjectId);
        if (reader == null || reader.IsDelete == 2 || reader.Status != 2)
            return new LoginResult<ReaderLoginResponse>(false, null, "Tài khoản không hợp lệ", 403);

        var response = await BuildLoginResponseAsync(reader);
        return new LoginResult<ReaderLoginResponse>(true, response, null);
    }

    // Dùng chung bởi cả nhánh đăng nhập trực tiếp (không cần OTP) lẫn nhánh xác thực OTP thành công —
    // đảm bảo chỉ có 1 nơi phát hành token thật, tránh trùng lặp logic.
    private async Task<ReaderLoginResponse> BuildLoginResponseAsync(Reader reader)
    {
        var expireMinutes = int.TryParse(_config["Jwt:ExpireMinutes"], out var m) ? m : 60;
        var expiration = DateTime.UtcNow.AddMinutes(expireMinutes);
        var token = GenerateToken(reader.PublicId, reader.Cardno!, $"{reader.FirstName} {reader.LastName}", reader.TenantId, expiration);

        Guid? tenantPublicId = reader.TenantId.HasValue
            ? await _db.Tenants.Where(t => t.Id == reader.TenantId.Value)
                               .Select(t => (Guid?)t.PublicId).FirstOrDefaultAsync()
            : null;

        return new ReaderLoginResponse(token, $"{reader.FirstName} {reader.LastName}", reader.Cardno!, reader.PublicId, reader.PublicId, tenantPublicId, expiration);
    }

    private string GenerateToken(Guid userId, string loginName, string? fullName, long? tenantId, DateTime expiration)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name,           loginName),
            new("FullName",                fullName ?? string.Empty),
            new("Type",                    "Reader"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (tenantId.HasValue)
            claims.Add(new Claim("TenantId", tenantId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

}
