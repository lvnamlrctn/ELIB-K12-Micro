using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>Trạng thái nộp tài liệu bạn đọc (không có bên LRC — LRC's DocumentSubmission chỉ nhận, không có luồng duyệt).</summary>
public static class DocumentSubmissionStatus
{
    public const int Pending = 1;
    public const int Approved = 2;
    public const int Rejected = 3;
}

/// <summary>
/// Tài liệu bạn đọc tự nộp qua OPAC, chờ thủ thư duyệt thành tài liệu số (LRC: DocumentSubmission — port thêm luồng duyệt/từ
/// chối mà LRC chưa có, xem quyết định #5 của plan giai đoạn 2). File đã tải lên media (purpose document-submission) trước khi
/// tạo bản ghi này — xem <c>DocumentSubmissionResource.SubmitAsync</c>.
/// </summary>
public sealed class DocumentSubmission : TenantEntity
{
    public const int MaxTitleLength = 1000;
    public const int MaxAuthorLength = 500;
    public const int MaxAbstractLength = 4000;
    public const int MaxReasonLength = 500;
    public const int MaxFileNameLength = 255;
    public const int MaxFileExtLength = 20;

    private DocumentSubmission() { }

    public Guid ReaderPublicId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Author { get; private set; }
    public long? DigTypeId { get; private set; }
    public string? Abstract { get; private set; }
    public Guid MediaFileId { get; private set; }
    public string FileName { get; private set; } = "";
    public string? FileExt { get; private set; }
    public long FileSize { get; private set; }
    public int Status { get; private set; } = DocumentSubmissionStatus.Pending;
    public string? RejectReason { get; private set; }
    public long? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>Tài liệu số được tạo khi duyệt — gắn để không tạo trùng nếu bấm Duyệt nhiều lần.</summary>
    public Guid? CreatedItemPublicId { get; private set; }

    public static DocumentSubmission Submit(
        Guid readerPublicId, string title, string? author, long? digTypeId, string? @abstract, Guid mediaFileId, string fileName,
        string? fileExt, long fileSize) => new()
    {
        ReaderPublicId = readerPublicId,
        Title = TextRules.Required(title, MaxTitleLength, "TITLE_INVALID", "Tên tài liệu"),
        Author = TextRules.Optional(author, MaxAuthorLength, "AUTHOR_INVALID", "Tác giả"),
        DigTypeId = digTypeId,
        Abstract = TextRules.Optional(@abstract, MaxAbstractLength, "ABSTRACT_INVALID", "Tóm tắt"),
        MediaFileId = mediaFileId,
        FileName = TextRules.Required(fileName, MaxFileNameLength, "FILE_NAME_INVALID", "Tên file"),
        FileExt = TextRules.Optional(fileExt, MaxFileExtLength, "FILE_EXT_INVALID", "Đuôi file"),
        FileSize = fileSize >= 0 ? fileSize : throw new BusinessRuleException("FILE_SIZE_INVALID", "Dung lượng không được âm."),
    };

    public void Approve(long? actorId, DateTimeOffset now, Guid createdItemPublicId)
    {
        if (Status != DocumentSubmissionStatus.Pending)
            throw new BusinessRuleException("SUBMISSION_NOT_PENDING", "Tài liệu nộp không ở trạng thái chờ duyệt.");
        Status = DocumentSubmissionStatus.Approved;
        ReviewedBy = actorId;
        ReviewedAt = now;
        CreatedItemPublicId = createdItemPublicId;
    }

    public void Reject(long? actorId, DateTimeOffset now, string reason)
    {
        if (Status != DocumentSubmissionStatus.Pending)
            throw new BusinessRuleException("SUBMISSION_NOT_PENDING", "Tài liệu nộp không ở trạng thái chờ duyệt.");
        Status = DocumentSubmissionStatus.Rejected;
        ReviewedBy = actorId;
        ReviewedAt = now;
        RejectReason = RequireReason(reason);
    }

    private static string RequireReason(string? reason)
    {
        var r = reason?.Trim();
        return r is { Length: > 0 and <= MaxReasonLength }
            ? r
            : throw new BusinessRuleException("REASON_REQUIRED", $"Nhập lý do từ chối (tối đa {MaxReasonLength} ký tự).");
    }
}
