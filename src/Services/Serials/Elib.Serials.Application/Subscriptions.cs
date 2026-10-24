using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Serials.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Serials.Application;

public sealed class SubscriptionSearch : CrudSearch
{
    public long? PublicationTypeId { get; set; }
    public long? FrequencyId { get; set; }
    public bool? Approved { get; set; }
}

/// <summary>
/// Đăng ký đặt báo. <see cref="BibPublicId"/> chọn biểu ghi thư mục (nhan đề lấy từ biểu ghi nếu bỏ trống); không chọn thì khai <see cref="Title"/> +
/// <see cref="Issn"/> — dùng được cho khai báo/nhận kỳ, còn duyệt và đóng tập cần biểu ghi.
/// </summary>
public sealed record SubscriptionRequest(
    string? Title = null, string? Issn = null, Guid? BibPublicId = null, long? PublicationTypeId = null, long? FrequencyId = null, long? PatternId = null,
    long? StoreId = null, string? StoreName = null, Guid? SupplierPublicId = null, string? SupplierName = null, DateOnly? StartsOn = null, DateOnly? EndsOn = null,
    DateOnly? FirstIssueDate = null, int? StartX = null, int? StartY = null, int? StartZ = null, string? CallNumber = null, string? PublicNote = null,
    string? InternalNote = null, int GraceDays = 0, int Status = SubscriptionStatus.Active);

