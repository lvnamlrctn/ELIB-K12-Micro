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
    string? Email, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Đánh giá tài liệu số — thủ thư duyệt/sửa/xoá (LRC: EbookReviewController, quyền DIGITAL_REVIEWS). Bạn đọc tự gửi qua
/// <see cref="OpacReviews.SubmitAsync"/> (ẩn danh), không qua Add của resource này.</summary>
public sealed class EbookReviewResource(ICrudDbContext db)
    : CrudResource<EbookReviewResource, EbookReview, EbookReviewSearch, EbookReviewRequest, EbookReviewDto>(db)
{
    protected override string EntityName => "Đánh giá tài liệu số";

    protected override string? Describe(EbookReview entity) => entity.DisplayName;

    protected override Expression<Func<EbookReview, EbookReviewDto>> Projection => x => new EbookReviewDto(
        x.Id, x.PublicId, x.EbookItemId, Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.PublicId).FirstOrDefault(),
        Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.Title).FirstOrDefault(), x.Rating, x.DisplayName, x.Content, x.Email,
        x.Status, x.CreatedAt, x.UpdatedAt);

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

    private async Task<long> ResolveItemIdAsync(Guid itemPublicId, CancellationToken ct) =>
        await Db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId).Select(i => i.Id).FirstOrDefaultAsync(ct) is { } id and > 0
            ? id : throw new NotFoundException("Tài liệu số", itemPublicId);
}

public sealed record OpacReviewSubmission(int? Rating, string? DisplayName, string? Content, string? Email);

public sealed record OpacReviewSummary(Guid PublicId, int? Rating, string? DisplayName, string? Content, DateTimeOffset CreatedAt);

/// <summary>Đánh giá trên OPAC — ẩn danh (LRC: PublicEbookReviewController, không gắn bạn đọc). Gửi mới luôn Chờ duyệt
/// (IHasStatus.Inactive); chỉ Đã duyệt (Active) mới hiện ở trang chi tiết.</summary>
public sealed class OpacReviews(ICrudDbContext db)
{
    public async Task<OpacReviewSummary> SubmitAsync(Guid itemPublicId, OpacReviewSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var itemId = await db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId && i.Status == IHasStatus.Active).Select(i => i.Id).FirstOrDefaultAsync(ct);
        if (itemId == 0) throw new NotFoundException("Tài liệu số", itemPublicId);

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
