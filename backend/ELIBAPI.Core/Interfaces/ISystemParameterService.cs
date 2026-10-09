namespace ELIBAPI.Core.Interfaces;

public interface ISystemParameterService
{
    /// Đọc giá trị dbo.SystemParameter theo Code — ưu tiên dòng khớp TenantId hiện tại, fallback dòng
    /// TenantId=null (dùng chung toàn hệ thống). Tenant lấy từ HttpContext hiện tại — KHÔNG dùng được
    /// trong Hangfire job (không có HTTP request), dùng overload GetValueAsync(code, tenantId) thay thế.
    Task<string?> GetValueAsync(string code);

    /// Như trên nhưng nhận tenantId tường minh thay vì đọc từ HttpContext — bắt buộc dùng trong các
    /// Hangfire job (BookRequestExpiryJob, EbookLoanExpiryJob) vì job chạy không có HTTP request.
    Task<string?> GetValueAsync(string code, long? tenantId);

    /// Đọc 1 tham số dạng cờ bật/tắt ("1"/"0") và trả về bool — tự bỏ thẻ HTML trước khi so khớp vì
    /// trang "Tham số hệ thống" dùng CKEditor để sửa Value, dễ lưu "1" thành "&lt;p&gt;1&lt;/p&gt;" khiến
    /// so khớp chuỗi tuyệt đối == "1" lặng lẽ thất bại.
    Task<bool> IsEnabledAsync(string code);

    /// Như trên nhưng nhận tenantId tường minh (dùng trong Hangfire job, không có HTTP request).
    Task<bool> IsEnabledAsync(string code, long? tenantId);
}
