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
    public const string ReaderPasswordReset = "READER_PASSWORD_RESET";

    // Lưu thông (circulation gửi theo mã — giữ tên monolith EMAIL_PRINT_*).
    public const string PrintDueSoon = "PRINT_DUE_SOON";
    public const string PrintOverdue = "PRINT_OVERDUE";
    public const string PrintHoldReady = "PRINT_HOLD_READY";
    public const string PrintHoldExpired = "PRINT_HOLD_EXPIRED";

    // Tài liệu số (digital gửi theo mã).
    public const string EbookHoldReady = "EBOOK_HOLD_READY";
    public const string EbookLoanExpired = "EBOOK_LOAN_EXPIRED";
    public const string EbookSubmissionApproved = "EBOOK_SUBMISSION_APPROVED";
    public const string EbookSubmissionRejected = "EBOOK_SUBMISSION_REJECTED";
    public const string EbookReviewApproved = "EBOOK_REVIEW_APPROVED";
    public const string EbookReviewRejected = "EBOOK_REVIEW_REJECTED";

    // Cổng thông tin (portal gửi theo mã).
    public const string PortalContactReceived = "PORTAL_CONTACT_RECEIVED";

    // Không gian & đặt phòng (space gửi theo mã).
    public const string SpaceBookingCreated = "SPACE_BOOKING_CREATED";
    public const string SpaceBookingApproved = "SPACE_BOOKING_APPROVED";
    public const string SpaceBookingRejected = "SPACE_BOOKING_REJECTED";
    public const string SpaceBookingExpired = "SPACE_BOOKING_EXPIRED";
    public const string SpaceBookingCancelled = "SPACE_BOOKING_CANCELLED";
    public const string SpaceBookingNoShow = "SPACE_BOOKING_NOSHOW";
    public const string SpaceBookingBanned = "SPACE_BOOKING_BANNED";

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
        new(ReaderPasswordReset, "Đặt lại mật khẩu bạn đọc", "Đặt lại mật khẩu tra cứu — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Thư viện nhận được yêu cầu đặt lại mật khẩu tra cứu (OPAC) cho thẻ <strong>{{ card_no }}</strong>.</p>
            <p><a href="{{ link }}">Bấm vào đây để đặt mật khẩu mới</a> — link có hiệu lực trong {{ minutes }} phút và chỉ dùng được một lần.</p>
            <p>Nếu bạn không yêu cầu, hãy bỏ qua thư này; mật khẩu hiện tại vẫn giữ nguyên.</p>
            """),
        new(PrintDueSoon, "Nhắc sách sắp đến hạn trả", "Sách sắp đến hạn trả — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Tài liệu <strong>{{ title }}</strong> (ĐKCB {{ barcode }}) bạn mượn sẽ đến hạn trả vào ngày <strong>{{ due_date }}</strong>.</p>
            <p>Vui lòng trả hoặc gia hạn tại thư viện trước ngày này để tránh bị phạt.</p>
            """),
        new(PrintOverdue, "Thông báo sách quá hạn trả", "Sách đã quá hạn trả — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Tài liệu <strong>{{ title }}</strong> (ĐKCB {{ barcode }}) đã quá hạn trả từ ngày <strong>{{ due_date }}</strong>.</p>
            <p>Vui lòng mang trả thư viện sớm. Bạn đọc có tài liệu quá hạn sẽ không mượn thêm được và có thể bị phạt.</p>
            """),
        new(PrintHoldReady, "Sách đặt mượn đã sẵn sàng", "Sách bạn đặt mượn đã sẵn sàng — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Tài liệu <strong>{{ title }}</strong> bạn đặt mượn đã được giữ tại quầy (ĐKCB {{ barcode }}).</p>
            <p>Vui lòng đến mượn trước hết ngày <strong>{{ expires_at }}</strong>; sau thời hạn này sách sẽ chuyển cho bạn đọc khác.</p>
            """),
        new(PrintHoldExpired, "Huỷ giữ sách đặt mượn quá hạn", "Đặt mượn đã hết hạn giữ — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Tài liệu <strong>{{ title }}</strong> giữ cho bạn đến hết ngày {{ expires_at }} nhưng bạn chưa đến mượn, nên đặt mượn đã bị huỷ.</p>
            <p>Bạn có thể đặt mượn lại nếu vẫn cần tài liệu này.</p>
            """),
        new(EbookHoldReady, "Tài liệu số đặt trước đã sẵn sàng", "Tài liệu số bạn đặt trước đã sẵn sàng — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Tài liệu số <strong>{{ title }}</strong> bạn đặt trước đã có chỗ đọc.</p>
            <p>Vui lòng vào mượn trước <strong>{{ ready_expires_at }}</strong>; sau thời hạn này chỗ đọc sẽ chuyển cho bạn đọc khác.</p>
            """),
        new(EbookLoanExpired, "Tài liệu số đã hết hạn mượn", "Tài liệu số đã hết hạn mượn — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Tài liệu số <strong>{{ title }}</strong> bạn mượn đã hết hạn truy cập lúc {{ expired_at }}.</p>
            <p>Bạn có thể mượn lại nếu vẫn cần đọc tài liệu này.</p>
            """),
        new(EbookSubmissionApproved, "Tài liệu nộp đã được duyệt", "Tài liệu bạn nộp đã được duyệt — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Tài liệu <strong>{{ title }}</strong> bạn nộp đã được thủ thư duyệt và đưa vào thư viện số.</p>
            """),
        new(EbookSubmissionRejected, "Tài liệu nộp bị từ chối", "Tài liệu bạn nộp chưa được duyệt — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Tài liệu <strong>{{ title }}</strong> bạn nộp chưa được duyệt. Lý do: {{ reason }}.</p>
            """),
        new(EbookReviewApproved, "Đánh giá tài liệu đã được duyệt", "Đánh giá của bạn đã được duyệt — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Đánh giá của bạn về tài liệu <strong>{{ title }}</strong> đã được thủ thư duyệt và hiển thị công khai. Cảm ơn bạn đã đóng góp!</p>
            """),
        new(EbookReviewRejected, "Đánh giá tài liệu không được duyệt", "Đánh giá của bạn chưa được duyệt — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Đánh giá của bạn về tài liệu <strong>{{ title }}</strong> chưa được duyệt. Lý do: {{ reason }}.</p>
            """),
        new(PortalContactReceived, "Có liên hệ mới từ cổng thông tin", "Liên hệ mới: {{ contact_title }} — {{ tenant_name }}",
            """
            <p>Có liên hệ mới gửi từ cổng thông tin.</p>
            <p><strong>Người gửi:</strong> {{ contact_name }} &lt;{{ contact_email }}&gt;</p>
            <p><strong>Tiêu đề:</strong> {{ contact_title }}</p>
            <p>{{ contact_content }}</p>
            """),
        new(SpaceBookingCreated, "Đã gửi yêu cầu đặt phòng", "Đã gửi yêu cầu đặt phòng — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Yêu cầu đặt <strong>{{ space_name }}</strong> từ {{ start_at }} đến {{ end_at }} đã được gửi, chờ thủ thư duyệt.</p>
            """),
        new(SpaceBookingApproved, "Đặt phòng đã được duyệt", "Đặt phòng đã được duyệt — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Lượt đặt <strong>{{ space_name }}</strong> từ {{ start_at }} đến {{ end_at }} đã được duyệt.</p>
            <p>Vui lòng check-in đúng giờ — tới muộn quá giờ cho phép lượt đặt sẽ tự huỷ.</p>
            """),
        new(SpaceBookingRejected, "Đặt phòng bị từ chối", "Đặt phòng bị từ chối — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Lượt đặt <strong>{{ space_name }}</strong> từ {{ start_at }} đến {{ end_at }} chưa được duyệt. Lý do: {{ reject_reason }}.</p>
            """),
        new(SpaceBookingExpired, "Yêu cầu đặt phòng đã quá hạn duyệt", "Yêu cầu đặt phòng đã quá hạn duyệt — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Yêu cầu đặt <strong>{{ space_name }}</strong> từ {{ start_at }} đã quá giờ bắt đầu mà chưa được duyệt, nên tự động huỷ.</p>
            """),
        new(SpaceBookingCancelled, "Đã huỷ lượt đặt phòng", "Đã huỷ lượt đặt phòng — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Lượt đặt <strong>{{ space_name }}</strong> từ {{ start_at }} đến {{ end_at }} đã được huỷ theo yêu cầu của bạn.</p>
            """),
        new(SpaceBookingNoShow, "Vắng mặt lượt đặt phòng", "Bạn đã vắng mặt lượt đặt phòng — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Bạn không check-in lượt đặt <strong>{{ space_name }}</strong> lúc {{ start_at }} trong thời gian cho phép, nên lượt đặt đã bị huỷ.</p>
            <p>Vắng mặt nhiều lần có thể khiến bạn bị tạm cấm đặt phòng.</p>
            """),
        new(SpaceBookingBanned, "Tạm cấm đặt phòng", "Bạn đã bị tạm cấm đặt phòng — {{ tenant_name }}",
            """
            <p>Xin chào {{ full_name }},</p>
            <p>Do {{ reason }}, bạn tạm thời không đặt được phòng/không gian đến hết ngày <strong>{{ banned_until }}</strong>.</p>
            """),
    ];

    public static Definition? Find(string code) => All.FirstOrDefault(d => d.Code == code);
}
