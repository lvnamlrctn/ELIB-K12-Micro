using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Serials.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elib.Serials.Application;

public sealed class BindingSearch : CrudSearch
{
    public string? AccessionNo { get; set; }
    public string? VolumeTitle { get; set; }
    public long? StoreId { get; set; }
    public Guid? SubscriptionPublicId { get; set; }
}

public sealed record BindingIssueDto(Guid IssuePublicId, string Label, DateOnly? ReceivedDate, int Quantity);

public sealed record BindingDto(
    long Id, Guid PublicId, string AccessionNo, string VolumeTitle, Guid SubscriptionPublicId, string SubscriptionTitle, DateOnly BindingDate, long? StoreId,
    string? StoreName, string? Note, Guid? ItemPublicId, int IssueCount, IReadOnlyList<BindingIssueDto> Issues, DateTimeOffset CreatedAt);

/// <summary>
/// Lưu tập đóng (thêm khi <see cref="PublicId"/> null, ngược lại sửa). Thêm: tạo ĐKCB = <see cref="AccessionNo"/> ở holdings gắn biểu ghi của đăng ký.
/// Sửa: đổi được nhan đề tập, ngày, ghi chú và danh sách kỳ; số ĐKCB và kho đã có ĐKCB nên khoá.
/// </summary>
public sealed record BindingSaveRequest(
    Guid SubscriptionPublicId, IReadOnlyList<Guid> IssueIds, Guid? PublicId = null, string? AccessionNo = null, string? VolumeTitle = null, DateOnly? BindingDate = null,
    long? StoreId = null, string? StoreName = null, string? Note = null);

/// <summary>Tập đóng (monolith: SerialBindingService, quyền SERIAL_BINDING) + ĐKCB thật ở holdings.</summary>
public sealed partial class BindingService(ICrudDbContext db, IHoldingsSerialItems holdings, ITenantContext tenant, TimeProvider clock, ILogger<BindingService> logger)
{
    public const int MaxPageSize = 500;

    private DateOnly Today => VietnamDate.Of(clock.GetUtcNow());

