namespace Elib.Contracts.Events.Patron;

/// <summary>
/// Trạng thái hiện tại của một bạn đọc (thêm/sửa/khoá/mở/xoá) — circulation, digital, space, payment, search giữ
/// PatronReplica từ event này (docs 02 §dữ liệu dùng chung), khoá theo <see cref="ReaderPublicId"/>.
/// Bản sao chỉ ghi khi <see cref="Version"/> lớn hơn bản đang có.
/// </summary>
public sealed record ReaderChanged : IntegrationEvent
{
    public required Guid ReaderPublicId { get; init; }
    public required string CardNo { get; init; }
    public required string FullName { get; init; }
    public long? ReaderTypeId { get; init; }
    public long? ClassId { get; init; }
    public long? CourseId { get; init; }

    /// <summary>Tên loại bạn đọc/lớp/khoá lúc phát event — để màn mượn trả hiển thị, không phải gọi patron.</summary>
    public string? ReaderTypeName { get; init; }

    public string? ClassName { get; init; }
    public string? CourseName { get; init; }

    /// <summary>Ảnh thẻ (file ở media, riêng tư).</summary>
    public Guid? PhotoId { get; init; }

    /// <summary>2 = hoạt động, 1 = bị khoá (quy ước Status của monolith).</summary>
    public int Status { get; init; }

    public DateOnly? ExpireDate { get; init; }
    public bool Deleted { get; init; }
    public long Version { get; init; }
}