public sealed record SubscriptionDto(
    long Id, Guid PublicId, Guid? BibPublicId, long? BibMfn, string Title, string? Issn, long? PublicationTypeId, string? PublicationTypeName, long? FrequencyId,
    string? FrequencyName, long? PatternId, string? PatternName, long? StoreId, string? StoreName, Guid? SupplierPublicId, string? SupplierName, DateOnly? StartsOn,
    DateOnly? EndsOn, DateOnly? FirstIssueDate, int? StartX, int? StartY, int? StartZ, int? LastX, int? LastY, int? LastZ, string? CallNumber, string? PublicNote,
    string? InternalNote, int GraceDays, int Status, bool Approved, DateTimeOffset? ApprovedAt, int IssueCount, int ReceivedCount, int MissingCount,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Đăng ký đặt báo (monolith: SerialController, quyền SUBSCRIPTIONS). Đã duyệt thì khoá sửa/xoá; duyệt cần biểu ghi thư mục.</summary>
public sealed class SubscriptionResource(ICrudDbContext db, BibSnapshots bibs, ICurrentActor actor, TimeProvider clock)
    : CrudResource<SubscriptionResource, Subscription, SubscriptionSearch, SubscriptionRequest, SubscriptionDto>(db)
{
    protected override string EntityName => "Đăng ký báo";

    protected override string? Describe(Subscription entity) => entity.Title;

    // Số kỳ "thiếu" = kỳ chưa nhận mà đã khiếu nại/báo thiếu, hoặc quá ngày dự kiến (như thống kê của monolith).
    protected override Expression<Func<Subscription, SubscriptionDto>> Projection
    {
        get
        {
            var today = VietnamDate.Of(clock.GetUtcNow());
            return x => new SubscriptionDto(
                x.Id, x.PublicId, x.BibPublicId, x.BibMfn, x.Title, x.Issn, x.PublicationTypeId,
                Db.Set<PublicationType>().Where(t => t.Id == x.PublicationTypeId).Select(t => t.Name).FirstOrDefault(), x.FrequencyId,
                Db.Set<Frequency>().Where(f => f.Id == x.FrequencyId).Select(f => f.Name).FirstOrDefault(), x.PatternId,
                Db.Set<NumberingPattern>().Where(p => p.Id == x.PatternId).Select(p => p.Name).FirstOrDefault(), x.StoreId, x.StoreName, x.SupplierPublicId,
                x.SupplierName, x.StartsOn, x.EndsOn, x.FirstIssueDate, x.StartX, x.StartY, x.StartZ, x.LastX, x.LastY, x.LastZ, x.CallNumber, x.PublicNote,
                x.InternalNote, x.GraceDays, x.Status, x.Approved, x.ApprovedAt,
                Db.Set<SerialIssue>().Count(i => i.SubscriptionId == x.Id),
                Db.Set<SerialIssue>().Count(i => i.SubscriptionId == x.Id && i.Status == IssueStatus.Received),
                Db.Set<SerialIssue>().Count(i => i.SubscriptionId == x.Id && i.Status != IssueStatus.Received
                    && (i.Status == IssueStatus.Claimed || i.Status == IssueStatus.Missing || (i.PlannedDate != null && i.PlannedDate < today))),
                x.CreatedAt, x.UpdatedAt);
        }
    }

    protected override Subscription Create(SubscriptionRequest request) => throw new NotSupportedException("Dùng CreateAsync.");

    protected override void Update(Subscription entity, SubscriptionRequest request) => throw new NotSupportedException("Dùng UpdateAsync.");

    protected override async Task<Subscription> CreateAsync(SubscriptionRequest request, CancellationToken ct) =>
        Subscription.Create(await DataAsync(request, ct));

    protected override async Task UpdateAsync(Subscription entity, SubscriptionRequest request, CancellationToken ct)
    {
        entity.EnsureEditable();
        entity.Update(await DataAsync(request, ct));
    }

    private async Task<SubscriptionData> DataAsync(SubscriptionRequest r, CancellationToken ct)
    {
        string? title = r.Title;
        Guid? bibPublicId = null;
        long? mfn = null;
        if (r.BibPublicId is { } bibId)
        {
            var bib = await bibs.RequireAsync(bibId, ct);
            bibPublicId = bib.BibPublicId;
            mfn = bib.Mfn;
            if (string.IsNullOrWhiteSpace(title)) title = bib.Title;
        }
        if (r.PublicationTypeId is { } type && !await Db.Set<PublicationType>().AnyAsync(t => t.Id == type, ct))
            throw new BusinessRuleException("TYPE_NOT_FOUND", "Loại ấn phẩm không tồn tại.", 404);
        if (r.FrequencyId is { } freq && !await Db.Set<Frequency>().AnyAsync(f => f.Id == freq, ct))
            throw new BusinessRuleException("FREQUENCY_NOT_FOUND", "Tần suất không tồn tại.", 404);
        if (r.PatternId is { } pattern && !await Db.Set<NumberingPattern>().AnyAsync(p => p.Id == pattern, ct))
            throw new BusinessRuleException("PATTERN_NOT_FOUND", "Mẫu đánh số không tồn tại.", 404);
        return new SubscriptionData(title, r.Issn, bibPublicId, mfn, r.PublicationTypeId, r.FrequencyId, r.PatternId, r.StoreId, r.StoreName, r.SupplierPublicId,
            r.SupplierName, r.StartsOn, r.EndsOn, r.FirstIssueDate, r.StartX, r.StartY, r.StartZ, r.CallNumber, r.PublicNote, r.InternalNote, r.GraceDays, r.Status);
    }

    protected override IQueryable<Subscription> Filter(IQueryable<Subscription> query, SubscriptionSearch search)
    {
        if (search.PublicationTypeId is { } type) query = query.Where(x => x.PublicationTypeId == type);
        if (search.FrequencyId is { } freq) query = query.Where(x => x.FrequencyId == freq);
        if (search.Approved is { } approved) query = query.Where(x => x.Approved == approved);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Title.ToLower().Contains(term) || (x.Issn != null && x.Issn.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Subscription> Order(IQueryable<Subscription> query) => query.OrderBy(x => x.Title).ThenBy(x => x.Id);

    protected override Task EnsureDeletableAsync(Subscription entity, CancellationToken ct)
    {
        entity.EnsureEditable();
        return Task.CompletedTask;
    }

    protected override async Task OnSavingAsync(Subscription entity, CrudChange change, CancellationToken ct)
    {
        if (change != CrudChange.Deleted) return;
        if (await Db.Set<Binding>().AnyAsync(b => b.SubscriptionId == entity.Id, ct))
            throw new ConflictException("SUBSCRIPTION_HAS_BINDINGS", "Đăng ký đã có tập đóng, không xoá được.");
        Db.Set<SerialIssue>().RemoveRange(await Db.Set<SerialIssue>().Where(i => i.SubscriptionId == entity.Id).ToListAsync(ct));
    }

    /// <summary>Gắn/đổi biểu ghi thư mục của đăng ký chưa duyệt (chọn từ danh sách tìm biểu ghi).</summary>
    public async Task<SubscriptionDto> LinkBibAsync(Guid publicId, Guid bibPublicId, CancellationToken ct)
    {
        var entity = await LoadAsync(publicId, ct);
        var bib = await bibs.RequireAsync(bibPublicId, ct);
        entity.LinkBib(bib.BibPublicId, bib.Mfn);
        await AuditAsync(entity, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>Duyệt một chiều: sau đó khoá sửa/xoá. Cần biểu ghi thư mục (<c>SERIAL_BIB_REQUIRED</c>).</summary>
    public async Task<SubscriptionDto> ApproveAsync(Guid publicId, CancellationToken ct)
    {
        var entity = await LoadAsync(publicId, ct);
        entity.Approve(clock.GetUtcNow(), actor.Id);
        await AuditAsync(entity, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }
}
