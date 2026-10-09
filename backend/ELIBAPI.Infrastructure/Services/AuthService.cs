using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Microsoft.IdentityModel.Tokens;

namespace ELIBAPI.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ELIBAPIDbContext _db;
    private readonly IConfiguration   _config;
    private readonly ISystemParameterService _sysParam;
    private readonly ICaptchaService _captcha;
    private readonly IOtpService _otp;
    private readonly IEmailService _email;
    private readonly IPermissionService _permissions;

    // Namespace cache riêng cho luồng admin — tách khỏi luồng bạn đọc dùng chung IOtpService.
    private const string OtpPurpose = "AdminLogin";

    public AuthService(
        ELIBAPIDbContext db,
        IConfiguration config,
        ISystemParameterService sysParam,
        ICaptchaService captcha,
        IOtpService otp,
        IEmailService email,
        IPermissionService permissions)
    {
        _permissions = permissions;
        _db     = db;
        _config = config;
        _sysParam = sysParam;
        _captcha  = captcha;
        _otp      = otp;
        _email    = email;
    }

    public async Task<LoginResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        // Tenant nội bộ chỉ dùng để đọc cờ CAPTCHA theo tenant (giống luồng bạn đọc) — KHÔNG thay đổi
        // luồng xác định/so khớp tenant hiện có bên dưới (dùng cho kiểm tra "tài khoản thuộc đơn vị này"
        // sau khi xác thực mật khẩu, vốn có thêm cơ chế isPrivileged bỏ qua kiểm tra).
        long? tenantInternalIdForFlags = null;
        if (request.TenantId.HasValue && request.TenantId != Guid.Empty)
        {
            tenantInternalIdForFlags = await _db.Tenants
                .Where(t => t.PublicId == request.TenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();
        }

        // Lớp bảo mật 1 — CAPTCHA: kiểm tra TRƯỚC khi tra cứu tài khoản, để một CAPTCHA sai không tiết lộ
        // việc tài khoản có tồn tại hay không. Nếu tenant không bật ADMIN_LOGIN_CAPTCHA_ENABLED thì bỏ
        // qua hoàn toàn — hành vi giống hệt trước khi có tính năng này.
        if (await _sysParam.IsEnabledAsync("ADMIN_LOGIN_CAPTCHA_ENABLED", tenantInternalIdForFlags))
        {
            if (string.IsNullOrEmpty(request.CaptchaId) || !_captcha.Validate(request.CaptchaId, request.CaptchaAnswer))
                return new LoginResult<LoginResponse>(false, null, "Mã xác thực không đúng hoặc đã hết hạn", 400);
        }

        var user = await _db.Users
            .Where(u => u.LoginName!.Trim().ToLower() == request.LoginName.Trim().ToLower())
            .FirstOrDefaultAsync();

        if (user == null)
            return new LoginResult<LoginResponse>(false, null, "Tên đăng nhập không tồn tại", 401);

        if (user.IsDelete == 2)
            return new LoginResult<LoginResponse>(false, null, "Tài khoản đã bị xóa", 403);

        if (user.Status != 2)
            return new LoginResult<LoginResponse>(false, null, "Tài khoản đã bị khóa", 403);

        if (!PasswordHasher.Verify(request.Password, user.Password))
            return new LoginResult<LoginResponse>(false, null, "Mật khẩu không đúng", 401);

        // Mật khẩu còn ở định dạng cũ (MD5 hex) → nâng cấp ngầm sang BCrypt ngay khi xác thực thành công,
        // không bắt người dùng đổi mật khẩu.
        if (!PasswordHasher.IsBCryptHash(user.Password))
        {
            user.Password = PasswordHasher.Hash(request.Password);
            await _db.SaveChangesAsync();
        }

        var (tenantCode, roleCode, isPrivileged) = await ResolveRoleContextAsync(user);

        if (!isPrivileged && request.TenantId.HasValue && request.TenantId != Guid.Empty)
        {
            var requestTenantId = await _db.Tenants
                .Where(t => t.PublicId == request.TenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id).FirstOrDefaultAsync();
            if (requestTenantId.HasValue && user.TenantId != requestTenantId)
                return new LoginResult<LoginResponse>(false, null, "Tài khoản không thuộc đơn vị này", 403);
        }

        // Lớp bảo mật 2 — OTP qua email: chỉ kích hoạt SAU KHI mật khẩu đã xác thực đúng. Nếu tenant
        // không bật ADMIN_LOGIN_OTP_ENABLED thì bỏ qua — hành vi giống hệt trước khi có tính năng này
        // (phát hành token ngay, như nhánh cuối hàm bên dưới).
        if (await _sysParam.IsEnabledAsync("ADMIN_LOGIN_OTP_ENABLED", user.TenantId))
        {
            if (string.IsNullOrWhiteSpace(user.Email))
                return new LoginResult<LoginResponse>(false, null, "Tài khoản chưa có email đăng ký để nhận mã xác thực, vui lòng liên hệ quản trị viên", 400);

            var (otp, otpSessionToken) = _otp.GenerateAndCache(user.Id, OtpPurpose);

            await _email.SendAsync(user.Email!, "Mã xác thực đăng nhập ELIB",
                $"Mã xác thực đăng nhập của bạn là <b>{otp}</b>, hết hạn sau 5 phút. Vui lòng không chia sẻ mã này cho bất kỳ ai.");

            var pendingResponse = new LoginResponse(
                "", user.FullName ?? string.Empty, user.LoginName!, user.Id, user.PublicId, user.TenantId, user.RoleId,
                isPrivileged, DateTime.UtcNow, OtpRequired: true, OtpSessionToken: otpSessionToken);
            return new LoginResult<LoginResponse>(true, pendingResponse, null);
        }

        var response = await BuildLoginResponseAsync(user, tenantCode, roleCode, isPrivileged);
        return new LoginResult<LoginResponse>(true, response, null);
    }

    public async Task<LoginResult<LoginResponse>> VerifyOtpAsync(VerifyOtpRequest request)
    {
        var result = _otp.Verify(request.OtpSessionToken, request.Code, OtpPurpose);
        if (!result.Success)
            return new LoginResult<LoginResponse>(false, null, result.Error, 400);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == result.SubjectId);
        if (user == null || user.IsDelete == 2 || user.Status != 2)
            return new LoginResult<LoginResponse>(false, null, "Tài khoản không hợp lệ", 403);

        var (tenantCode, roleCode, isPrivileged) = await ResolveRoleContextAsync(user);
        var response = await BuildLoginResponseAsync(user, tenantCode, roleCode, isPrivileged);
        return new LoginResult<LoginResponse>(true, response, null);
    }

    // Dùng chung bởi cả LoginAsync (tính isPrivileged trước khi kiểm tra tenant) lẫn VerifyOtpAsync
    // (không giữ lại context này qua vòng OTP nên phải tính lại) — tránh trùng lặp logic đọc RoleCodes.
    private async Task<(string? TenantCode, string? RoleCode, bool IsPrivileged)> ResolveRoleContextAsync(Users user)
    {
        string? tenantCode = null;
        string? roleCode   = null;
        if (user.TenantId.HasValue)
            tenantCode = await _db.Tenants
                .Where(t => t.Id == user.TenantId && t.IsDelete != 2)
                .Select(t => t.Code).FirstOrDefaultAsync();
        if (user.RoleId.HasValue)
            roleCode = await _db.Roles
                .Where(r => r.Id == (long?)user.RoleId && r.IsDelete != 2)
                .Select(r => r.Code).FirstOrDefaultAsync();

        var adminRoleCodes = _config.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        var roleCodes      = _config.GetSection("ReadOnlyPolicy:RoleCodes").Get<string[]>() ?? [];
        var tenantCodes    = _config.GetSection("ReadOnlyPolicy:TenantCodes").Get<string[]>() ?? [];
        var isPrivileged   = (roleCode != null && adminRoleCodes.Concat(roleCodes).Contains(roleCode, StringComparer.OrdinalIgnoreCase))
                          || (tenantCode != null && tenantCodes.Contains(tenantCode, StringComparer.OrdinalIgnoreCase));

        return (tenantCode, roleCode, isPrivileged);
    }

    // Dùng chung bởi nhánh đăng nhập trực tiếp (không cần OTP) lẫn nhánh xác thực OTP thành công —
    // đảm bảo chỉ có 1 nơi phát hành token thật, tránh trùng lặp logic (mirror ReaderAuthService).
    private async Task<LoginResponse> BuildLoginResponseAsync(Users user, string? tenantCode, string? roleCode, bool isPrivileged)
    {
        var expireMinutes = int.TryParse(_config["Jwt:ExpireMinutes"], out var m) ? m : 60;
        var expiration    = DateTime.UtcNow.AddMinutes(expireMinutes);

        // Giai đoạn 2 RBAC (port ELIB-LRC 09-27): nhúng kết quả phân quyền + PermissionStamp vào JWT để filter Permission
        // quyết định từ claim (1 query khoá chính thay cho 3–5 query có join) khi claim còn khớp stamp hiện tại của user.
        if (string.IsNullOrEmpty(user.PermissionStamp))
        {
            user.PermissionStamp = Guid.NewGuid().ToString();
            await _db.SaveChangesAsync();
        }
        string permClaim;
        if (await _permissions.IsAdminRoleAsync(user.Id))
            permClaim = "*";
        else
        {
            var map = await _permissions.GetUserPermissionsAsync(user.Id);
            permClaim = string.Join(';', map.Select(kv => $"{kv.Key}:{string.Join(',', kv.Value)}"));
        }
        var isReadOnly = await _permissions.IsReadOnlyUserAsync(user.Id);

        var token       = GenerateToken(user.Id, user.PublicId, user.LoginName!, user.FullName, user.TenantId, tenantCode, roleCode, expiration,
            permClaim, user.PermissionStamp, isReadOnly);

        return new LoginResponse(token, user.FullName ?? string.Empty, user.LoginName!, user.Id, user.PublicId, user.TenantId, user.RoleId, isPrivileged, expiration);
    }

    private string GenerateToken(long userId, Guid publicId, string loginName, string? fullName, long? tenantId,
        string? tenantCode, string? roleCode, DateTime expiration, string permClaim, string permissionStamp, bool isReadOnly)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("PublicId",                publicId.ToString()),
            new(ClaimTypes.Name,           loginName),
            new("FullName",                fullName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("perm",                    permClaim),
            new("pstamp",                  permissionStamp)
        };

        if (isReadOnly)          claims.Add(new Claim("ro", "1"));
        if (tenantId.HasValue)   claims.Add(new Claim("TenantId",   tenantId.Value.ToString()));
        if (tenantCode != null)  claims.Add(new Claim("TenantCode", tenantCode));
        if (roleCode   != null)  claims.Add(new Claim("RoleCode",   roleCode));

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expiration,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
