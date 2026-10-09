namespace ELIBAPI.Core.DTOs.Chat;

/// <summary>
/// Câu hỏi gửi cho trợ lý TÌM TÀI LIỆU — nhánh tách hẳn khỏi <see cref="ChatRequest"/>.
///
/// Khác biệt cốt lõi so với RAG nội dung (<c>chat/ask</c>): ở đây LLM chỉ làm đúng một việc là bóc
/// tách ý định người dùng thành bộ tiêu chí thư mục, còn việc tìm do Elasticsearch trên index
/// metadata <c>library_docs</c> đảm nhiệm. Không có tham số nào chạm tới index nội dung trang sách,
/// và DTO này cố ý KHÔNG có <c>EbookId</c> để không thể vô tình bị dùng làm chat trong tài liệu.
/// </summary>
public class DocumentFinderRequest
{
    public string Question { get; set; } = "";

    /// <summary>Các lượt trước trong hội thoại — để hiểu câu nối tiếp ("chỉ bản từ 2020").</summary>
    public List<ChatTurn>? History { get; set; }

    // ── Ràng buộc do portal áp đặt (người dùng không sửa được qua câu hỏi) ────
    /// <summary>Giới hạn trong 1 bộ sưu tập — dùng khi widget chat nhúng ở trang bộ sưu tập.</summary>
    public string? CollectionId { get; set; }
    /// <summary>"print" | "digital" | null = cả hai.</summary>
    public string? DocType { get; set; }

    /// <summary>GUID đơn vị do client truyền (Public API, giống UnifiedSearchRequest.TenantId) —
    /// giới hạn kết quả trong đúng tenant của portal đang chạy. Được phân giải sang Id số ở controller.</summary>
    public Guid? TenantId { get; set; }
    /// <summary>Id số đã phân giải — controller gán, client không truyền.</summary>
    public long? ResolvedTenantId { get; set; }

    /// <summary>Số tài liệu tối đa trả về. Kẹp trong [1, 20] ở service.</summary>
    public int PageSize { get; set; } = 6;

    /// <summary>PublicId của bạn đọc đang đăng nhập — controller tự điền từ JWT, KHÔNG nhận từ
    /// client, để gợi ý "theo hồ sơ của tôi" không thể bị giả mạo qua body request.</summary>
    public Guid? ReaderPublicId { get; set; }
}
