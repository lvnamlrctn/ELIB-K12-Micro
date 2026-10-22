namespace Elib.Contracts.Events.Digital;

/// <summary>
/// Trạng thái hiện tại của một tài liệu số (thêm/sửa/ẩn/hiện/xoá/đổi metadata/đổi file). search giữ bản sao tìm kiếm từ event này,
/// khoá theo <see cref="EbookPublicId"/>; chỉ ghi khi <see cref="Version"/> lớn hơn bản đang có (cùng khuôn <c>BibChanged</c>).
/// Gộp "xuất bản / cập nhật / gỡ" của docs 03 vào một event trạng thái: nơi nhận suy ra từ <see cref="Status"/> + <see cref="Deleted"/>,
/// không phụ thuộc thứ tự tới của nhiều loại event. Nội dung file không đi theo event (chưa trích xuất toàn văn).
/// </summary>
public sealed record EbookChanged : IntegrationEvent
{
    public required Guid EbookPublicId { get; init; }

    /// <summary>Id tăng dần trong đơn vị — con trỏ trang hoá khi search dựng lại chỉ mục qua /internal.</summary>
    public required long EbookId { get; init; }

    public required string Title { get; init; }
    public string? Author { get; init; }
    public string? OtherTitle { get; init; }
    public string? Publisher { get; init; }
    public string? PublishYear { get; init; }
    public string? Keywords { get; init; }
    public string? Language { get; init; }
    public string? CollectionName { get; init; }
    public string? SubjectName { get; init; }
    public string? TopicName { get; init; }
    public string? TypeName { get; init; }

    /// <summary>Giá trị metadata đã nhập (đã làm phẳng) — để tìm theo trường mô tả mở rộng.</summary>
    public IReadOnlyList<string> Metadata { get; init; } = [];

    /// <summary>Ảnh bìa: đường dẫn ảnh trong kho media công khai (/s3/media-public/…).</summary>
    public string? CoverUrl { get; init; }

    /// <summary>Đọc không cần đăng nhập.</summary>
    public bool Free { get; init; }

    /// <summary>Có ít nhất một file để đọc.</summary>
    public bool HasFile { get; init; }

    /// <summary>Liên kết tới biểu ghi sách in (catalog) — OPAC hiện "có bản điện tử" ở trang chi tiết sách in.</summary>
    public Guid? LinkedBibPublicId { get; init; }

    /// <summary>2 = xuất bản trên OPAC, 1 = ẩn.</summary>
    public int Status { get; init; }

    public bool Deleted { get; init; }
    public long Version { get; init; }
}
