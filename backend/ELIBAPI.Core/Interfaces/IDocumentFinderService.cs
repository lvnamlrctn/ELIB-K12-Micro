using ELIBAPI.Core.DTOs.Chat;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Trợ lý TÌM TÀI LIỆU — tách hẳn khỏi <see cref="IChatService"/> (RAG nội dung trang sách).
/// Chỉ tra cứu thư mục trên index metadata, không đọc nội dung bên trong tài liệu.
/// </summary>
public interface IDocumentFinderService
{
    Task<DocumentFinderResponse> FindAsync(DocumentFinderRequest request);
}
