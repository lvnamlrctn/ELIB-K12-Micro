using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Serials.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Serials.Application;

public sealed class IssueSearch : CrudSearch
{
    public Guid? SubscriptionPublicId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>true = chỉ kỳ đã vào tập đóng, false = chỉ kỳ chưa đóng tập.</summary>
    public bool? Bound { get; set; }
}

public sealed record IssueDto(
    long Id, Guid PublicId, long SubscriptionId, Guid SubscriptionPublicId, string SubscriptionTitle, string Label, int? X, int? Y, int? Z, bool IsSpecial, bool IsMerged,
    int Status, int Quantity, DateOnly? PlannedDate, DateOnly? ReceivedDate, DateTimeOffset? ClaimDate, int ClaimCount, string? Note, bool Bound, DateTimeOffset CreatedAt);

/// <summary>Thêm/sửa/nhận một kỳ. Trường null giữ giá trị cũ khi sửa. X/Y/Z trùng một kỳ đã có (không ghép số) → cập nhật kỳ đó.</summary>
public sealed record IssueSaveRequest(
    Guid SubscriptionPublicId, Guid? PublicId = null, string? Label = null, int? X = null, int? Y = null, int? Z = null, bool IsSpecial = false, int? Quantity = null,
    DateOnly? PlannedDate = null, DateOnly? ReceivedDate = null, int? Status = null, string? Note = null);

public sealed record IssueReceiveRequest(Guid PublicId, DateOnly? ReceivedDate = null, int? Quantity = null);

public sealed record IssueClaimRequest(Guid PublicId);

public sealed record IssuePredictRequest(Guid SubscriptionPublicId, int Count = 10);

/// <summary>Kết quả sinh kỳ dự kiến: số kỳ mới, số số đã có phải bỏ qua, danh sách kỳ mới.</summary>
public sealed record IssuePredictResult(int Generated, int Skipped, IReadOnlyList<IssueDto> Issues);

public sealed record IssueMergeRequest(Guid SubscriptionPublicId, IReadOnlyList<Guid> IssueIds, string? Label = null, DateOnly? ReceivedDate = null, string? Note = null);

public sealed record IssueStatistics(int Total, int Received, int Claimed, int Missing, int Late, DateOnly? LastReceivedDate);

/// <summary>
/// Kỳ báo (monolith: SerialIssueService): sinh kỳ dự kiến theo tần suất + mẫu đánh số, nhận kỳ, khiếu nại, ghép số (một transaction), xoá, thống kê.
/// Quyền SERIAL_ISSUES (xem danh sách nhận kỳ cũng cho SERIAL_RECEIPTS).
/// </summary>
public sealed class IssueService(ICrudDbContext db, TimeProvider clock)
{
    public const int MaxPredict = 366;
    public const int MaxPageSize = 500;

    private DateOnly Today => VietnamDate.Of(clock.GetUtcNow());

    /// <summary>Kỳ + đăng ký → DTO. Sắp xếp (theo ngày dự kiến) làm SAU phép nối trên dữ liệu thật — sắp ở truy vấn con trước khi nối không đảm bảo thứ tự.</summary>
    private IQueryable<IssueDto> Dtos(IQueryable<SerialIssue> issues, bool byPlanned = false)
    {
        var joined = from i in issues
                     join s in Db.Set<Subscription>().AsNoTracking() on i.SubscriptionId equals s.Id
                     select new { Issue = i, Sub = s };
        var ordered = byPlanned
            ? joined.OrderBy(x => x.Issue.PlannedDate == null).ThenBy(x => x.Issue.PlannedDate).ThenBy(x => x.Issue.SortOrder).ThenBy(x => x.Issue.Id)
            : joined.OrderBy(x => x.Issue.Id);
        return ordered.Select(x => new IssueDto(x.Issue.Id, x.Issue.PublicId, x.Issue.SubscriptionId, x.Sub.PublicId, x.Sub.Title, x.Issue.Label, x.Issue.X, x.Issue.Y,
            x.Issue.Z, x.Issue.IsSpecial, x.Issue.IsMerged, x.Issue.Status, x.Issue.Quantity, x.Issue.PlannedDate, x.Issue.ReceivedDate, x.Issue.ClaimDate,
            x.Issue.ClaimCount, x.Issue.Note, Db.Set<BindingIssue>().Any(b => b.IssueId == x.Issue.Id), x.Issue.CreatedAt));
    }

    private ICrudDbContext Db => db;

