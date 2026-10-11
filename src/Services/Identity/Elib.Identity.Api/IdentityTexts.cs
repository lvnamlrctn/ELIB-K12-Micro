using System.Globalization;

namespace Elib.Identity.Api;

/// <summary>
/// Chữ trên các trang đăng nhập do identity render (vi mặc định, en). Ngôn ngữ: <c>?lang=vi|en</c> (lưu lại vào cookie), không có thì
/// cookie <c>elib_lang</c> mà OPAC/Admin cùng host ghi khi bạn đọc/cán bộ chọn ngôn ngữ.
/// </summary>
public sealed class IdentityTexts
{
    public const string Cookie = "elib_lang";

    private static readonly Dictionary<string, (string Vi, string En)> All = new(StringComparer.Ordinal)
    {
        ["SYSTEM_NAME"] = ("Hệ thống quản lý thư viện", "Library management system"),
        ["BRAND_TITLE"] = ("Hệ Thống<br>Quản Lý Thư Viện", "Library<br>Management System"),
        ["BRAND_TEXT"] = ("Giải pháp toàn diện giúp bạn quản lý kho sách, xuất bản phẩm số và phục vụ độc giả một cách chuyên nghiệp và hiệu quả nhất.",
            "An all-in-one solution to manage your collections, digital publications and reader services professionally and efficiently."),
        ["LANGUAGE"] = ("Ngôn ngữ", "Language"),
        ["LOGIN_TITLE"] = ("Đăng nhập", "Sign in"),
        ["LOGIN_SUBTITLE"] = ("Chào mừng bạn quay lại hệ thống quản trị", "Welcome back to the administration system"),
        ["TENANT_CODE"] = ("Mã đơn vị", "Organisation code"),
        ["TENANT_HINT"] = ("Để trống nếu là quản trị hệ thống.", "Leave empty for platform administrators."),
        ["CAPTCHA"] = ("Mã xác nhận", "Verification code"),
        ["CAPTCHA_ALT"] = ("Ảnh mã xác nhận", "Verification code image"),
        ["CAPTCHA_REFRESH"] = ("Đổi mã khác", "Get another code"),
        ["CAPTCHA_PLACEHOLDER"] = ("Nhập {0} ký tự trong ảnh", "Enter the {0} characters in the image"),
        ["CAPTCHA_INVALID"] = ("Mã xác nhận không đúng hoặc đã hết hạn.", "The verification code is wrong or has expired."),
        ["USERNAME"] = ("Tên đăng nhập", "Username"),
        ["PASSWORD"] = ("Mật khẩu", "Password"),
        ["SIGN_IN_BTN"] = ("Đăng Nhập &rarr;", "Sign in &rarr;"),
        ["ERR_LOCKED_OUT"] = ("Tài khoản tạm khoá do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.",
            "The account is temporarily locked after too many failed sign-ins. Please try again in 15 minutes."),
        ["ERR_DISABLED"] = ("Tài khoản đã bị vô hiệu hoá. Liên hệ quản trị thư viện.", "The account has been disabled. Please contact the library administrator."),
        ["ERR_TENANT"] = ("Đơn vị không tồn tại hoặc đang tạm ngưng.", "The organisation does not exist or is suspended."),
        ["ERR_CREDENTIALS"] = ("Tên đăng nhập hoặc mật khẩu không đúng.", "Incorrect username or password."),
        ["OTP_TITLE"] = ("Xác thực đăng nhập", "Sign-in verification"),
        ["OTP_SUBTITLE"] = ("Bước 2: nhập mã gửi qua email", "Step 2: enter the code sent by email"),
        ["OTP_SENT"] = ("Mã xác thực gồm 6 chữ số đã được gửi tới <b>{0}</b>. Mã có hiệu lực trong 5 phút.",
            "A 6-digit verification code has been sent to <b>{0}</b>. The code is valid for 5 minutes."),
        ["OTP_CODE"] = ("Mã xác thực", "Verification code"),
        ["OTP_CONFIRM"] = ("Xác nhận &rarr;", "Confirm &rarr;"),
        ["OTP_RESEND"] = ("Gửi lại mã", "Resend code"),
        ["OTP_OTHER_ACCOUNT"] = ("Đăng nhập bằng tài khoản khác", "Sign in with another account"),
        ["OTP_WRONG"] = ("Mã xác thực không đúng.", "The verification code is incorrect."),
        ["OTP_TOO_MANY"] = ("Nhập sai mã xác thực quá nhiều lần. Vui lòng đăng nhập lại.", "Too many wrong codes. Please sign in again."),
        ["OTP_EXPIRED"] = ("Mã xác thực đã hết hạn. Vui lòng đăng nhập lại.", "The verification code has expired. Please sign in again."),
        ["OTP_RESENT"] = ("Đã gửi mã mới. Mã cũ không còn dùng được.", "A new code has been sent. The previous code no longer works."),
        ["OTP_WAIT"] = ("Vui lòng đợi {0} giây rồi gửi lại.", "Please wait {0} seconds before resending."),
        ["OTP_RESEND_LIMIT"] = ("Đã gửi lại quá số lần cho phép. Vui lòng đăng nhập lại để nhận mã mới.",
            "The resend limit has been reached. Please sign in again to get a new code."),
        ["IMP_INVALID_TITLE"] = ("Liên kết không hợp lệ", "Invalid link"),
        ["IMP_INVALID_SUBTITLE"] = ("Không mở được phiên xem đơn vị", "Could not open the organisation view"),
        ["IMP_INVALID_TEXT"] = ("Liên kết đã hết hạn, đã được dùng, hoặc không mở đúng tên miền của đơn vị.",
            "The link has expired, was already used, or was not opened on the organisation's domain."),
        ["IMP_INVALID_HINT"] = ("Quay lại trang quản trị nền tảng và bấm \"Vào xem đơn vị\" lần nữa.",
            "Go back to the platform administration and click \"View organisation\" again."),
        ["READER_LOGIN_TITLE"] = ("Đăng nhập bạn đọc", "Reader sign-in"),
        ["READER_LOGIN_SUBTITLE"] = ("Dùng số thẻ thư viện và mật khẩu thư viện đã cấp", "Use your library card number and the password issued by the library"),
        ["READER_NO_TENANT_SUBTITLE"] = ("Không xác định được thư viện", "Library not identified"),
        ["READER_NO_TENANT"] = ("Vui lòng đăng nhập từ trang tra cứu (OPAC) của trường bạn.", "Please sign in from your school's catalogue (OPAC) page."),
        ["READER_CARD"] = ("Số thẻ hoặc email", "Card number or email"),
        ["READER_CARD_PLACEHOLDER"] = ("Số thẻ bạn đọc", "Library card number"),
        ["READER_HINT"] = ("Chưa có mật khẩu hoặc quên mật khẩu? Liên hệ thư viện của trường.", "No password or forgot it? Please contact your school library."),
        ["READER_ERR_LOCKED_OUT"] = ("Tài khoản tạm khoá do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.",
            "The account is temporarily locked after too many failed sign-ins. Please try again in 15 minutes."),
        ["READER_ERR_DISABLED"] = ("Thẻ bạn đọc đang bị khoá. Vui lòng liên hệ thư viện.", "Your library card is blocked. Please contact the library."),
        ["READER_ERR_TENANT"] = ("Thư viện đang tạm ngưng phục vụ trực tuyến.", "The library's online services are temporarily suspended."),
        ["READER_ERR_CREDENTIALS"] = ("Số thẻ hoặc mật khẩu không đúng.", "Incorrect card number or password."),
        ["READER_NEW_PASSWORD_TITLE"] = ("Đặt mật khẩu mới", "Set a new password"),
        ["READER_NEW_PASSWORD_SUBTITLE"] = ("Lần đầu đăng nhập: hãy đổi mật khẩu thư viện đã cấp", "First sign-in: please change the password issued by the library"),
        ["READER_NEW_PASSWORD"] = ("Mật khẩu mới", "New password"),
        ["READER_NEW_PASSWORD_HINT"] = ("Từ {0} ký tự, không trùng số thẻ.", "At least {0} characters, not your card number."),
        ["READER_CONFIRM_PASSWORD"] = ("Nhập lại mật khẩu mới", "Confirm new password"),
        ["READER_CHANGE_BTN"] = ("Đổi mật khẩu &rarr;", "Change password &rarr;"),
        ["READER_PASSWORD_MISMATCH"] = ("Hai lần nhập mật khẩu không khớp.", "The passwords do not match."),
        // Mã lỗi của ReaderSignInService.ChangePasswordAsync — hiện trên trang đổi mật khẩu lần đầu.
        ["READER_PASSWORD_INVALID"] = ("Mật khẩu bạn đọc từ 6 đến 128 ký tự.", "Reader passwords must be 6 to 128 characters long."),
        ["READER_PASSWORD_IS_CARDNO"] = ("Mật khẩu mới không được trùng số thẻ.", "The new password must not be your card number."),
        ["PASSWORD_REUSED"] = ("Mật khẩu mới phải khác mật khẩu hiện tại.", "The new password must be different from the current one."),
    };

