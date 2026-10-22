using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed class EbookReviewSearch : CrudSearch
{
    public Guid? EbookItemPublicId { get; set; }
}

public sealed record EbookReviewRequest(Guid EbookItemPublicId, int? Rating, string? DisplayName, string? Content, string? Email);

public sealed record EbookReviewDto(
    long Id, Guid PublicId, long EbookItemId, Guid EbookItemPublicId, string? ItemTitle, int? Rating, string? DisplayName, string? Content,
    string? Email, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, DateTimeOffset? ReviewedAt = null, DateTimeOffset? RejectedAt = null,
    string? RejectReason = null);

public sealed record RejectReviewRequest(string? Reason);

/// <summary>Đánh giá tài liệu số — thủ thư duyệt/sửa/xoá (LRC: EbookReviewController, quyền DIGITAL_REVIEWS). Bạn đọc tự gửi qua
/// <see cref="OpacReviews.SubmitAsync"/> (ẩn danh), không qua Add của resource này.</summary>
public sealed class EbookReviewResource(ICrudDbContext db, EbookReaderNotifier notifier, TimeProvider clock)
    : CrudResource<EbookReviewResource, EbookReview, EbookReviewSearch, EbookReviewRequest, EbookReviewDto>(db)
{
    protected override string EntityName => "Đánh giá tài liệu số";

    protected override string? Describe(EbookReview entity) => entity.DisplayName;

    protected override Expression<Func<EbookReview, EbookReviewDto>> Projection => x => new EbookReviewDto(
        x.Id, x.PublicId, x.EbookItemId, Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.PublicId).FirstOrDefault(),
        Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.Title).FirstOrDefault(), x.Rating, x.DisplayName, x.Content, x.Email,
        x.Status, x.CreatedAt, x.UpdatedAt, x.ReviewedAt, x.RejectedAt, x.RejectReason);

    // Duyệt/từ chối chỉ qua Approve/Reject (quyền approve, có lý do và thông báo) — ChangeStatus chung của CRUD chỉ cần quyền edit nên không dùng được.
    protected override Task OnSavingAsync(EbookReview entity, CrudChange change, CancellationToken ct) =>
        change == CrudChange.StatusChanged
            ? throw new BusinessRuleException("REVIEW_USE_APPROVE", "Dùng thao tác Duyệt / Từ chối đánh giá thay vì đổi trạng thái trực tiếp.")
            : Task.CompletedTask;

    protected override EbookReview Create(EbookReviewRequest request) => throw new NotSupportedException("Tạo qua CreateAsync (cần tra EbookItemId).");

    protected override async Task<EbookReview> CreateAsync(EbookReviewRequest request, CancellationToken ct) =>
        EbookReview.Submit(await ResolveItemIdAsync(request.EbookItemPublicId, ct), request.Rating, request.DisplayName, request.Content, request.Email);

    protected override void Update(EbookReview entity, EbookReviewRequest request) =>
        entity.Update(request.Rating, request.DisplayName, request.Content, request.Email);

    protected override IQueryable<EbookReview> Filter(IQueryable<EbookReview> query, EbookReviewSearch search)
    {
        if (search.EbookItemPublicId is { } itemPublicId)
        {
            var itemId = Db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId).Select(i => i.Id).FirstOrDefault();
            query = query.Where(x => x.EbookItemId == itemId);
        }
        return query;
    }

    protected override IOrderedQueryable<EbookReview> Order(IQueryable<EbookReview> query) => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);

    /// <summary>Duyệt (hiện công khai) và báo người gửi nếu có email. Gọi lại khi đã duyệt → không làm gì thêm, không báo lần hai.</summary>
    public async Task<EbookReviewDto> ApproveAsync(Guid publicId, long? actorId, CancellationToken ct)
    {
        var review = await Set.FirstOrDefaultAsync(x => x.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        if (review.Status != IHasStatus.Active)
        {
            review.Approve(actorId, clock.GetUtcNow());
            await NotifyAsync(review, EbookTemplates.ReviewApproved, null, ct);
            await Db.SaveChangesAsync(ct);
        }
        return await GetAsync(publicId, ct);
    }

    /// <summary>Từ chối — lý do bắt buộc, ghi lại; đánh giá đã duyệt cũng gỡ được. Báo người gửi nếu có email.</summary>
    public async Task<EbookReviewDto> RejectAsync(Guid publicId, RejectReviewRequest request, long? actorId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var review = await Set.FirstOrDefaultAsync(x => x.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        review.Reject(actorId, clock.GetUtcNow(), request.Reason);
        await NotifyAsync(review, EbookTemplates.ReviewRejected, review.RejectReason, ct);
        await Db.SaveChangesAsync(ct);
        return await GetAsync(publicId, ct);
    }

    // Publish TRƯỚC SaveChanges — NotificationRequested đi qua outbox, chỉ gửi được nếu cùng transaction lưu kèm thay đổi này.
    private async Task NotifyAsync(EbookReview review, string template, string? reason, CancellationToken ct)
    {
        var title = await Db.Set<EbookItem>().Where(i => i.Id == review.EbookItemId).Select(i => i.Title).FirstOrDefaultAsync(ct) ?? string.Empty;
        var data = new Dictionary<string, string> { ["title"] = title, ["reason"] = reason ?? string.Empty };
        await notifier.NotifyEmailAsync(review.Email, review.DisplayName, template, data, $"ebook-review:{template}:{review.Id}:{review.ReviewedAt:O}", ct);
    }

    private async Task<long> ResolveItemIdAsync(Guid itemPublicId, CancellationToken ct) =>
        await Db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId).Select(i => i.Id).FirstOrDefaultAsync(ct) is { } id and > 0
            ? id : throw new NotFoundException("Tài liệu số", itemPublicId);
}

