using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Notification.Domain;

/// <summary>
/// Mẫu email của đơn vị, tra theo <see cref="Code"/> (mã do service nghiệp vụ gửi trong NotificationRequested).
/// Đơn vị chưa có mẫu cho một mã thì dùng mẫu mặc định trong <see cref="DefaultTemplates"/>.
/// Biến dạng <c>{{ ten_bien }}</c> — giá trị được mã hoá HTML khi chèn vào nội dung.
/// </summary>
public sealed partial class EmailTemplate : TenantEntity, IHasStatus
{
    public const int MaxBodyLength = 50_000;

    private EmailTemplate() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Subject { get; private set; } = "";
    public string Body { get; private set; } = "";
    public int Status { get; private set; } = IHasStatus.Active;

    public static EmailTemplate Create(string code, string name, string subject, string body, int? status)
    {
        var template = new EmailTemplate { Code = NormalizeCode(code) };
        template.Update(name, subject, body, status);
        return template;
    }

    /// <summary>Mã không đổi được sau khi tạo — service nghiệp vụ tham chiếu theo mã.</summary>
    public void Update(string name, string subject, string body, int? status)
    {
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 200 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên bắt buộc, tối đa 200 ký tự.");
        var s = (subject ?? "").Trim();
        Subject = s.Length is > 0 and <= 300 && !s.Contains('\n', StringComparison.Ordinal) && !s.Contains('\r', StringComparison.Ordinal)
            ? s
            : throw new BusinessRuleException("SUBJECT_INVALID", "Tiêu đề bắt buộc, một dòng, tối đa 300 ký tự.");
        var b = body ?? "";
        Body = b.Trim().Length > 0 && b.Length <= MaxBodyLength
            ? b
            : throw new BusinessRuleException("BODY_INVALID", $"Nội dung bắt buộc, tối đa {MaxBodyLength:N0} ký tự.");
        if (status is not null) ChangeStatus(status.Value);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public static string NormalizeCode(string? code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        return c.Length is > 0 and <= 50 && CodePattern().IsMatch(c)
            ? c
            : throw new BusinessRuleException("TEMPLATE_CODE_INVALID", "Mã mẫu chỉ gồm chữ in hoa, số và dấu _, tối đa 50 ký tự.");
    }

    [GeneratedRegex("^[A-Z0-9_]+$")]
    private static partial Regex CodePattern();
}

/// <summary>Mẫu mặc định của nền tảng — seed cho đơn vị mới và dùng khi đơn vị chưa có mẫu riêng.</summary>
public static class DefaultTemplates
{
    public const string TestEmail = "TEST_EMAIL";
    public const string LoginOtp = "LOGIN_OTP";

    public sealed record Definition(string Code, string Name, string Subject, string Body);

    public static readonly IReadOnlyList<Definition> All =
    [
        new(TestEmail, "Thư kiểm tra cấu hình email", "Thư kiểm tra từ {{ tenant_name }}",
            """
            <p>Xin chào,</p>
            <p>Đây là thư kiểm tra cấu hình gửi email của <strong>{{ tenant_name }}</strong>.</p>
            <p>Nếu bạn nhận được thư này, máy chủ gửi thư đã hoạt động.</p>
            """),
        new(LoginOtp, "Mã xác thực đăng nhập", "Mã xác thực đăng nhập {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Mã xác thực đăng nhập của bạn là:</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:4px">{{ otp }}</p>
            <p>Mã có hiệu lực trong {{ minutes }} phút. Không chia sẻ mã này cho bất kỳ ai.</p>
            <p>Nếu bạn không đăng nhập, hãy đổi mật khẩu ngay.</p>
            """),
    ];

    public static Definition? Find(string code) => All.FirstOrDefault(d => d.Code == code);
}
