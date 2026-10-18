using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>
/// Đánh giá tài liệu số (LRC: EbookReview) — ẩn danh, không gắn bạn đọc (LRC's PublicEbookReviewRequest chỉ nhận
/// DisplayName/Email tự nhập, không có ReaderId). Status dùng đúng số của IHasStatus: Inactive=1 "Chờ duyệt" (mới gửi,
/// chưa hiện công khai), Active=2 "Đã duyệt" (hiện ở trang chi tiết) — đúng ý nghĩa LRC's Status 1/2.
/// </summary>
public sealed class EbookReview : TenantEntity, IHasStatus
{
    public const int MaxDisplayNameLength = 200;
    public const int MaxContentLength = 4000;
    public const int MaxEmailLength = 200;

    private EbookReview() { }

    public long EbookItemId { get; private set; }
    public int? Rating { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Content { get; private set; }
    public string? Email { get; private set; }
    public int Status { get; private set; } = IHasStatus.Inactive;

    public static EbookReview Submit(long ebookItemId, int? rating, string? displayName, string? content, string? email)
    {
        var review = new EbookReview { EbookItemId = ebookItemId };
        review.Update(rating, displayName, content, email);
        return review;
    }

    public void Update(int? rating, string? displayName, string? content, string? email)
    {
        Rating = rating is { } r and (< 1 or > 5) ? throw new BusinessRuleException("RATING_INVALID", "Số sao đánh giá phải từ 1 đến 5.") : rating;
        DisplayName = TextRules.Optional(displayName, MaxDisplayNameLength, "DISPLAY_NAME_INVALID", "Tên người đánh giá");
        Content = TextRules.Optional(content, MaxContentLength, "CONTENT_INVALID", "Nội dung đánh giá");
        Email = TextRules.Optional(email, MaxEmailLength, "EMAIL_INVALID", "Email");
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>
/// Yêu thích tài liệu số (LRC: EbookFavorite) — gắn bạn đọc qua claim, KHÔNG nhận ReaderId/Cardnumber từ request (khác LRC,
/// nơi client tự gửi id bạn đọc cho cả Add/Remove/Search — lỗ hổng cho phép giả mạo hộ người khác). Chỉ bạn đọc tự phục vụ,
/// không có CRUD cho thủ thư (giống holds của circulation).
/// </summary>
public sealed class EbookFavorite : TenantEntity
{
    private EbookFavorite() { }

    public long EbookItemId { get; private set; }
    public Guid ReaderPublicId { get; private set; }

    public static EbookFavorite Create(long ebookItemId, Guid readerPublicId) =>
        new() { EbookItemId = ebookItemId, ReaderPublicId = readerPublicId };
}
