namespace ELIBAPI.Core.DTOs.Chat;

/// <summary>
/// Kết quả của trợ lý TÌM TÀI LIỆU: một câu dẫn ngắn + danh sách thẻ tài liệu bấm được.
///
/// <see cref="Answer"/> cố ý chỉ là câu dẫn (“Tìm thấy 12 tài liệu về …”), KHÔNG phải bài trả lời
/// tổng hợp từ nội dung sách — nội dung nằm ở nhánh <c>chat/ask</c>. Nhờ vậy phía client biết chắc
/// mọi thông tin thực chất đều nằm trong <see cref="Documents"/> và render được thành thẻ.
/// </summary>
public class DocumentFinderResponse
{
    public string Answer { get; set; } = "";

    /// <summary>Bộ tiêu chí LLM bóc được — trả về để UI hiện "chip" cho người dùng thấy đã hiểu gì.</summary>
    public DocumentFinderCriteria Criteria { get; set; } = new();

    /// <summary>Tổng số tài liệu khớp trong kho (có thể lớn hơn số phần tử trả về).</summary>
    public long Total { get; set; }

    public List<DocumentFinderItem> Documents { get; set; } = [];
}

/// <summary>Tiêu chí thư mục bóc từ câu hỏi tự nhiên. Mọi trường đều có thể null.</summary>
public class DocumentFinderCriteria
{
    /// <summary>Từ khóa cốt lõi đã viết lại gọn — cái thực sự ném vào ô tìm kiếm.</summary>
    public string? Keyword         { get; set; }
    public string? Title           { get; set; }
    public string? Author          { get; set; }
    public string? Publisher       { get; set; }
    public string? DocType         { get; set; }
    public string? Language        { get; set; }
    public string? MaterialType    { get; set; }
    public int?    PublishYearFrom { get; set; }
    public int?    PublishYearTo   { get; set; }
    public bool?   AvailableOnly   { get; set; }
    public string? SortBy          { get; set; }

    /// <summary>Chủ đề/môn học đã suy ra (từ câu nói tự do hoặc từ hồ sơ bạn đọc) -- không phải giá
    /// trị Gemini trả trực tiếp, dùng để lọc Elasticsearch theo topicId/subjectId đã có sẵn.</summary>
    public long?   TopicId         { get; set; }
    public long?   SubjectId       { get; set; }
    /// <summary>Tên hiển thị của Topic/Subject đã resolve — dùng để ghép câu dẫn cho người dùng.</summary>
    public string? TopicName       { get; set; }
    public string? SubjectName     { get; set; }
    /// <summary>Gemini xác định người dùng xin gợi ý theo hồ sơ cá nhân (không nêu chủ đề cụ thể).</summary>
    public bool    WantsPersonalRecommendation { get; set; }
}

/// <summary>
/// Một tài liệu trong kết quả. Rút gọn từ <c>UnifiedSearchItem</c>, chỉ giữ những trường đủ để
/// vẽ 1 thẻ và điều hướng — widget chat không cần bộ trường đầy đủ của trang tìm kiếm.
/// </summary>
public class DocumentFinderItem
{
    /// <summary>"print" | "digital" — quyết định client mở sang trang chi tiết nào.</summary>
    public string  DocType     { get; set; } = string.Empty;
    public string? PublicId    { get; set; }
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public int?    PublishYear { get; set; }
    public string? Images      { get; set; }
    public string? Language    { get; set; }
    public string? Keyword     { get; set; }

    // Riêng tài liệu số
    public long? EbookId { get; set; }
    public bool? Free    { get; set; }

    // Riêng tài liệu in — số bản thời gian thực để hiện "còn/hết"
    public long? BibId          { get; set; }
    public int?  CopyCount      { get; set; }
    public int?  AvailableCount { get; set; }
}
