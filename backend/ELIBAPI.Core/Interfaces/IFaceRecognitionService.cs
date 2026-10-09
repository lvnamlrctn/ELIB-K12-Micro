namespace ELIBAPI.Core.Interfaces;

public record FaceMatchResult(long ReaderId, Guid PublicId, string? CardNo, string? Name, string? Photo, double Confidence);

/// Nhận diện khuôn mặt bạn đọc qua Gemini vision, dùng để tự động điền mã thẻ ở màn hình Mượn sách và
/// Check-in/out, và check-in phòng học nhóm (kiosk / bạn đọc tự xác minh). Chỉ đọc, không có tác dụng phụ.
public interface IFaceRecognitionService
{
    /// <param name="tenantId">Tenant hiện tại (resolve từ claim ở controller) — giới hạn ứng viên so khớp
    /// đúng bạn đọc thuộc tenant này; null nếu tài khoản không thuộc tenant nào (thấy toàn bộ).</param>
    /// <param name="candidateReaderIds">Khác null: chỉ so với đúng các bạn đọc này (người có lượt đặt phòng đang tới giờ, hoặc 1 bạn
    /// đọc cần xác minh) — nhanh và chính xác hơn quét toàn bộ (port ELIB-LRC 09-30). Rỗng → null ngay, không gọi AI.</param>
    Task<FaceMatchResult?> IdentifyReaderAsync(string capturedImageBase64, long? tenantId,
        IReadOnlyCollection<long>? candidateReaderIds = null, CancellationToken ct = default);

    /// <summary>Bạn đọc có ít nhất 1 ảnh khuôn mặt (Reader.Photo hoặc ReaderPhoto) để nhận diện không.</summary>
    Task<bool> HasFacePhotoAsync(long readerId, CancellationToken ct = default);
}
