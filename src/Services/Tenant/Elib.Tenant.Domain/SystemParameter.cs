using Elib.BuildingBlocks.Domain;

namespace Elib.Tenant.Domain;

/// <summary>
/// Tham số hệ thống của đơn vị (monolith: dbo.systemparameter). Mã chuẩn hoá chữ hoa, duy nhất trong đơn vị
/// (monolith so khớp không phân biệt hoa thường). Tham số khai báo trong <see cref="ParameterCatalog"/> thì
/// <see cref="Service"/> và <see cref="IsPublic"/> do danh mục quyết định — đơn vị không tự mở công khai một tham số bí mật.
/// </summary>
public sealed class SystemParameter : TenantEntity
{
    private SystemParameter() { }

    public string Code { get; private set; } = "";
    public string? Value { get; private set; }
    public string? Description { get; private set; }
    public string? DescriptionEn { get; private set; }

    /// <summary>Kiểu giá trị để giao diện hiển thị ô nhập phù hợp: text, bool, number, json…</summary>
    public string? Type { get; private set; }

    /// <summary>Service đọc tham số này — dùng cho event <c>SystemParameterChanged</c> và cache theo service.</summary>
    public string? Service { get; private set; }

    /// <summary>OPAC đọc được khi chưa đăng nhập (tên thư viện, địa chỉ, giờ mở cửa…).</summary>
    public bool IsPublic { get; private set; }

    public static SystemParameter Create(string code, string? value, string? description, string? descriptionEn, string? type, string? service, bool isPublic)
    {
        var parameter = new SystemParameter { Code = NormalizeCode(code) };
        parameter.Update(value, description, descriptionEn, type, service, isPublic);
        return parameter;
    }

    public static SystemParameter FromDefinition(ParameterDefinition d) =>
        Create(d.Code, d.DefaultValue, d.Description, null, d.Type, d.Service, d.IsPublic);

    public void Update(string? value, string? description, string? descriptionEn, string? type, string? service, bool isPublic)
    {
        Value = Limit(value, 4000, "Giá trị");
        Description = Limit(description, 350, "Mô tả");
        DescriptionEn = Limit(descriptionEn, 350, "Mô tả tiếng Anh");
        Type = Limit(type, 50, "Kiểu");

        if (ParameterCatalog.Find(Code) is { } definition)
        {
            Service = definition.Service;
            IsPublic = definition.IsPublic;
        }
        else
        {
            Service = Limit(service, 50, "Service")?.ToLowerInvariant();
            IsPublic = isPublic;
        }
    }

    public static string NormalizeCode(string code)
    {
        var value = (code ?? "").Trim().ToUpperInvariant();
        return value.Length is > 0 and <= 100 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-')
            ? value
            : throw new BusinessRuleException("PARAMETER_CODE_INVALID", "Mã tham số gồm 1–100 ký tự A-Z, 0-9, '_', '.', '-'.");
    }

    private static string? Limit(string? value, int max, string label)
    {
        if (value is null) return null;
        return value.Length <= max ? value : throw new BusinessRuleException("PARAMETER_FIELD_TOO_LONG", $"{label} tối đa {max} ký tự.");
    }
}

public sealed record ParameterDefinition(string Code, string? DefaultValue, string Description, string Service, bool IsPublic = false, string Type = "text");

/// <summary>
/// Tham số mà mã nguồn đọc tới — có giá trị mặc định khi đơn vị chưa khai báo, được chép cho đơn vị mới.
/// Mỗi service khi port sang thêm tham số của mình vào đây (theo lộ trình), không seed bằng SQL tay như monolith.
/// </summary>
public static class ParameterCatalog
{
    public static IReadOnlyList<ParameterDefinition> All { get; } =
    [
        new("LIBRARYNAME", "", "Tên thư viện hiển thị trên OPAC", "portal", IsPublic: true),
        new("LIBRARY_ADDR", "", "Địa chỉ thư viện", "portal", IsPublic: true),
        new("LIBRARY_TEL", "", "Điện thoại thư viện", "portal", IsPublic: true),
        new("LIBRARY_EMAIL", "", "Email thư viện", "portal", IsPublic: true),
        new("OPENHOUR", "", "Giờ mở cửa", "portal", IsPublic: true),
        new("ADMIN_LOGIN_CAPTCHA_ENABLED", "0", "Yêu cầu CAPTCHA khi nhân viên đăng nhập. \"1\" = bật, \"0\" = tắt.", "identity", Type: "bool"),
        new("ADMIN_LOGIN_OTP_ENABLED", "0", "Yêu cầu mã OTP qua email khi nhân viên đăng nhập. \"1\" = bật, \"0\" = tắt.", "identity", Type: "bool"),
        new("READER_LOGIN_CAPTCHA_ENABLED", "0", "Yêu cầu CAPTCHA khi bạn đọc đăng nhập OPAC. \"1\" = bật, \"0\" = tắt.", "identity", IsPublic: true, Type: "bool"),
        new("READER_LOGIN_OTP_ENABLED", "0", "Yêu cầu mã OTP qua email khi bạn đọc đăng nhập OPAC. \"1\" = bật, \"0\" = tắt.", "identity", IsPublic: true, Type: "bool"),
        new("READER_AUTH_CONFIG", "", "Xác thực bạn đọc qua LDAP/API ngoài (JSON cùng cấu trúc mục ReaderAuth). Chứa mật khẩu dịch vụ — không bao giờ công khai.", "identity", Type: "json"),
    ];

    public static ParameterDefinition? Find(string code)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        return All.FirstOrDefault(d => d.Code == normalized);
    }
}
