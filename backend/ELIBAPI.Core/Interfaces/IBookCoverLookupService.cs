namespace ELIBAPI.Core.Interfaces;

/// Tra cứu ảnh bìa sách theo ISBN qua Google Books, dự phòng OpenLibrary — không cần tenant (lookup
/// ngoài, không đụng dữ liệu nội bộ).
public interface IBookCoverLookupService
{
    Task<string?> FindCoverUrlAsync(string isbn);
}
