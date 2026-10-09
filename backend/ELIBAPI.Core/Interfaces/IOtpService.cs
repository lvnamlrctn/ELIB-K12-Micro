namespace ELIBAPI.Core.Interfaces;

/// Sinh + xác thực mã OTP 6 số dùng chung cho MỌI luồng đăng nhập 2 lớp (bạn đọc, admin, ...) — tách ra
/// từ logic vốn nằm riêng trong ReaderAuthService để AuthService (đăng nhập admin) dùng lại được, không
/// copy-paste. subjectId là khoá chính của đối tượng đăng nhập (Reader.Id hoặc Users.Id) — service này
/// không quan tâm nó là ai, chỉ cache lại và trả về khi Verify đúng.
///
/// purpose dùng để tách namespace cache giữa các luồng khác nhau (vd "ReaderLogin" / "AdminLogin"),
/// tránh trường hợp lý thuyết 1 sessionToken (Guid ngẫu nhiên, gần như không đụng nhau) bị dùng nhầm
/// giữa 2 luồng khác nhau nếu bị lộ.
public interface IOtpService
{
    /// Sinh 1 mã OTP mới + session token, cache 5 phút. Trả về (Otp, SessionToken) — Otp dùng để gửi
    /// email, SessionToken trả về cho client để dùng ở bước Verify.
    (string Otp, string SessionToken) GenerateAndCache(long subjectId, string purpose);

    /// Kiểm tra mã OTP. Tối đa 5 lần sai trước khi tự xoá khỏi cache. Dùng 1 lần — xoá ngay khi đúng.
    OtpVerifyResult Verify(string sessionToken, string code, string purpose);
}

public record OtpVerifyResult(bool Success, long? SubjectId, string? Error);