/// <param name="Website">Ô bẫy (honeypot): người thật không thấy nên không điền; bot điền → bỏ qua lặng lẽ.</param>
public sealed record OpacReviewSubmission(int? Rating, string? DisplayName, string? Content, string? Email, string? Website = null);

public sealed record OpacReviewSummary(Guid PublicId, int? Rating, string? DisplayName, string? Content, DateTimeOffset CreatedAt);

/// <summary>Đánh giá trên OPAC — ẩn danh (LRC: PublicEbookReviewController, không gắn bạn đọc). Gửi mới luôn Chờ duyệt
/// (IHasStatus.Inactive); chỉ Đã duyệt (Active) mới hiện ở trang chi tiết.</summary>
public sealed class OpacReviews(ICrudDbContext db)
{
    private const int MaxPendingPerItem = 50;
    private const int MaxPendingPerEmail = 3;

    public async Task<OpacReviewSummary> SubmitAsync(Guid itemPublicId, OpacReviewSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var itemId = await db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId && i.Status == IHasStatus.Active).Select(i => i.Id).FirstOrDefaultAsync(ct);
        if (itemId == 0) throw new NotFoundException("Tài liệu số", itemPublicId);

        // Bot điền ô bẫy → giả vờ nhận để không dò được, nhưng không lưu gì.
        if (!string.IsNullOrWhiteSpace(submission.Website))
            return new OpacReviewSummary(Guid.CreateVersion7(), submission.Rating, submission.DisplayName, submission.Content, DateTimeOffset.UtcNow);

        // Chống spam: mỗi tài liệu tối đa 50 đánh giá chờ duyệt; cùng một email tối đa 3.
        var pending = db.Set<EbookReview>().Where(x => x.EbookItemId == itemId && x.Status == IHasStatus.Inactive && x.RejectedAt == null);
        if (await pending.CountAsync(ct) >= MaxPendingPerItem)
            throw new BusinessRuleException("REVIEW_TOO_MANY_PENDING", "Tài liệu này đang có quá nhiều đánh giá chờ duyệt, vui lòng thử lại sau.");
        var email = submission.Email?.Trim();
        if (!string.IsNullOrEmpty(email) && await pending.CountAsync(x => x.Email == email, ct) >= MaxPendingPerEmail)
            throw new BusinessRuleException("REVIEW_TOO_MANY_PENDING", "Bạn đã gửi đánh giá cho tài liệu này, vui lòng chờ thủ thư duyệt.");

        var review = EbookReview.Submit(itemId, submission.Rating, submission.DisplayName, submission.Content, submission.Email);
        db.Set<EbookReview>().Add(review);
        await db.SaveChangesAsync(ct);
        return new OpacReviewSummary(review.PublicId, review.Rating, review.DisplayName, review.Content, review.CreatedAt);
    }

    public async Task<IReadOnlyList<OpacReviewSummary>> PublishedAsync(Guid itemPublicId, CancellationToken ct)
    {
        var itemId = await db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId).Select(i => i.Id).FirstOrDefaultAsync(ct);
        if (itemId == 0) return [];
        return await db.Set<EbookReview>().AsNoTracking().Where(x => x.EbookItemId == itemId && x.Status == IHasStatus.Active)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OpacReviewSummary(x.PublicId, x.Rating, x.DisplayName, x.Content, x.CreatedAt))
            .ToListAsync(ct);
    }
}

public sealed record FavoriteSummary(Guid EbookItemPublicId, string Title, string? Author, string? CoverUrl, bool Free, DateTimeOffset CreatedAt);

/// <summary>Yêu thích tài liệu số — tự phục vụ của bạn đọc (claim, không nhận ReaderId từ client — xem quyết định #2 của plan
/// giai đoạn 2). Không có CRUD cho thủ thư, đúng khuôn holds tự phục vụ của circulation.</summary>
public sealed class ReaderFavorites(ICrudDbContext db)
{
    public async Task<IReadOnlyList<FavoriteSummary>> MyListAsync(Guid readerPublicId, CancellationToken ct) =>
        await db.Set<EbookFavorite>().AsNoTracking().Where(f => f.ReaderPublicId == readerPublicId)
            .OrderByDescending(f => f.CreatedAt)
            .Join(db.Set<EbookItem>().AsNoTracking(), f => f.EbookItemId, i => i.Id, (f, i) => new FavoriteSummary(i.PublicId, i.Title, i.Author, i.CoverUrl, i.Free, f.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Thêm yêu thích — vô hại nếu gọi lại khi đã có (idempotent, LRC's AddFavoriteAsync cũng trả bản ghi cũ nếu trùng).</summary>
    public async Task AddAsync(Guid itemPublicId, Guid readerPublicId, CancellationToken ct)
    {
        var itemId = await db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId && i.Status == IHasStatus.Active).Select(i => i.Id).FirstOrDefaultAsync(ct);
        if (itemId == 0) throw new NotFoundException("Tài liệu số", itemPublicId);
        if (await db.Set<EbookFavorite>().AnyAsync(f => f.EbookItemId == itemId && f.ReaderPublicId == readerPublicId, ct)) return;

        db.Set<EbookFavorite>().Add(EbookFavorite.Create(itemId, readerPublicId));
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid itemPublicId, Guid readerPublicId, CancellationToken ct)
    {
        var itemId = await db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId).Select(i => i.Id).FirstOrDefaultAsync(ct);
        var existing = itemId == 0 ? null : await db.Set<EbookFavorite>().FirstOrDefaultAsync(f => f.EbookItemId == itemId && f.ReaderPublicId == readerPublicId, ct);
        if (existing is null) return;
        db.Set<EbookFavorite>().Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
