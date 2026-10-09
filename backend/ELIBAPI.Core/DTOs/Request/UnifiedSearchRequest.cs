namespace ELIBAPI.Core.DTOs.Request;

/// <summary>
/// Tìm kiếm GỘP tài liệu in + tài liệu số trên index <c>library_docs</c>.
/// Mọi tiêu chí đều thuộc bộ trường CHUNG nên áp dụng được cho cả 2 loại tài liệu.
/// </summary>
public class UnifiedSearchRequest
{
    /// <summary>Tìm nhanh 1 ô — quét toàn bộ trường chung + nội dung file (tài liệu số).</summary>
    public string? Q { get; set; }

    /// <summary>Như <see cref="Q"/> nhưng CHỈ quét trường thư mục (title/author/publisher/keyword/
    /// summary/isbn/ddc/allMarcText) -- không đụng tới "content" (nội dung file). Dùng cho trợ lý
    /// TÌM TÀI LIỆU (DocumentFinderService): không được lẫn nội dung trang sách vào kết quả tra
    /// cứu thư mục, khác với tìm kiếm chính (Q) vốn cố tình quét luôn nội dung.</summary>
    public string? MetaQ { get; set; }

    // ── Tìm nâng cao theo từng tiêu chí ──────────────────────────────────────
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? Keyword     { get; set; }
    public string? Summary     { get; set; }
    public string? Isbn        { get; set; }
    public string? Ddc         { get; set; }
    /// <summary>Tìm trong NỘI DUNG file — chỉ tài liệu số có nội dung nên tự khoanh vùng.</summary>
    public string? Content     { get; set; }
    /// <summary>Số ĐKCB / nhan đề bản sao — chỉ tài liệu in.</summary>
    public string? CallNumber  { get; set; }

    // ── Bộ lọc ───────────────────────────────────────────────────────────────
    /// <summary>"print" | "digital" | null = cả hai.</summary>
    public string? DocType         { get; set; }
    public int?    PublishYearFrom { get; set; }
    public int?    PublishYearTo   { get; set; }
    public string? Language        { get; set; }
    public string? MaterialType    { get; set; }
    /// <summary>Đợt 22.2 — chọn ĐÚNG 1 tác giả từ facet "Tác giả" (khớp chính xác qua author.raw). Khác
    /// <see cref="Author"/> (ô tìm nâng cao, khớp gần đúng/mờ) — dùng song song, không thay thế.</summary>
    public string? AuthorExact     { get; set; }
    /// <summary>Đợt 25 — chọn NHIỀU tác giả từ facet "Tác giả" (multi-select). Có giá trị thì ưu tiên
    /// dùng thay <see cref="AuthorExact"/> (giữ lại cho tương thích các nơi gọi khác/URL cũ).</summary>
    public List<string>? AuthorsExact { get; set; }
    public string? CollectionId    { get; set; }
    /// <summary>CollectionId + toàn bộ bộ sưu tập con/cháu — controller công khai tự tính (ghi đè giá trị client
    /// gửi); null = chỉ lọc đúng <see cref="CollectionId"/> như trước (các nơi gọi nội bộ khác).</summary>
    public List<long>? ResolvedCollectionIds { get; set; }
    public int?    StoreId         { get; set; }
    /// <summary>Đợt 22.2 — lọc theo TÊN Kho (facet "Kho" trả tên, không phải Id; nhấn facet gửi lại đúng
    /// giá trị này). Khác <see cref="StoreId"/> (id số, dùng ở nơi khác) — dùng SONG SONG, không thay thế.</summary>
    public string? StoreName       { get; set; }
    /// <summary>Đợt 25 — chọn NHIỀU Kho từ facet "Kho" (multi-select). Có giá trị thì ưu tiên dùng thay
    /// <see cref="StoreName"/> (giữ lại cho tương thích các nơi gọi khác/URL cũ).</summary>
    public List<string>? StoreNames { get; set; }
    /// <summary>Đợt 25 — chọn NHIỀU năm xuất bản cụ thể từ facet "Năm xuất bản" (multi-select, khác
    /// <see cref="PublishYearFrom"/>/<see cref="PublishYearTo"/> vốn là 1 khoảng liên tục). Có giá trị
    /// thì ưu tiên dùng thay khoảng From/To.</summary>
    public List<int>? PublishYears { get; set; }
    public bool?   Free            { get; set; }
    /// <summary>Chỉ lấy tài liệu in còn bản sẵn sàng cho mượn.</summary>
    public bool?   AvailableOnly   { get; set; }
    /// <summary>Ebook.Topic.Id (dạng chuỗi) — chỉ tài liệu số có phân loại này (trợ lý tìm tài liệu, port ELIB-LRC 09-29).</summary>
    public string? TopicId         { get; set; }
    /// <summary>Ebook.Subject.Id (dạng chuỗi) — chỉ tài liệu số có phân loại này.</summary>
    public string? SubjectId       { get; set; }
    /// <summary>Nội bộ — chế độ nhẹ cho trợ lý chat: bỏ facet, highlight và dfs (chỉ cần danh sách + tổng số).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool    LightMode       { get; set; }

    /// <summary>GUID đơn vị do client truyền (Public API). Được phân giải sang Id số ở controller.</summary>
    public Guid? TenantId { get; set; }
    /// <summary>Id số đã phân giải — controller gán, client không truyền.</summary>
    public long? ResolvedTenantId { get; set; }

    public int Page     { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    /// <summary>"relevance" (mặc định) | "newest" | "oldest" | "title".</summary>
    public string? SortBy { get; set; }
}
