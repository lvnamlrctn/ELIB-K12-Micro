using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Serials.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Elib.Serials.Application;

/// <summary>
/// Bộ lọc báo cáo báo/tạp chí (monolith: SerialReportService). <see cref="SubscriptionPublicId"/> hoặc <see cref="Keyword"/> (nhan đề/ISSN) chọn đăng ký;
/// khoảng ngày theo ngày nhận (báo cáo nhận) hoặc ngày dự kiến (báo cáo thiếu/khiếu nại).
/// </summary>
public sealed record SerialReportRequest(Guid? SubscriptionPublicId = null, string? Keyword = null, DateOnly? From = null, DateOnly? To = null, int PageIndex = 1,
    int PageSize = 50);

public sealed record ReceivedSummaryRow(Guid SubscriptionPublicId, string Title, string? Issn, int IssueCount, int Quantity, DateOnly? LastReceivedDate);

public sealed record ReceivedDetailRow(
    Guid IssuePublicId, Guid SubscriptionPublicId, string Title, string? Issn, string Label, DateOnly? PlannedDate, DateOnly? ReceivedDate, int Quantity, bool Late);

public sealed record MissingClaimRow(
    Guid IssuePublicId, Guid SubscriptionPublicId, string Title, string? Issn, string Label, DateOnly? PlannedDate, int Status, string StatusName, int ClaimCount,
    DateOnly? ClaimDate);

/// <summary>File Excel của các báo cáo báo/tạp chí — tên thư viện lấy từ bản sao đơn vị trong DB của serials.</summary>
public sealed class SerialsExcel(ICrudDbContext db, ITenantContext tenant, TimeProvider clock, IHttpContextAccessor http) : ReportExcel(clock, http)
{
    public override async Task<string?> LibraryNameAsync(CancellationToken ct)
    {
        var id = tenant.RequireTenantId();
        return await db.Set<TenantReplicaRecord>().AsNoTracking().Where(t => t.TenantId == id).Select(t => t.Name).FirstOrDefaultAsync(ct);
    }
}

/// <summary>Ba báo cáo của LRC: tổng hợp nhận, chi tiết nhận, thiếu/khiếu nại — mỗi báo cáo có bản xuất Excel (quyền SERIAL_REPORT).</summary>
public sealed class SerialReports(ICrudDbContext db, SerialsExcel excel, TimeProvider clock)
{
    public const int MaxPageSize = 500;
    private const int RowCap = ReportExcel.MaxRows;

    private DateOnly Today => VietnamDate.Of(clock.GetUtcNow());

    // Khởi tạo bằng thuộc tính (không dùng constructor) để EF còn dịch được các Where/OrderBy nối tiếp.
    private sealed class Row
    {
        public Guid IssuePublicId { get; init; }
        public Guid SubscriptionPublicId { get; init; }
        public string Title { get; init; } = "";
        public string? Issn { get; init; }
        public string Label { get; init; } = "";
        public int Status { get; init; }
        public int Quantity { get; init; }
        public DateOnly? PlannedDate { get; init; }
        public DateOnly? ReceivedDate { get; init; }
        public int ClaimCount { get; init; }
        public DateTimeOffset? ClaimDate { get; init; }
    }

    private IQueryable<Row> Rows(SerialReportRequest r) =>
        from i in db.Set<SerialIssue>().AsNoTracking()
        join s in db.Set<Subscription>().AsNoTracking() on i.SubscriptionId equals s.Id
        where (r.SubscriptionPublicId == null || s.PublicId == r.SubscriptionPublicId)
        select new Row
        {
            IssuePublicId = i.PublicId, SubscriptionPublicId = s.PublicId, Title = s.Title, Issn = s.Issn, Label = i.Label, Status = i.Status, Quantity = i.Quantity,
            PlannedDate = i.PlannedDate, ReceivedDate = i.ReceivedDate, ClaimCount = i.ClaimCount, ClaimDate = i.ClaimDate,
        };

