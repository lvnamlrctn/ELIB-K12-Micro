namespace ELIBAPI.Core.Common;

/// <summary>Đuôi file chuẩn hoá: chữ thường, không dấu chấm đầu ("pdf"). Dữ liệu EbookFile.FileExt nhập từ hệ thống cũ
/// lưu dạng ".pdf"/".PDF" — so trực tiếp với "pdf" thì không bao giờ khớp (trước đây làm hỏng đóng dấu PDF, đếm số
/// trang đã đọc, xem theo trang). Thiếu FileExt thì lấy theo tên/đường dẫn file.</summary>
public static class FileExtension
{
    public static string Normalize(string? fileExt, string? fileNameFallback = null)
    {
        var ext = string.IsNullOrWhiteSpace(fileExt) ? Path.GetExtension(fileNameFallback ?? "") : fileExt;
        return ext.Trim().TrimStart('.').ToLowerInvariant();
    }

    public static bool IsPdf(string? fileExt, string? fileNameFallback = null, string? contentType = null) =>
        Normalize(fileExt, fileNameFallback) == "pdf" || string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
}
