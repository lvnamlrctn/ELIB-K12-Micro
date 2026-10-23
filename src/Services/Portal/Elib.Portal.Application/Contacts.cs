using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Portal.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Portal.Application;

public sealed record ContactGroupRequest(string Name, string? Email = null, string? Description = null, string? Language = null, int SortOrder = 0, int Status = IHasStatus.Active);

public sealed record ContactGroupDto(
    long Id, Guid PublicId, string Name, string? Email, string? Description, string? Language, int SortOrder, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Nhóm liên hệ (LRC: ContactGroupController, quyền PORTAL_CONTACTS).</summary>
public sealed class ContactGroupResource(ICrudDbContext db) : CrudResource<ContactGroupResource, ContactGroup, CrudSearch, ContactGroupRequest, ContactGroupDto>(db)
{
    protected override string EntityName => "Nhóm liên hệ";

    protected override string? Describe(ContactGroup entity) => entity.Name;

    protected override Expression<Func<ContactGroup, ContactGroupDto>> Projection => x =>
        new ContactGroupDto(x.Id, x.PublicId, x.Name, x.Email, x.Description, x.Language, x.SortOrder, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override ContactGroup Create(ContactGroupRequest r) => ContactGroup.Create(r.Name, r.Email, r.Description, r.Language, r.SortOrder, r.Status);

    protected override void Update(ContactGroup entity, ContactGroupRequest r) => entity.Update(r.Name, r.Email, r.Description, r.Language, r.SortOrder, r.Status);

    protected override async Task EnsureDeletableAsync(ContactGroup entity, CancellationToken ct)
    {
        if (await Db.Set<Contact>().AnyAsync(c => c.ContactGroupId == entity.Id, ct)) throw new ConflictException("CONTACT_GROUP_HAS_CONTACTS", $"Nhóm '{entity.Name}' còn liên hệ.");
    }

    protected override IQueryable<ContactGroup> Filter(IQueryable<ContactGroup> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<ContactGroup> Order(IQueryable<ContactGroup> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);
}

public sealed class ContactSearch : CrudSearch
{
    public long? ContactGroupId { get; set; }
}

public sealed record ContactRequest(string? HandleNote);

public sealed record ContactDto(
    long Id, Guid PublicId, long? ContactGroupId, string? ContactGroupName, string Name, string Email, string? Mobile, string? Address, string? Title, string Content, int Status,
    DateTimeOffset? HandledAt, string? HandleNote, DateTimeOffset CreatedAt);

/// <summary>
/// Liên hệ từ OPAC (LRC: ContactController, quyền PORTAL_CONTACTS). Thủ thư xem, ghi chú xử lý/đánh dấu đã xử lý, xoá; không thêm hộ (chỉ sinh từ OPAC).
/// </summary>
public sealed class ContactResource(ICrudDbContext db, TimeProvider clock) : CrudResource<ContactResource, Contact, ContactSearch, ContactRequest, ContactDto>(db)
{
    protected override string EntityName => "Liên hệ";

    protected override string? Describe(Contact entity) => entity.Name;

    protected override Expression<Func<Contact, ContactDto>> Projection => x => new ContactDto(
        x.Id, x.PublicId, x.ContactGroupId, Db.Set<ContactGroup>().Where(g => g.Id == x.ContactGroupId).Select(g => g.Name).FirstOrDefault(), x.Name, x.Email, x.Mobile, x.Address,
        x.Title, x.Content, x.Status, x.HandledAt, x.HandleNote, x.CreatedAt);

    protected override Contact Create(ContactRequest r) => throw new BusinessRuleException("CONTACT_FROM_OPAC_ONLY", "Liên hệ do bạn đọc gửi từ OPAC, không thêm hộ.");

    protected override void Update(Contact entity, ContactRequest r) => throw new BusinessRuleException("CONTACT_USE_HANDLE", "Dùng thao tác Đánh dấu đã xử lý.");

    /// <summary>Đánh dấu đã xử lý kèm ghi chú. Gọi lại → cập nhật ghi chú, giữ lần xử lý đầu.</summary>
    public async Task<ContactDto> HandleAsync(Guid publicId, long? actorId, ContactRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var contact = await Set.FirstOrDefaultAsync(x => x.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        contact.MarkHandled(contact.HandledBy ?? actorId, contact.HandledAt ?? clock.GetUtcNow(), request.HandleNote);
        await Db.SaveChangesAsync(ct);
        return await GetAsync(publicId, ct);
    }

    protected override IQueryable<Contact> Filter(IQueryable<Contact> query, ContactSearch search)
    {
        if (search.ContactGroupId is { } group) query = query.Where(x => x.ContactGroupId == group);
        if (search.Status is { } status) query = query.Where(x => x.Status == status);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Email.ToLower().Contains(term) || (x.Title != null && x.Title.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Contact> Order(IQueryable<Contact> query) => query.OrderBy(x => x.Status).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
}

public sealed record CounterSummary(long Total, long Today, long Last7Days);

public sealed record CounterDayPoint(DateOnly Day, long Hits);

/// <summary>Bộ đếm truy cập (LRC: Counter / PublicCounter): cộng dồn theo ngày giờ Việt Nam, không lưu IP.</summary>
public sealed class VisitCounter(ICrudDbContext db, TimeProvider clock)
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public async Task<CounterSummary> HitAsync(CancellationToken ct)
    {
        var today = Today();
        var row = await db.Set<CounterDay>().FirstOrDefaultAsync(c => c.Day == today, ct);
        if (row is null) db.Set<CounterDay>().Add(row = CounterDay.For(today));
        row.Add();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Hai lượt đầu ngày cùng lúc: một bên tạo dòng ngày trước — bên kia bỏ qua lượt này thay vì lỗi (con số chỉ là ước lượng).
        }
        return await SummaryAsync(ct);
    }

    public async Task<CounterSummary> SummaryAsync(CancellationToken ct)
    {
        var today = Today();
        var days = await db.Set<CounterDay>().AsNoTracking().Select(c => new { c.Day, c.Hits }).ToListAsync(ct);
        return new CounterSummary(days.Sum(d => d.Hits), days.Where(d => d.Day == today).Sum(d => d.Hits), days.Where(d => d.Day > today.AddDays(-7)).Sum(d => d.Hits));
    }

    /// <summary>Lượt truy cập từng ngày lịch Việt Nam, đủ <paramref name="days"/> ngày gần nhất (ngày không có lượt = 0), cũ → mới.</summary>
    public async Task<IReadOnlyList<CounterDayPoint>> DaysAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 366);
        var today = Today();
        var from = today.AddDays(1 - days);
        var hits = (await db.Set<CounterDay>().AsNoTracking().Where(c => c.Day >= from && c.Day <= today).Select(c => new { c.Day, c.Hits }).ToListAsync(ct))
            .ToDictionary(c => c.Day, c => c.Hits);
        return [.. Enumerable.Range(0, days).Select(i => from.AddDays(i)).Select(d => new CounterDayPoint(d, hits.GetValueOrDefault(d)))];
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(Vietnam).DateTime);
}