    public async Task<CrudPage<BindingDto>> SearchAsync(BindingSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = db.Set<Binding>().AsNoTracking();
        if (search.AccessionNo?.Trim() is { Length: > 0 } acc) query = query.Where(b => b.AccessionNo.StartsWith(acc));
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        if (search.VolumeTitle?.Trim().ToLowerInvariant() is { Length: > 0 } title) query = query.Where(b => b.VolumeTitle.ToLower().Contains(title));
        if (search.Term is { } term) query = query.Where(b => b.VolumeTitle.ToLower().Contains(term) || b.AccessionNo.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        if (search.StoreId is { } store) query = query.Where(b => b.StoreId == store);
        if (search.SubscriptionPublicId is { } sub) query = query.Where(b => db.Set<Subscription>().Any(s => s.Id == b.SubscriptionId && s.PublicId == sub));
        var page = Math.Max(1, search.PageIndex);
        var size = Math.Clamp(search.PageSize, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(b => b.BindingDate).ThenByDescending(b => b.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<BindingDto>(await ToDtosAsync(rows, withIssues: false, ct), total, page, size);
    }

    public async Task<BindingDto> GetAsync(Guid publicId, CancellationToken ct)
    {
        var binding = await db.Set<Binding>().AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == publicId, ct) ?? throw new NotFoundException("Tập đóng", publicId);
        return (await ToDtosAsync([binding], withIssues: true, ct))[0];
    }

    private async Task<IReadOnlyList<BindingDto>> ToDtosAsync(IReadOnlyList<Binding> bindings, bool withIssues, CancellationToken ct)
    {
        var ids = bindings.Select(b => b.Id).ToList();
        var subs = bindings.Select(b => b.SubscriptionId).Distinct().ToList();
        var subRows = await db.Set<Subscription>().AsNoTracking().Where(s => subs.Contains(s.Id)).Select(s => new { s.Id, s.PublicId, s.Title }).ToListAsync(ct);
        var issueRows = await (from bi in db.Set<BindingIssue>().AsNoTracking()
                               join i in db.Set<SerialIssue>().AsNoTracking() on bi.IssueId equals i.Id
                               where ids.Contains(bi.BindingId)
                               orderby i.PlannedDate, i.Id
                               select new { bi.BindingId, i.PublicId, i.Label, i.ReceivedDate, i.Quantity }).ToListAsync(ct);
        return [.. bindings.Select(b =>
        {
            var sub = subRows.FirstOrDefault(s => s.Id == b.SubscriptionId);
            var issues = issueRows.Where(r => r.BindingId == b.Id).Select(r => new BindingIssueDto(r.PublicId, r.Label, r.ReceivedDate, r.Quantity)).ToList();
            return new BindingDto(b.Id, b.PublicId, b.AccessionNo, b.VolumeTitle, sub?.PublicId ?? Guid.Empty, sub?.Title ?? "", b.BindingDate, b.StoreId, b.StoreName,
                b.Note, b.ItemPublicId, issues.Count, withIssues ? issues : [], b.CreatedAt);
        })];
    }

    public async Task<BindingDto> SaveAsync(BindingSaveRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sub = await db.Set<Subscription>().AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == request.SubscriptionPublicId, ct)
            ?? throw new NotFoundException("Đăng ký báo", request.SubscriptionPublicId);
        if (sub.BibMfn is not { } mfn)
            throw new BusinessRuleException("SERIAL_BIB_REQUIRED", "Liên kết đăng ký với một biểu ghi thư mục trước khi đóng tập.");

        Binding? binding = request.PublicId is { } id
            ? await db.Set<Binding>().FirstOrDefaultAsync(b => b.PublicId == id, ct) ?? throw new NotFoundException("Tập đóng", id)
            : null;
        var issues = await ValidIssuesAsync(sub, request.IssueIds, binding?.Id, ct);
        var data = new BindingData(request.AccessionNo ?? binding?.AccessionNo, request.VolumeTitle, sub.Id, request.BindingDate, request.StoreId ?? binding?.StoreId,
            request.StoreName ?? binding?.StoreName, request.Note);

        if (binding is null)
        {
            var created = Binding.Create(Guid.CreateVersion7(), data, Today);
            if (await db.Set<Binding>().AnyAsync(b => b.AccessionNo == created.AccessionNo, ct))
                throw new ConflictException("ACCESSION_EXISTS", $"Số ĐKCB '{created.AccessionNo}' đã có tập đóng khác.");
            var tenantId = tenant.RequireTenantId();
            var item = await holdings.RegisterAsync(tenantId, new SerialItemRegisterRequest(created.PublicId, mfn, created.AccessionNo, created.StoreId, created.Note), ct);
            try
            {
                created.AttachItem(item.PublicId);
                db.Set<Binding>().Add(created);
                await db.SaveChangesAsync(ct);
                db.Set<BindingIssue>().AddRange(issues.Select(i => BindingIssue.Create(created.Id, i.Id)));
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Holdings đã có ĐKCB mà serials chưa lưu được → gỡ bù (idempotent); gỡ lỗi thì để holdings tự dọn khi thử lại cùng BindingPublicId.
                await CompensateAsync(tenantId, created.PublicId, ex);
                throw;
            }
            return await GetAsync(created.PublicId, ct);
        }

        var accession = (request.AccessionNo ?? binding.AccessionNo).Trim();
        if (!string.Equals(accession, binding.AccessionNo, StringComparison.Ordinal) || (request.StoreId is not null && request.StoreId != binding.StoreId))
            throw new BusinessRuleException("BINDING_ACCESSION_LOCKED", "Số ĐKCB và kho đã gắn với bản sách ở kho, không đổi được — xoá tập rồi đóng lại.");
        binding.Apply(data with { AccessionNo = binding.AccessionNo }, Today);
        var current = await db.Set<BindingIssue>().Where(b => b.BindingId == binding.Id).ToListAsync(ct);
        var wanted = issues.Select(i => i.Id).ToHashSet();
        db.Set<BindingIssue>().RemoveRange(current.Where(c => !wanted.Contains(c.IssueId)));
        db.Set<BindingIssue>().AddRange(wanted.Where(w => current.All(c => c.IssueId != w)).Select(w => BindingIssue.Create(binding.Id, w)));
        await db.SaveChangesAsync(ct);
        return await GetAsync(binding.PublicId, ct);
    }

    /// <summary>Kỳ chọn phải tồn tại, cùng đăng ký, đã nhận và chưa thuộc tập khác (trừ chính tập đang sửa).</summary>
    private async Task<List<SerialIssue>> ValidIssuesAsync(Subscription sub, IReadOnlyList<Guid>? publicIds, long? bindingId, CancellationToken ct)
    {
        var ids = publicIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0) throw new BusinessRuleException("BINDING_ISSUES_REQUIRED", "Chọn ít nhất một kỳ đã nhận để đóng tập.");
        var issues = await db.Set<SerialIssue>().AsNoTracking().Where(i => ids.Contains(i.PublicId)).ToListAsync(ct);
        if (issues.Count != ids.Count) throw new BusinessRuleException("ISSUE_NOT_FOUND", "Có kỳ không tồn tại.", 404);
        if (issues.Any(i => i.SubscriptionId != sub.Id)) throw new BusinessRuleException("ISSUE_SUBSCRIPTION_MISMATCH", "Các kỳ phải thuộc cùng một đăng ký.");
        if (issues.Any(i => i.Status != IssueStatus.Received)) throw new BusinessRuleException("ISSUE_NOT_RECEIVED", "Chỉ đóng tập các kỳ đã nhận.");
        var issueIds = issues.Select(i => i.Id).ToList();
        if (await db.Set<BindingIssue>().AnyAsync(b => issueIds.Contains(b.IssueId) && b.BindingId != bindingId, ct))
            throw new ConflictException("ISSUE_ALREADY_BOUND", "Có kỳ đã thuộc một tập đóng khác.");
        return issues;
    }

    private async Task CompensateAsync(long tenantId, Guid bindingPublicId, Exception cause)
    {
        try
        {
            await holdings.RemoveAsync(tenantId, bindingPublicId, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCompensationFailed(logger, ex, bindingPublicId, cause.Message);
        }
    }

    /// <summary>Xoá tập: gỡ ĐKCB ở holdings trước (holdings chặn nếu bản đã lưu thông), rồi xoá mềm tập và các dòng kỳ.</summary>
    public async Task DeleteAsync(Guid publicId, CancellationToken ct)
    {
        var binding = await db.Set<Binding>().FirstOrDefaultAsync(b => b.PublicId == publicId, ct) ?? throw new NotFoundException("Tập đóng", publicId);
        if (binding.ItemPublicId is not null) await holdings.RemoveAsync(tenant.RequireTenantId(), binding.PublicId, ct);
        db.Set<BindingIssue>().RemoveRange(await db.Set<BindingIssue>().Where(b => b.BindingId == binding.Id).ToListAsync(ct));
        db.Set<Binding>().Remove(binding);
        await db.SaveChangesAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Không gỡ bù được ĐKCB của tập đóng {BindingPublicId} ở holdings sau lỗi: {Cause}")]
    private static partial void LogCompensationFailed(ILogger logger, Exception exception, Guid bindingPublicId, string cause);
}