    private IdentityTexts(string lang) => Lang = lang;

    /// <summary>vi | en</summary>
    public string Lang { get; }

    public bool English => Lang == "en";

    public string this[string key] => All.TryGetValue(key, out var t) ? (English ? t.En : t.Vi) : key;

    public string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, this[key], args);

    /// <summary>Có bản dịch cho mã (dùng cho mã lỗi nghiệp vụ); không có thì null để nơi gọi dùng thông báo gốc.</summary>
    public string? Code(string code) => All.ContainsKey(code) ? this[code] : null;

    /// <summary>Ngôn ngữ của request; <c>?lang=</c> hợp lệ thì ghi cookie để các bước sau (POST, OTP) giữ ngôn ngữ.</summary>
    public static IdentityTexts For(HttpContext ctx)
    {
        var query = ctx.Request.Query["lang"].ToString();
        if (query is "vi" or "en")
        {
            ctx.Response.Cookies.Append(Cookie, query, new CookieOptions
            {
                Path = "/", SameSite = SameSiteMode.Lax, Secure = ctx.Request.IsHttps, MaxAge = TimeSpan.FromDays(365), HttpOnly = false,
            });
            return new IdentityTexts(query);
        }
        return new IdentityTexts(ctx.Request.Cookies[Cookie] == "en" ? "en" : "vi");
    }
}