    private IQueryable<Row> Filtered(SerialReportRequest r)
    {
        ReportExcel.EnsureRange(r.From, r.To);
        var rows = Rows(r);
        if (r.Keyword?.Trim().ToLowerInvariant() is { Length: > 0 } term)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            rows = rows.Where(x => x.Title.ToLower().Contains(term) || (x.Issn != null && x.Issn.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return rows;
    }

    private IQueryable<Row> Received(SerialReportRequest r)
    {
        var rows = Filtered(r).Where(x => x.Status == IssueStatus.Received);
        if (r.From is { } from) rows = rows.Where(x => x.ReceivedDate >= from);
        if (r.To is { } to) rows = rows.Where(x => x.ReceivedDate <= to);
        return rows;
    }

    private IQueryable<Row> Missing(SerialReportRequest r)
    {
        var today = Today;
        var rows = Filtered(r).Where(x => x.Status != IssueStatus.Received
            && (x.Status == IssueStatus.Claimed || x.Status == IssueStatus.Missing || x.ClaimCount > 0 || (x.PlannedDate != null && x.PlannedDate < today)));
        if (r.From is { } from) rows = rows.Where(x => x.PlannedDate >= from);
        if (r.To is { } to) rows = rows.Where(x => x.PlannedDate <= to);
        return rows;
    }

    private static (int Page, int Size) Paging(SerialReportRequest r) => (Math.Max(1, r.PageIndex), Math.Clamp(r.PageSize, 1, MaxPageSize));

    // ── Tổng hợp nhận ────────────────────────────────────────────────────────

    private async Task<List<ReceivedSummaryRow>> SummaryAsync(SerialReportRequest r, CancellationToken ct)
    {
        var rows = await Received(r).Take(RowCap).ToListAsync(ct);
        return [.. rows.GroupBy(x => x.SubscriptionPublicId).Select(g => new ReceivedSummaryRow(g.Key, g.First().Title, g.First().Issn, g.Count(), g.Sum(x => x.Quantity),
            g.Max(x => x.ReceivedDate))).OrderBy(x => x.Title, StringComparer.CurrentCulture)];
    }

    public async Task<CrudPage<ReceivedSummaryRow>> ReceivedSummaryAsync(SerialReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var all = await SummaryAsync(request, ct);
        var (page, size) = Paging(request);
        return new CrudPage<ReceivedSummaryRow>([.. all.Skip((page - 1) * size).Take(size)], all.Count, page, size);
    }

    public async Task<ExcelFile> ReceivedSummaryExportAsync(SerialReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var all = await SummaryAsync(request, ct);
        return await excel.BuildAsync("tong-hop-nhan-bao", ct, new ExcelSheet("Tổng hợp nhận", "TỔNG HỢP NHẬN BÁO, TẠP CHÍ", ReportExcel.Period(request.From, request.To),
            ["Nhan đề", "ISSN", "Số kỳ", "Số bản", "Ngày nhận cuối"],
            all.Select(x => (IReadOnlyList<object?>)[x.Title, x.Issn, x.IssueCount, x.Quantity, x.LastReceivedDate])));
    }

    // ── Chi tiết nhận ────────────────────────────────────────────────────────

    private static IQueryable<Row> ByReceived(IQueryable<Row> q) => q.OrderBy(x => x.ReceivedDate).ThenBy(x => x.Title).ThenBy(x => x.PlannedDate);

    private static ReceivedDetailRow ToDetail(Row x) => new(x.IssuePublicId, x.SubscriptionPublicId, x.Title, x.Issn, x.Label, x.PlannedDate, x.ReceivedDate, x.Quantity,
        x.PlannedDate is { } p && x.ReceivedDate is { } d && d > p);

    public async Task<CrudPage<ReceivedDetailRow>> ReceivedDetailAsync(SerialReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var q = Received(request);
        var (page, size) = Paging(request);
        var rows = await ByReceived(q).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<ReceivedDetailRow>([.. rows.Select(ToDetail)], await q.CountAsync(ct), page, size);
    }

    public async Task<ExcelFile> ReceivedDetailExportAsync(SerialReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = (await ByReceived(Received(request)).Take(RowCap).ToListAsync(ct)).Select(ToDetail);
        return await excel.BuildAsync("chi-tiet-nhan-bao", ct, new ExcelSheet("Chi tiết nhận", "CHI TIẾT NHẬN BÁO, TẠP CHÍ", ReportExcel.Period(request.From, request.To),
            ["Nhan đề", "ISSN", "Kỳ", "Ngày dự kiến", "Ngày nhận", "Số bản", "Nhận trễ"],
            rows.Select(x => (IReadOnlyList<object?>)[x.Title, x.Issn, x.Label, x.PlannedDate, x.ReceivedDate, x.Quantity, x.Late ? "Có" : ""])));
    }

    // ── Thiếu / khiếu nại ────────────────────────────────────────────────────

    private static IQueryable<Row> ByPlanned(IQueryable<Row> q) => q.OrderBy(x => x.PlannedDate).ThenBy(x => x.Title);

    private static string StatusName(int status) => status switch
    {
        IssueStatus.Claimed => "Đã khiếu nại",
        IssueStatus.Missing => "Thiếu",
        _ => "Quá ngày dự kiến",
    };

    private static MissingClaimRow ToMissing(Row x) => new(x.IssuePublicId, x.SubscriptionPublicId, x.Title, x.Issn, x.Label, x.PlannedDate, x.Status, StatusName(x.Status),
        x.ClaimCount, x.ClaimDate is { } c ? VietnamDate.Of(c) : null);

    public async Task<CrudPage<MissingClaimRow>> MissingClaimAsync(SerialReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var q = Missing(request);
        var (page, size) = Paging(request);
        var rows = await ByPlanned(q).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<MissingClaimRow>([.. rows.Select(ToMissing)], await q.CountAsync(ct), page, size);
    }

    public async Task<ExcelFile> MissingClaimExportAsync(SerialReportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = (await ByPlanned(Missing(request)).Take(RowCap).ToListAsync(ct)).Select(ToMissing);
        return await excel.BuildAsync("thieu-khieu-nai-bao", ct, new ExcelSheet("Thiếu - khiếu nại", "DANH SÁCH KỲ THIẾU / KHIẾU NẠI", ReportExcel.Period(request.From, request.To),
            ["Nhan đề", "ISSN", "Kỳ", "Ngày dự kiến", "Tình trạng", "Số lần khiếu nại", "Ngày khiếu nại"],
            rows.Select(x => (IReadOnlyList<object?>)[x.Title, x.Issn, x.Label, x.PlannedDate, x.StatusName, x.ClaimCount, x.ClaimDate])));
    }
}

/// <summary>Bảng điều khiển báo/tạp chí của đơn vị (GET /api/dashboard).</summary>
public sealed record SerialsDashboard(int Subscriptions, int Approved, int Issues, int Received, int Claimed, int Missing, int Bindings);

public sealed class SerialsDashboardService(ICrudDbContext db, TimeProvider clock)
{
    public async Task<SerialsDashboard> BuildAsync(CancellationToken ct)
    {
        var today = VietnamDate.Of(clock.GetUtcNow());
        var subs = await db.Set<Subscription>().AsNoTracking().GroupBy(_ => 1).Select(g => new { Total = g.Count(), Approved = g.Count(s => s.Approved) }).FirstOrDefaultAsync(ct);
        var issues = await db.Set<SerialIssue>().AsNoTracking().Select(i => new { i.Status, i.PlannedDate }).ToListAsync(ct);
        return new SerialsDashboard(subs?.Total ?? 0, subs?.Approved ?? 0, issues.Count, issues.Count(i => i.Status == IssueStatus.Received),
            issues.Count(i => i.Status == IssueStatus.Claimed), issues.Count(i => i.Status == IssueStatus.Planned && i.PlannedDate is { } p && p < today),
            await db.Set<Binding>().CountAsync(ct));
    }
}