    public async Task<CrudPage<IssueDto>> SearchAsync(IssueSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = Db.Set<SerialIssue>().AsNoTracking();
        if (search.SubscriptionPublicId is { } sub) query = query.Where(i => Db.Set<Subscription>().Any(s => s.Id == i.SubscriptionId && s.PublicId == sub));
        if (search.Status is { } status) query = query.Where(i => i.Status == status);
        if (search.From is { } from) query = query.Where(i => (i.ReceivedDate ?? i.PlannedDate) >= from);
        if (search.To is { } to) query = query.Where(i => (i.ReceivedDate ?? i.PlannedDate) <= to);
        if (search.Bound is { } bound) query = bound
            ? query.Where(i => Db.Set<BindingIssue>().Any(b => b.IssueId == i.Id))
            : query.Where(i => !Db.Set<BindingIssue>().Any(b => b.IssueId == i.Id));
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(i => i.Label.ToLower().Contains(term) || Db.Set<Subscription>().Any(s => s.Id == i.SubscriptionId && s.Title.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        var page = Math.Max(1, search.PageIndex);
        var size = Math.Clamp(search.PageSize, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var ordered = Dtos(query, byPlanned: true);
        var items = await ordered.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<IssueDto>(items, total, page, size);
    }

    public async Task<IssueDto> GetAsync(Guid publicId, CancellationToken ct) =>
        await Dtos(Db.Set<SerialIssue>().AsNoTracking().Where(i => i.PublicId == publicId)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Kỳ báo", publicId);

    private async Task<Subscription> RequireSubscriptionAsync(Guid publicId, CancellationToken ct) =>
        await Db.Set<Subscription>().FirstOrDefaultAsync(s => s.PublicId == publicId, ct) ?? throw new NotFoundException("Đăng ký báo", publicId);

    private async Task<SerialIssue> RequireIssueAsync(Guid publicId, CancellationToken ct) =>
        await Db.Set<SerialIssue>().FirstOrDefaultAsync(i => i.PublicId == publicId, ct) ?? throw new NotFoundException("Kỳ báo", publicId);

    private async Task<NumberingPattern?> PatternOfAsync(Subscription s, CancellationToken ct) =>
        s.PatternId is { } id ? await Db.Set<NumberingPattern>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) : null;

    /// <summary>Thêm / sửa / nhận kỳ.</summary>
    public async Task<IssueDto> SaveAsync(IssueSaveRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sub = await RequireSubscriptionAsync(request.SubscriptionPublicId, ct);
        var data = new IssueData(request.Label, request.X, request.Y, request.Z, request.IsSpecial, request.Quantity, request.PlannedDate, request.ReceivedDate,
            request.Status, request.Note);
        SerialIssue issue;
        if (request.PublicId is { } id)
        {
            issue = await RequireIssueAsync(id, ct);
            if (issue.SubscriptionId != sub.Id) throw new BusinessRuleException("ISSUE_SUBSCRIPTION_MISMATCH", "Kỳ không thuộc đăng ký này.");
            issue.Apply(data, Today);
        }
        else
        {
            // Nhập tay số X/Y/Z trùng một kỳ đã có (thường là kỳ dự kiến) → cập nhật kỳ đó thay vì nhân đôi.
            var existing = request.X is { } x && !request.IsSpecial
                ? await Db.Set<SerialIssue>().FirstOrDefaultAsync(i => i.SubscriptionId == sub.Id && !i.IsMerged && i.X == x && i.Y == request.Y && i.Z == request.Z, ct)
                : null;
            if (existing is not null)
            {
                issue = existing;
                issue.Apply(data, Today);
            }
            else
            {
                var label = request.Label;
                if (string.IsNullOrWhiteSpace(label) && request.X is { } nx)
                    label = (await PatternOfAsync(sub, ct) ?? throw new BusinessRuleException("LABEL_INVALID", "Nhập nhãn kỳ (đăng ký chưa chọn mẫu đánh số).")).Label(new(nx, request.Y, request.Z));
                issue = SerialIssue.Create(sub.Id, data with { Label = label }, merged: false);
                issue.SetOrder(await NextOrderAsync(sub.Id, ct));
                Db.Set<SerialIssue>().Add(issue);
            }
        }
        await Db.SaveChangesAsync(ct);
        return await GetAsync(issue.PublicId, ct);
    }

    private async Task<int> NextOrderAsync(long subscriptionId, CancellationToken ct) =>
        (await Db.Set<SerialIssue>().Where(i => i.SubscriptionId == subscriptionId).MaxAsync(i => (int?)i.SortOrder, ct) ?? 0) + 1;

    public async Task<IssueDto> ReceiveAsync(IssueReceiveRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issue = await RequireIssueAsync(request.PublicId, ct);
        issue.Receive(request.ReceivedDate ?? Today, request.Quantity);
        await Db.SaveChangesAsync(ct);
        return await GetAsync(issue.PublicId, ct);
    }

    /// <summary>Khiếu nại kỳ chưa nhận. Đăng ký có "số ngày chờ" thì chưa khiếu nại được trước ngày dự kiến + số ngày chờ.</summary>
    public async Task<IssueDto> ClaimAsync(IssueClaimRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issue = await RequireIssueAsync(request.PublicId, ct);
        var grace = await Db.Set<Subscription>().AsNoTracking().Where(s => s.Id == issue.SubscriptionId).Select(s => s.GraceDays).FirstAsync(ct);
        if (issue.Status != IssueStatus.Received && grace > 0 && issue.PlannedDate is { } planned && Today <= planned.AddDays(grace))
            throw new BusinessRuleException("CLAIM_TOO_EARLY", $"Chưa quá hạn chờ ({grace} ngày sau ngày dự kiến {planned:dd/MM/yyyy}), chưa khiếu nại được.");
        issue.Claim(clock.GetUtcNow());
        await Db.SaveChangesAsync(ct);
        return await GetAsync(issue.PublicId, ct);
    }

    /// <summary>
    /// Sinh kỳ dự kiến: bắt đầu từ số kỳ cuối đã sinh (hoặc số bắt đầu của đăng ký), tăng theo mẫu; số đã có kỳ (nhập tay) thì bỏ qua và sinh tiếp.
    /// Ngày dự kiến tính từ mốc (ngày dự kiến lớn nhất đã có → ngày phát hành kỳ đầu → hôm nay) theo tần suất; kỳ đầu của đăng ký chưa có kỳ nào đúng vào ngày phát hành kỳ đầu.
    /// </summary>
    public async Task<IssuePredictResult> PredictAsync(IssuePredictRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var count = Math.Clamp(request.Count, 1, MaxPredict);
        var sub = await RequireSubscriptionAsync(request.SubscriptionPublicId, ct);
        var frequency = sub.FrequencyId is { } fid ? await Db.Set<Frequency>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == fid, ct) : null;
        var pattern = await PatternOfAsync(sub, ct);
        if (frequency is null || pattern is null)
            throw new BusinessRuleException("SERIAL_PATTERN_REQUIRED", "Chọn tần suất và mẫu đánh số cho đăng ký trước khi sinh kỳ dự kiến.");

        var existing = await Db.Set<SerialIssue>().AsNoTracking().Where(i => i.SubscriptionId == sub.Id).ToListAsync(ct);
        var taken = existing.Where(i => !i.IsMerged && i.X is not null).Select(i => (i.X!.Value, i.Y, i.Z)).ToHashSet();
        var lastPlanned = existing.Max(i => i.PlannedDate);
        DateOnly anchor;
        int firstStep;
        if (lastPlanned is { } last)
        {
            anchor = last;
            firstStep = 1;
        }
        else if (sub.FirstIssueDate is { } first)
        {
            anchor = first;
            firstStep = 0;
        }
        else
        {
            anchor = Today;
            firstStep = 1;
        }

        var cursor = sub.LastX is { } lx
            ? pattern.Next(new IssueNumber(lx, sub.LastY, sub.LastZ))
            : pattern.Initial(sub.StartX, sub.StartY, sub.StartZ);
        var order = await NextOrderAsync(sub.Id, ct);
        var created = new List<SerialIssue>();
        var skipped = 0;
        var examined = cursor;
        var guard = count + taken.Count + 1;
        while (created.Count < count && guard-- > 0)
        {
            examined = cursor;
            if (taken.Contains((cursor.X, cursor.Y, cursor.Z)))
            {
                skipped++;
            }
            else
            {
                var issue = SerialIssue.Create(sub.Id, new IssueData(pattern.Label(cursor), cursor.X, cursor.Y, cursor.Z, PlannedDate: frequency.PlannedDate(anchor, firstStep + created.Count)));
                issue.SetOrder(order + created.Count);
                created.Add(issue);
            }
            cursor = pattern.Next(cursor);
        }
        if (created.Count > 0 || skipped > 0) sub.RecordLast(examined);
        Db.Set<SerialIssue>().AddRange(created);
        await Db.SaveChangesAsync(ct);
        var ids = created.Select(i => i.Id).ToList();
        var dtos = await Dtos(Db.Set<SerialIssue>().AsNoTracking().Where(i => ids.Contains(i.Id)), byPlanned: true).ToListAsync(ct);
        return new IssuePredictResult(created.Count, skipped, dtos);
    }

    /// <summary>
    /// Ghép số: gộp ≥ 2 kỳ của một đăng ký thành một kỳ đã nhận (nhãn "3+4", số lượng cộng lại) rồi xoá các kỳ nguồn — một lần lưu nên nguyên tử;
    /// kiểm tra xong mới sửa gì. Kỳ đã vào tập đóng không ghép được.
    /// </summary>
    public async Task<IssueDto> MergeAsync(IssueMergeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ids = request.IssueIds?.Distinct().ToList() ?? [];
        if (ids.Count < 2) throw new BusinessRuleException("MERGE_MIN_TWO", "Chọn ít nhất hai kỳ để ghép số.");
        var sub = await RequireSubscriptionAsync(request.SubscriptionPublicId, ct);
        var sources = await Db.Set<SerialIssue>().Where(i => ids.Contains(i.PublicId)).ToListAsync(ct);
        if (sources.Count != ids.Count) throw new NotFoundException("Kỳ báo", string.Join(",", ids));
        if (sources.Any(i => i.SubscriptionId != sub.Id)) throw new BusinessRuleException("ISSUE_SUBSCRIPTION_MISMATCH", "Các kỳ phải thuộc cùng một đăng ký.");
        var sourceIds = sources.Select(i => i.Id).ToList();
        if (await Db.Set<BindingIssue>().AnyAsync(b => sourceIds.Contains(b.IssueId), ct))
            throw new BusinessRuleException("ISSUE_BOUND", "Có kỳ đã vào tập đóng, không ghép số được.");

        var ordered = sources.OrderBy(i => i.X ?? int.MaxValue).ThenBy(i => i.PlannedDate).ThenBy(i => i.Id).ToList();
        var label = request.Label;
        if (string.IsNullOrWhiteSpace(label))
        {
            var xs = string.Join("+", ordered.Select(i => i.X?.ToString(System.Globalization.CultureInfo.InvariantCulture)).Where(x => x is not null));
            var pattern = await PatternOfAsync(sub, ct);
            label = xs.Length == 0 ? string.Join(" + ", ordered.Select(i => i.Label))
                : (pattern?.LabelTemplate ?? NumberingPattern.DefaultTemplate)
                    .Replace("{X}", xs, StringComparison.Ordinal)
                    .Replace("{Y}", ordered[0].Y?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", StringComparison.Ordinal)
                    .Replace("{Z}", ordered[0].Z?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", StringComparison.Ordinal);
        }
        var note = request.Note ?? "Gộp từ: " + string.Join(", ", ordered.Select(i => i.Label));
        var merged = SerialIssue.Create(sub.Id, new IssueData(label, ordered[0].X, ordered[0].Y, ordered[0].Z, Quantity: Math.Min(1000, ordered.Sum(i => i.Quantity)),
            PlannedDate: ordered.Min(i => i.PlannedDate), ReceivedDate: request.ReceivedDate ?? Today, Status: IssueStatus.Received,
            Note: note.Length <= 500 ? note : note[..500]), merged: true);
        merged.SetOrder(ordered[0].SortOrder);
        Db.Set<SerialIssue>().Add(merged);
        Db.Set<SerialIssue>().RemoveRange(sources); // interceptor chuyển thành xoá mềm
        await Db.SaveChangesAsync(ct);
        return await GetAsync(merged.PublicId, ct);
    }

    public async Task DeleteAsync(Guid publicId, CancellationToken ct)
    {
        var issue = await RequireIssueAsync(publicId, ct);
        if (await Db.Set<BindingIssue>().AnyAsync(b => b.IssueId == issue.Id, ct))
            throw new BusinessRuleException("ISSUE_BOUND", "Kỳ đã vào tập đóng, không xoá được.", 409);
        Db.Set<SerialIssue>().Remove(issue);
        await Db.SaveChangesAsync(ct);
    }

    /// <summary>Thống kê một đăng ký: thiếu = chưa nhận và quá ngày dự kiến; trễ = nhận sau ngày dự kiến.</summary>
    public async Task<IssueStatistics> StatisticsAsync(Guid subscriptionPublicId, CancellationToken ct)
    {
        var sub = await RequireSubscriptionAsync(subscriptionPublicId, ct);
        var today = Today;
        var rows = await Db.Set<SerialIssue>().AsNoTracking().Where(i => i.SubscriptionId == sub.Id)
            .Select(i => new { i.Status, i.PlannedDate, i.ReceivedDate }).ToListAsync(ct);
        return new IssueStatistics(rows.Count, rows.Count(r => r.Status == IssueStatus.Received), rows.Count(r => r.Status == IssueStatus.Claimed),
            rows.Count(r => r.Status == IssueStatus.Planned && r.PlannedDate is { } p && p < today),
            rows.Count(r => r.Status == IssueStatus.Received && r.PlannedDate is { } p && r.ReceivedDate is { } d && d > p),
            rows.Where(r => r.Status == IssueStatus.Received).Max(r => r.ReceivedDate));
    }
}
