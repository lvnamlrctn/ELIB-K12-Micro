using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.BuildingBlocks.Crud;

/// <summary>Lý do thay đổi: nhập trong body (ưu tiên) hoặc header <see cref="CrudDiff.ReasonHeader"/>; 1–500 ký tự.</summary>
public static class ChangeReason
{
    public const int MaxLength = CrudDiff.MaxReasonLength;

    /// <summary>Lý do hợp lệ hoặc null (không nhập).</summary>
    public static string? Optional(string? body, string? header = null)
    {
        var text = string.IsNullOrWhiteSpace(body) ? header : body.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static string Require(string? body, string? header, string what)
    {
        var text = Optional(body, header);
        return text is { Length: > 0 and <= MaxLength }
            ? text
            : throw new BusinessRuleException("REASON_REQUIRED", $"Nhập lý do {what} (tối đa {MaxLength} ký tự).");
    }
}

public static class ApprovalStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    /// <summary>Không áp dụng được nữa (bản ghi đã đổi từ lúc đề nghị) — phải lập yêu cầu mới.</summary>
    public const string Failed = "Failed";
}

/// <summary>
/// Yêu cầu thay đổi chờ duyệt. Chưa áp dụng gì lên dữ liệu thật cho đến khi người có quyền <c>MODULE:approve</c> duyệt;
/// duyệt = chạy lại đúng thao tác từ <see cref="PayloadJson"/> dưới danh nghĩa người duyệt. Mỗi service giữ bảng riêng.
/// </summary>
public sealed class ChangeRequest : TenantEntity
{
    public string Kind { get; set; } = "";
    public Guid TargetPublicId { get; set; }
    public string? TargetLabel { get; set; }
    public string Summary { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string Reason { get; set; } = "";
    public decimal? Amount { get; set; }
    public int? Count { get; set; }

    /// <summary>Phiên bản bản ghi đích lúc đề nghị — lệch lúc duyệt thì <see cref="ApprovalStatus.Failed"/>.</summary>
    public long TargetVersion { get; set; }

    public string Status { get; set; } = ApprovalStatus.Pending;
    public long? RequestedBy { get; set; }
    public string? RequestedByName { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public long? DecidedBy { get; set; }
    public string? DecidedByName { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>Quy tắc duyệt do đơn vị chỉnh (ghi đè mặc định của <see cref="ApprovalKind"/>).</summary>
public sealed class ApprovalRule : TenantEntity
{
    public string Kind { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public decimal? AmountThreshold { get; set; }
    public decimal? PercentThreshold { get; set; }
    public int? CountThreshold { get; set; }
}

/// <summary>
/// Một loại thao tác cần cân nhắc phê duyệt. Ngưỡng: mọi ngưỡng null = LUÔN cần duyệt; có ngưỡng thì cần duyệt khi số tiền hoặc tỉ lệ
/// VƯỢT ngưỡng (&gt;), hoặc số lượng ĐẠT ngưỡng (≥).
/// </summary>
public sealed record ApprovalKind(string Kind, string Module, string EntityType, string EntityName, string Name, string Description,
    bool Enabled = true, decimal? Amount = null, decimal? Percent = null, int? Count = null);

public interface IApprovalKinds
{
    IReadOnlyList<ApprovalKind> All { get; }
}

public sealed class ApprovalKinds(IReadOnlyList<ApprovalKind> all) : IApprovalKinds
{
    public IReadOnlyList<ApprovalKind> All { get; } = all;
}

/// <summary>Áp dụng một yêu cầu đã duyệt (chạy lại thao tác gốc). Ném <see cref="BusinessRuleException"/> nếu không áp dụng được.</summary>
public interface IChangeRequestHandler
{
    bool CanHandle(string kind);

    /// <summary>Chạy dưới danh nghĩa người duyệt; được phép SaveChanges (yêu cầu đã được đánh dấu Approved trong cùng transaction).</summary>
    Task ApplyAsync(ChangeRequest request, CancellationToken cancellationToken);

    /// <summary>Phiên bản hiện tại của bản ghi đích (null = không còn).</summary>
    Task<long?> CurrentVersionAsync(ChangeRequest request, CancellationToken cancellationToken);
}

public sealed class ChangeRequestSearch : CrudSearch
{
    /// <summary>inbox = chờ tôi duyệt (theo quyền); mine = tôi đề nghị.</summary>
    public string Scope { get; set; } = "inbox";
}

public sealed record ChangeRequestDto(
    Guid PublicId, string Kind, string KindName, string Module, Guid TargetPublicId, string? TargetLabel, string Summary, string Reason,
    decimal? Amount, int? Count, string Status, string? RequestedByName, DateTimeOffset RequestedAt, string? DecidedByName,
    DateTimeOffset? DecidedAt, string? DecisionNote, JsonNode? Before, JsonNode? After, bool CanDecide);

public sealed record ApprovalRuleDto(string Kind, string Module, string Name, string Description, bool Enabled, decimal? Amount, decimal? Percent, int? Count,
    bool IsCustom);

public sealed record ApprovalRuleRequest(bool Enabled, decimal? Amount = null, decimal? Percent = null, int? Count = null);

public sealed record ApprovalDecisionRequest(string? Note = null);

/// <summary>Kết quả trả về khi thao tác chưa được áp dụng vì cần duyệt (HTTP 202).</summary>
public sealed record PendingApproval(Guid RequestId, string Kind, string KindName, string Message);

public static class ApprovalModel
{
    /// <summary>Ánh xạ hai bảng phê duyệt vào DbContext của service (<c>change_requests</c>, <c>approval_rules</c>).</summary>
    public static void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<ChangeRequest>(e =>
        {
            e.ToTable("change_requests");
            e.Property(x => x.Kind).HasMaxLength(40);
            e.Property(x => x.TargetLabel).HasMaxLength(250);
            e.Property(x => x.Summary).HasMaxLength(1000);
            e.Property(x => x.Reason).HasMaxLength(CrudDiff.MaxReasonLength);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasMaxLength(20).IsConcurrencyToken(); // hai người duyệt cùng lúc: chỉ một người thắng, người sau nhận 409
            e.Property(x => x.RequestedByName).HasMaxLength(250);
            e.Property(x => x.DecidedByName).HasMaxLength(250);
            e.Property(x => x.DecisionNote).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.Status, x.Kind });
            e.HasIndex(x => new { x.TenantId, x.TargetPublicId });
            e.HasIndex(x => new { x.TenantId, x.RequestedBy });
        });
        modelBuilder.Entity<ApprovalRule>(e =>
        {
            e.ToTable("approval_rules");
            e.Property(x => x.Kind).HasMaxLength(40);
            e.Property(x => x.AmountThreshold).HasPrecision(18, 2);
            e.Property(x => x.PercentThreshold).HasPrecision(9, 2);
            e.HasIndex(x => new { x.TenantId, x.Kind }).IsUnique().HasFilter("is_deleted = false");
        });
    }
}

/// <summary>
/// Phê duyệt theo thẩm quyền: người có <c>MODULE:approve</c> thao tác trực tiếp; người khác — khi thao tác vượt ngưỡng của đơn vị —
/// tạo <see cref="ChangeRequest"/> và chưa gì được áp dụng. Người đề nghị không tự duyệt yêu cầu của mình.
/// </summary>
public sealed class ApprovalService(
    ICrudDbContext db, IApprovalKinds kinds, IActorPermissions permissions, ICurrentActor actor, IServiceProvider services, TimeProvider clock,
    CrudAuditContext? auditContext = null, ICrudAuditSink? auditSink = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ApprovalKind Kind(string kind) => kinds.All.FirstOrDefault(k => k.Kind == kind)
        ?? throw new BusinessRuleException("APPROVAL_KIND_UNKNOWN", $"Loại phê duyệt '{kind}' không tồn tại.", 500);

    public static string ApproveCode(ApprovalKind kind) => kind.Module + ":approve";

    public string? ActorName => auditContext?.Capture().ActorName;

    private async Task<(bool Enabled, decimal? Amount, decimal? Percent, int? Count, bool Custom)> EffectiveAsync(ApprovalKind kind, CancellationToken ct)
    {
        var row = await db.Set<ApprovalRule>().AsNoTracking().FirstOrDefaultAsync(r => r.Kind == kind.Kind, ct);
        return row is null
            ? (kind.Enabled, kind.Amount, kind.Percent, kind.Count, false)
            : (row.Enabled, row.AmountThreshold, row.PercentThreshold, row.CountThreshold, true);
    }

    /// <summary>
    /// Thao tác này có phải chờ duyệt không. false khi người thao tác có quyền duyệt (áp dụng ngay), quy tắc tắt, hoặc chưa vượt ngưỡng.
    /// </summary>
    public async Task<bool> RequiresAsync(string kindCode, decimal? amount, decimal? percent, int? count, CancellationToken ct)
    {
        var kind = Kind(kindCode);
        if (await permissions.HasAsync(ApproveCode(kind), ct)) return false;
        var rule = await EffectiveAsync(kind, ct);
        if (!rule.Enabled) return false;
        if (rule.Amount is null && rule.Percent is null && rule.Count is null) return true;
        return (rule.Amount is { } a && amount > a) || (rule.Percent is { } p && percent > p) || (rule.Count is { } c && count >= c);
    }

    /// <summary>
    /// Lập yêu cầu (lưu ngay) và ghi nhật ký; trả về thông báo cho người thao tác. <paramref name="systemProposal"/>: đề xuất do hệ thống tạo từ một
    /// event — KHÔNG có người đề nghị (người kích hoạt event vẫn duyệt được; chỉ ghi tên vào <paramref name="requestedByName"/>).
    /// </summary>
    public async Task<PendingApproval> SubmitAsync(string kindCode, Guid targetPublicId, string? targetLabel, string summary, object payload,
        JsonObject? before, JsonObject? after, string reason, decimal? amount, int? count, long targetVersion, CancellationToken ct,
        long? requestedBy = null, string? requestedByName = null, bool systemProposal = false)
    {
        var kind = Kind(kindCode);
        var request = new ChangeRequest
        {
            PublicId = Guid.CreateVersion7(), Kind = kindCode, TargetPublicId = targetPublicId, TargetLabel = targetLabel, Summary = Trim(summary, 1000),
            PayloadJson = JsonSerializer.Serialize(payload, Json), BeforeJson = before?.ToJsonString(Json), AfterJson = after?.ToJsonString(Json),
            Reason = reason, Amount = amount, Count = count, TargetVersion = targetVersion, RequestedBy = systemProposal ? null : requestedBy ?? actor.Id, RequestedByName = requestedByName ?? ActorName,
            RequestedAt = clock.GetUtcNow(),
        };
        db.Set<ChangeRequest>().Add(request);
        if (auditSink is not null)
            await auditSink.RecordAsync(new CrudAuditEntry(kind.EntityType, kind.EntityName, targetPublicId, CrudChange.Requested,
                $"{kind.Name}: {summary}", after, reason), ct);
        await db.SaveChangesAsync(ct);
        return new PendingApproval(request.PublicId, kindCode, kind.Name,
            $"Thao tác \"{kind.Name}\" vượt thẩm quyền của bạn — đã gửi yêu cầu chờ người có quyền duyệt xử lý. Chưa có thay đổi nào được áp dụng.");
    }

    public async Task<CrudPage<ChangeRequestDto>> SearchAsync(ChangeRequestSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = db.Set<ChangeRequest>().AsNoTracking();
        var approvable = new List<string>();
        foreach (var kind in kinds.All)
            if (await permissions.HasAsync(ApproveCode(kind), ct)) approvable.Add(kind.Kind);
        if (search.Scope == "mine") query = query.Where(r => r.RequestedBy == actor.Id);
        else query = query.Where(r => approvable.Contains(r.Kind));

        if (search.Status is { } s) query = query.Where(r => r.Status == StatusName(s));
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(r => r.Summary.ToLower().Contains(term) || (r.TargetLabel != null && r.TargetLabel.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        var total = await query.CountAsync(ct);
        var pageSize = Math.Clamp(search.PageSize, 1, 200);
        var index = Math.Max(1, search.PageIndex);
        var rows = await query.OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.Id).Skip((index - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new CrudPage<ChangeRequestDto>([.. rows.Select(r => ToDto(r, approvable))], total, index, pageSize);
    }

    /// <summary>Số yêu cầu đang chờ mà người dùng duyệt được — hiện ở menu.</summary>
    public async Task<int> PendingCountAsync(CancellationToken ct)
    {
        var approvable = new List<string>();
        foreach (var kind in kinds.All)
            if (await permissions.HasAsync(ApproveCode(kind), ct)) approvable.Add(kind.Kind);
        return approvable.Count == 0 ? 0 : await db.Set<ChangeRequest>().CountAsync(r => r.Status == ApprovalStatus.Pending && approvable.Contains(r.Kind), ct);
    }

    /// <summary>Yêu cầu đang chờ của một bản ghi (hiện trên màn chi tiết).</summary>
    public async Task<IReadOnlyList<ChangeRequestDto>> PendingForAsync(Guid targetPublicId, CancellationToken ct)
    {
        var rows = await db.Set<ChangeRequest>().AsNoTracking().Where(r => r.TargetPublicId == targetPublicId && r.Status == ApprovalStatus.Pending)
            .OrderByDescending(r => r.RequestedAt).ToListAsync(ct);
        return [.. rows.Select(r => ToDto(r, []))];
    }

    public async Task<ChangeRequestDto> ApproveAsync(Guid publicId, string? note, CancellationToken ct)
    {
        var request = await PendingAsync(publicId, ct);
        var kind = Kind(request.Kind);
        await RequireDeciderAsync(request, kind, ct);
        var handler = services.GetServices<IChangeRequestHandler>().FirstOrDefault(h => h.CanHandle(request.Kind))
            ?? throw new BusinessRuleException("APPROVAL_HANDLER_MISSING", $"Chưa có bộ xử lý cho '{request.Kind}'.", 500);

        var current = await handler.CurrentVersionAsync(request, ct);
        if (current is null || current != request.TargetVersion)
        {
            Decide(request, ApprovalStatus.Failed, "Bản ghi đã thay đổi từ lúc đề nghị — không áp dụng được, cần lập yêu cầu mới.");
            await db.SaveChangesAsync(ct);
            throw new ConflictException("APPROVAL_STALE", "Bản ghi đã thay đổi từ lúc đề nghị — yêu cầu bị đánh dấu không áp dụng được, cần lập yêu cầu mới.");
        }

        Decide(request, ApprovalStatus.Approved, ChangeReason.Optional(note));
        if (auditSink is not null)
            await auditSink.RecordAsync(new CrudAuditEntry(kind.EntityType, kind.EntityName, request.TargetPublicId, CrudChange.Approved,
                $"Duyệt {kind.Name}: {request.Summary} (đề nghị bởi {request.RequestedByName ?? "?"})", null,
                $"Lý do đề nghị: {request.Reason}" + (string.IsNullOrWhiteSpace(note) ? "" : $"; ghi chú duyệt: {note.Trim()}")), ct);
        await handler.ApplyAsync(request, ct); // SaveChanges của handler lưu luôn trạng thái Approved (cùng transaction với thay đổi)
        await db.SaveChangesAsync(ct);
        return ToDto(request, [request.Kind]);
    }

    public async Task<ChangeRequestDto> RejectAsync(Guid publicId, string? note, CancellationToken ct)
    {
        var request = await PendingAsync(publicId, ct);
        var kind = Kind(request.Kind);
        await RequireDeciderAsync(request, kind, ct);
        var reason = ChangeReason.Require(note, null, "từ chối");
        Decide(request, ApprovalStatus.Rejected, reason);
        if (auditSink is not null)
            await auditSink.RecordAsync(new CrudAuditEntry(kind.EntityType, kind.EntityName, request.TargetPublicId, CrudChange.Rejected,
                $"Từ chối {kind.Name}: {request.Summary} (đề nghị bởi {request.RequestedByName ?? "?"})", null, reason), ct);
        await db.SaveChangesAsync(ct);
        return ToDto(request, [request.Kind]);
    }

    /// <summary>Người đề nghị rút lại yêu cầu của mình khi còn chờ.</summary>
    public async Task<ChangeRequestDto> CancelAsync(Guid publicId, CancellationToken ct)
    {
        var request = await PendingAsync(publicId, ct);
        if (request.RequestedBy != actor.Id)
            throw new BusinessRuleException("APPROVAL_FORBIDDEN", "Chỉ người đề nghị mới rút lại được yêu cầu.", 403);
        Decide(request, ApprovalStatus.Cancelled, null);
        await db.SaveChangesAsync(ct);
        return ToDto(request, []);
    }

    public async Task<IReadOnlyList<ApprovalRuleDto>> RulesAsync(CancellationToken ct)
    {
        var rows = await db.Set<ApprovalRule>().AsNoTracking().ToListAsync(ct);
        return [.. kinds.All.Select(k =>
        {
            var row = rows.FirstOrDefault(r => r.Kind == k.Kind);
            return row is null
                ? new ApprovalRuleDto(k.Kind, k.Module, k.Name, k.Description, k.Enabled, k.Amount, k.Percent, k.Count, false)
                : new ApprovalRuleDto(k.Kind, k.Module, k.Name, k.Description, row.Enabled, row.AmountThreshold, row.PercentThreshold, row.CountThreshold, true);
        })];
    }

    public async Task<ApprovalRuleDto> SaveRuleAsync(string kindCode, ApprovalRuleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kind = Kind(kindCode);
        if (request.Amount is < 0 || request.Percent is < 0 or > 100 || request.Count is < 1)
            throw new BusinessRuleException("APPROVAL_THRESHOLD_INVALID", "Ngưỡng không hợp lệ (số tiền ≥ 0, tỉ lệ 0–100%, số lượng ≥ 1).");
        var row = await db.Set<ApprovalRule>().FirstOrDefaultAsync(r => r.Kind == kindCode, ct);
        var before = row is null
            ? new JsonObject { ["enabled"] = kind.Enabled, ["amount"] = kind.Amount, ["percent"] = kind.Percent, ["count"] = kind.Count }
            : new JsonObject { ["enabled"] = row.Enabled, ["amount"] = row.AmountThreshold, ["percent"] = row.PercentThreshold, ["count"] = row.CountThreshold };
        if (row is null) db.Set<ApprovalRule>().Add(row = new ApprovalRule { PublicId = Guid.CreateVersion7(), Kind = kindCode });
        (row.Enabled, row.AmountThreshold, row.PercentThreshold, row.CountThreshold) = (request.Enabled, request.Amount, request.Percent, request.Count);
        var after = new JsonObject { ["enabled"] = row.Enabled, ["amount"] = row.AmountThreshold, ["percent"] = row.PercentThreshold, ["count"] = row.CountThreshold };
        if (auditSink is not null)
        {
            var diff = new JsonObject();
            foreach (var (key, value) in after)
                if (!JsonNode.DeepEquals(before[key], value)) diff[key] = new JsonArray(before[key]?.DeepClone(), value?.DeepClone());
            await auditSink.RecordAsync(new CrudAuditEntry("ApprovalRule", "Thẩm quyền phê duyệt", row.PublicId == Guid.Empty ? Guid.CreateVersion7() : row.PublicId,
                CrudChange.Updated, kind.Name, diff.Count == 0 ? null : diff, ChangeReason.Optional(null, auditContext?.Capture().Reason)), ct);
        }
        await db.SaveChangesAsync(ct);
        return (await RulesAsync(ct)).First(r => r.Kind == kindCode);
    }

    private async Task<ChangeRequest> PendingAsync(Guid publicId, CancellationToken ct)
    {
        var request = await db.Set<ChangeRequest>().FirstOrDefaultAsync(r => r.PublicId == publicId, ct) ?? throw new NotFoundException("Yêu cầu", publicId);
        return request.Status == ApprovalStatus.Pending
            ? request
            : throw new ConflictException("APPROVAL_NOT_PENDING", "Yêu cầu này đã được xử lý.");
    }

    private async Task RequireDeciderAsync(ChangeRequest request, ApprovalKind kind, CancellationToken ct)
    {
        if (!await permissions.HasAsync(ApproveCode(kind), ct))
            throw new BusinessRuleException("APPROVAL_FORBIDDEN", $"Bạn không có quyền duyệt \"{kind.Name}\" ({ApproveCode(kind)}).", 403);
        if (request.RequestedBy is not null && request.RequestedBy == actor.Id)
            throw new BusinessRuleException("APPROVAL_SELF", "Không tự duyệt yêu cầu do chính mình đề nghị.", 403);
    }

    private void Decide(ChangeRequest request, string status, string? note)
    {
        request.Status = status;
        request.DecidedBy = actor.Id;
        request.DecidedByName = ActorName;
        request.DecidedAt = clock.GetUtcNow();
        request.DecisionNote = note is null ? null : Trim(note, 500);
    }

    private ChangeRequestDto ToDto(ChangeRequest r, IReadOnlyCollection<string> approvable)
    {
        var kind = kinds.All.FirstOrDefault(k => k.Kind == r.Kind);
        return new ChangeRequestDto(r.PublicId, r.Kind, kind?.Name ?? r.Kind, kind?.Module ?? "", r.TargetPublicId, r.TargetLabel, r.Summary, r.Reason, r.Amount,
            r.Count, r.Status, r.RequestedByName, r.RequestedAt, r.DecidedByName, r.DecidedAt, r.DecisionNote, Parse(r.BeforeJson), Parse(r.AfterJson),
            r.Status == ApprovalStatus.Pending && approvable.Contains(r.Kind) && r.RequestedBy != actor.Id);
    }

    private static JsonNode? Parse(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

    private static string StatusName(int status) => status switch
    {
        1 => ApprovalStatus.Pending,
        2 => ApprovalStatus.Approved,
        3 => ApprovalStatus.Rejected,
        4 => ApprovalStatus.Cancelled,
        _ => ApprovalStatus.Failed,
    };

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>Phần trăm giảm trừ — tiện cho nơi gọi.</summary>
    public static decimal Percent(decimal part, decimal total) => total <= 0 ? 0 : decimal.Round(part * 100m / total, 2, MidpointRounding.AwayFromZero);

    public static string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}

public static class ApprovalExtensions
{
    /// <summary>Đăng ký dịch vụ phê duyệt của service cùng danh sách loại thao tác (mặc định của đơn vị chưa chỉnh).</summary>
    public static IServiceCollection AddApprovals(this IServiceCollection services, params ApprovalKind[] kinds)
    {
        services.AddSingleton<IApprovalKinds>(new ApprovalKinds(kinds));
        services.AddScoped<ApprovalService>();
        return services;
    }

    /// <summary>
    /// <c>/approvals</c> (hộp "Chờ phê duyệt": mọi nhân viên gọi được, kết quả lọc theo quyền duyệt) và <c>/approval-rules</c>
    /// (màn "Thẩm quyền phê duyệt", quyền APPROVAL_RULES).
    /// </summary>
    public static IEndpointRouteBuilder MapApprovals(this IEndpointRouteBuilder app)
    {
        var approvals = app.MapGroup("/approvals").WithTags("Approvals").RequireAuthorization(new RequireStaffAttribute());
        approvals.MapPost("/Search", (ChangeRequestSearch search, ApprovalService s, CancellationToken ct) => s.SearchAsync(search, ct));
        approvals.MapGet("/Count", async (ApprovalService s, CancellationToken ct) => new { pending = await s.PendingCountAsync(ct) });
        approvals.MapGet("/ForTarget/{publicId:guid}", (Guid publicId, ApprovalService s, CancellationToken ct) => s.PendingForAsync(publicId, ct));
        approvals.MapPost("/{publicId:guid}/Approve", (Guid publicId, ApprovalDecisionRequest? request, ApprovalService s, CancellationToken ct) => s.ApproveAsync(publicId, request?.Note, ct));
        approvals.MapPost("/{publicId:guid}/Reject", (Guid publicId, ApprovalDecisionRequest? request, ApprovalService s, CancellationToken ct) => s.RejectAsync(publicId, request?.Note, ct));
        approvals.MapPost("/{publicId:guid}/Cancel", (Guid publicId, ApprovalService s, CancellationToken ct) => s.CancelAsync(publicId, ct));

        var rules = app.MapGroup("/approval-rules").WithTags("ApprovalRules");
        rules.MapGet("", (ApprovalService s, CancellationToken ct) => s.RulesAsync(ct)).RequireAuthorization(new PermissionAttribute("APPROVAL_RULES", "view"));
        rules.MapPut("/{kind}", (string kind, ApprovalRuleRequest request, ApprovalService s, CancellationToken ct) => s.SaveRuleAsync(kind, request, ct))
            .RequireAuthorization(new PermissionAttribute("APPROVAL_RULES", "edit"));
        return app;
    }
}
