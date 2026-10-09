using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookReviewRepository
    : BaseRepository<EbookReview, EbookReviewSearchRequest, EbookReviewRequest>,
      IEbookReviewRepository
{
    public EbookReviewRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<EbookReview> BuildQuery(EbookReviewSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);

        if (r.Status.HasValue && r.Status > 0)
            q = q.Where(x => x.Status == r.Status);

        if (r.Rating.HasValue)
            q = q.Where(x => x.Rating == r.Rating);

        if (r.ItemId.HasValue)
        {
            var itemId = _context.EbookItems
                .Where(x => x.PublicId == r.ItemId.Value)
                .Select(x => x.Id).FirstOrDefault();
            q = q.Where(x => x.ItemId == itemId);
        }

        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(EbookReviewRequest r, EbookReview e, long userId, bool isNew)
    {
        e.ItemId = r.ItemId; e.Rating = r.Rating; e.DisplayName = r.DisplayName;
        e.Content = r.Content; e.Email = r.Email; e.Status = r.Status;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(EbookReview e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookReview e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    private IQueryable<EbookReviewResponse> ProjectWithTitle(IQueryable<EbookReview> q)
        => from rv in q
           join item in _context.EbookItems on rv.ItemId equals item.Id into items
           from item in items.DefaultIfEmpty()
           join xml in _context.Set<EbookItemXml>() on item.Id equals xml.Id into xmls
           from xml in xmls.DefaultIfEmpty()
           join tenant in _context.Tenants on item.TenantId equals (long?)tenant.Id into tenants
           from tenant in tenants.DefaultIfEmpty()
           select new EbookReviewResponse
           {
               Id             = rv.Id,
               ItemId         = item != null ? item.PublicId : (Guid?)null,
               ItemTitle      = xml != null ? xml.Title : null,
               DisplayName    = rv.DisplayName,
               Rating         = rv.Rating,
               Content        = rv.Content,
               Email          = rv.Email,
               Status         = rv.Status,
               CreatedRowDate = rv.CreatedRowDate,
               PublicId       = rv.PublicId,
               TenantName     = tenant != null ? tenant.Name : null
           };

    // Review không có TenantId riêng đáng tin cậy (bạn đọc gửi đánh giá ngoài ngữ cảnh JWT admin) —
    // đơn vị "thật" của 1 review là đơn vị của EbookItem liên quan. Tài khoản thường vẫn bị ép theo
    // JWT như cũ; tài khoản đặc quyền giờ có thể chọn xem riêng 1 đơn vị (trước đây luôn thấy hết,
    // không có cách lọc) — theo đúng quy ước 3 nhánh (đơn vị đã chọn + dữ liệu dùng chung) ở nơi khác.
    private async Task<IQueryable<EbookReview>> ApplyItemTenantFilterAsync(IQueryable<EbookReview> q, Guid? requestTenantPublicId)
    {
        var jwtTenantId = GetCurrentTenantId();
        if (jwtTenantId != null && !IsReadOnlyPolicyUser())
        {
            var scopedItemIds = _context.EbookItems
                .Where(i => (i.TenantId == jwtTenantId || i.TenantId == null) && i.IsDelete != 2)
                .Select(i => i.Id);
            return q.Where(rv => rv.ItemId != null && scopedItemIds.Contains(rv.ItemId.Value));
        }

        var requestTenantId = await ResolveRequestTenantIdAsync(requestTenantPublicId);
        if (!requestTenantId.HasValue) return q;

        var tenantItemIds = _context.EbookItems
            .Where(i => (i.TenantId == requestTenantId || i.TenantId == null) && i.IsDelete != 2)
            .Select(i => i.Id);
        return q.Where(rv => rv.ItemId != null && tenantItemIds.Contains(rv.ItemId.Value));
    }

    public async Task<PagedResult<EbookReviewResponse>> SearchWithTitleAsync(EbookReviewSearchRequest request)
    {
        var baseQ = await ApplyItemTenantFilterAsync(BuildQuery(request), request.TenantId);

        var projected = ProjectWithTitle(baseQ);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            projected = projected.Where(x =>
                (x.ItemTitle != null && x.ItemTitle.Contains(kw))
                || (x.DisplayName != null && x.DisplayName.Contains(kw))
                || (x.Content != null && x.Content.Contains(kw)));
        }

        var total = await projected.CountAsync();
        var items = await projected
            .Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<EbookReviewResponse>
        {
            Items      = items,
            TotalCount = total,
            PageIndex  = request.PageIndex,
            PageSize   = request.PageSize
        };
    }

    public async Task<List<EbookReviewResponse>> SearchAllWithTitleAsync(EbookReviewSearchRequest request)
    {
        var baseQ = await ApplyItemTenantFilterAsync(BuildQuery(request), request.TenantId);
        return await ProjectWithTitle(baseQ).ToListAsync();
    }
}
