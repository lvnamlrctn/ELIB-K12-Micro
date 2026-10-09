namespace ELIBAPI.Core.Interfaces;

/// Sinh mã CAPTCHA dạng SVG vẽ tay (không dùng thư viện ảnh ngoài) cho lớp bảo mật đăng nhập bạn đọc
/// thứ 1. Mỗi mã dùng 1 lần: Validate luôn xoá mã khỏi cache bất kể kết quả đúng/sai, để chống replay
/// (đoán lại cùng 1 mã nhiều lần) và buộc FE phải tải mã mới sau mỗi lần thử.
public interface ICaptchaService
{
    /// Sinh 1 thử thách CAPTCHA mới — trả về (captchaId, mã SVG dạng chuỗi) để FE hiển thị trực tiếp.
    (string CaptchaId, string Svg) Generate();

    /// Kiểm tra đáp án. Trả về true nếu đúng và còn hạn (2 phút); false nếu sai/hết hạn/không tồn tại.
    /// Luôn xoá captchaId khỏi cache sau khi gọi, kể cả khi kết quả là true.
    bool Validate(string captchaId, string? answer);
}
