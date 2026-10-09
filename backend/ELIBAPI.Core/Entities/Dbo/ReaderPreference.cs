using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>
/// Hồ sơ chủ đề/môn học bạn đọc quan tâm — dùng để chatbot gợi ý tài liệu theo hồ sơ mà không cần
/// hỏi lại mỗi lần. Bảng mới, không đụng tới bảng Reader sẵn có. 1 bạn đọc = 1 dòng (upsert).
/// </summary>
[Table("ReaderPreference", Schema = "dbo")]
public class ReaderPreference
{
    [Key] public long Id { get; set; }
    public long ReaderId { get; set; }
    /// <summary>Ebook.EbookSubject.Id — "môn học"/lĩnh vực quan tâm.</summary>
    public long? SubjectId { get; set; }
    /// <summary>Ebook.EbookTopic.Id — "chủ đề" quan tâm.</summary>
    public long? TopicId { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    /// <summary>Stamp theo tenant của reader lúc ghi — không dùng để lọc (đã lọc đủ qua ReaderId,
    /// 1 reader chỉ thuộc đúng 1 tenant), chỉ giữ để nhất quán quy ước "entity mới luôn có TenantId".</summary>
    public long? TenantId { get; set; }
}
