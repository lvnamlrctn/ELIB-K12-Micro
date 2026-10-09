using ELIBAPI.Core.DTOs.Response;

namespace ELIBAPI.Core.Interfaces;

public interface IMinioService
{
    string PublicBaseUrl { get; }

    /// <summary>List image files (jpg/jpeg/png/gif/webp) in the public bucket, newest first. Returns path relative to PublicBaseUrl (e.g. "2026/07/uuid.png").</summary>
    Task<(List<MinioImageItem> Items, int TotalCount)> ListPublicImagesAsync(string? keyword, int page, int pageSize);

    /// <summary>Upload stream to MinIO public bucket, returns object name (e.g. "2026/06/uuid.ext"). Frontend constructs full URL using PublicBaseUrl from config.</summary>
    Task<string> UploadAsync(Stream stream, string filename, string contentType);

    /// <summary>Delete object by full URL or object name. Silent on failure.</summary>
    Task DeleteAsync(string urlOrObjectName);

    /// <summary>Delete an object from the PRIVATE bucket by its exact object name. Silent on failure.</summary>
    Task DeletePrivateAsync(string objectName);

    /// <summary>Upload to private bucket (no public access). Returns object name only.</summary>
    Task<string> UploadPrivateAsync(Stream stream, string filename, string contentType);

    /// <summary>Download object from private bucket into memory. Caller must dispose the stream.</summary>
    Task<(Stream stream, string contentType)> GetObjectStreamAsync(string objectName);

    /// <summary>Download object from PUBLIC bucket into memory. Caller must dispose the stream.</summary>
    Task<(Stream stream, string contentType)> GetPublicObjectStreamAsync(string objectName);

    /// <summary>Generate a presigned GET URL for a private-bucket object (time-limited).</summary>
    Task<string> GetPresignedUrlAsync(string objectName, int expirySeconds = 900);

    /// <summary>Đợt 22 — kiểm tra MinIO còn phản hồi (dùng cho /ready), chỉ hỏi tồn tại bucket public, không tải gì.</summary>
    Task<bool> PingAsync();

    /// <summary>Đợt 22.4 — liệt kê TOÀN BỘ object trong bucket riêng tư (đệ quy), dùng cho
    /// <c>DigitalStorageAuditJob</c>. Không tải nội dung file, chỉ tên + kích thước.</summary>
    IAsyncEnumerable<(string ObjectName, long Size)> ListPrivateBucketObjectsAsync();
}
