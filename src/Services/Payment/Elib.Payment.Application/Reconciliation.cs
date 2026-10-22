using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Payment.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Payment.Application;

/// <summary>Tìm giao dịch để đối soát. Keyword: mã giao dịch, số thẻ hoặc nhãn khoản thu. From/To theo ngày tạo (giờ UTC của ngày Việt Nam).</summary>
public sealed class PaymentSearch : CrudSearch
{
    public string? TransactionStatus { get; set; }
    public string? Provider { get; set; }
    public bool? NeedsReview { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record PaymentRow(
    Guid PublicId, string TransactionCode, string Provider, decimal Amount, string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
    DateTimeOffset? PaidAt, string? ProviderRef, bool NeedsReview, string? ReviewNote, string Label, string Kind, string CardNo, string ChargeStatus);

/// <summary>Tổng tiền theo trạng thái trên toàn bộ kết quả lọc (không chỉ trang đang xem).</summary>
public sealed record PaymentTotals(decimal Paid, decimal Pending, decimal NeedsReview, int NeedsReviewCount);

public sealed record PaymentSearchResult(CrudPage<PaymentRow> Page, PaymentTotals Totals);

public sealed record ResolveReviewRequest(string Note);

public sealed class WebhookLogSearch : CrudSearch
{
    public string? Provider { get; set; }
}

public sealed record WebhookLogRow(long Id, string Provider, string? TransactionCode, string Result, string Payload, string? RemoteIp, DateTimeOffset ReceivedAt);

/// <summary>Đối soát: danh sách giao dịch, các khoản cần xử lý tay (tiền về muộn, sai số tiền, khoản đã đóng), nhật ký webhook.</summary>
public sealed class PaymentReconciliation(ICrudDbContext db, ICrudAuditSink? audit = null)
{
    public async Task<PaymentSearchResult> SearchAsync(PaymentSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = from t in db.Set<PaymentTransaction>().AsNoTracking()
                    join c in db.Set<Charge>().AsNoTracking() on t.ChargeId equals c.Id
                    select new { t, c };
        if (!string.IsNullOrWhiteSpace(search.TransactionStatus)) query = query.Where(x => x.t.Status == search.TransactionStatus);
        if (!string.IsNullOrWhiteSpace(search.Provider)) query = query.Where(x => x.t.Provider == search.Provider);
        if (search.NeedsReview is { } review) query = query.Where(x => x.t.NeedsReview == review);
        if (search.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
            query = query.Where(x => x.t.CreatedAt >= start);
        }
        if (search.To is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
            query = query.Where(x => x.t.CreatedAt < end);
        }
        if (search.Term is { } term)
        {
            var upper = term.ToUpperInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower()/ToUpper() sang SQL
            query = query.Where(x => x.t.TransactionCode.StartsWith(upper) || x.c.CardNo.StartsWith(upper) || x.c.Label.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var amounts = await query.Select(x => new { x.t.Status, x.t.Amount, x.t.PaidAmount, x.t.NeedsReview }).ToListAsync(ct); // cộng ngoài SQL: SQLite (test) không SUM decimal
        var totals = new PaymentTotals(
            amounts.Where(a => a.Status is TransactionStatuses.Paid or TransactionStatuses.PaidLate).Sum(a => a.PaidAmount ?? a.Amount),
            amounts.Where(a => a.Status == TransactionStatuses.Pending).Sum(a => a.Amount),
            amounts.Where(a => a.NeedsReview).Sum(a => a.PaidAmount ?? a.Amount),
            amounts.Count(a => a.NeedsReview));

        var size = Math.Clamp(search.PageSize, 1, 200);
        var index = Math.Max(1, search.PageIndex);
        var total = amounts.Count;
        var rows = await query.OrderByDescending(x => x.t.CreatedAt).ThenByDescending(x => x.t.Id).Skip((index - 1) * size).Take(size)
            .Select(x => new PaymentRow(x.t.PublicId, x.t.TransactionCode, x.t.Provider, x.t.Amount, x.t.Status, x.t.CreatedAt, x.t.ExpiresAt, x.t.PaidAt,
                x.t.ProviderRef, x.t.NeedsReview, x.t.ReviewNote, x.c.Label, x.c.Kind, x.c.CardNo, x.c.Status)).ToListAsync(ct);
        return new PaymentSearchResult(new CrudPage<PaymentRow>(rows, total, index, size), totals);
    }

    /// <summary>Cán bộ xác nhận đã xử lý tay (hoàn tiền, đối chiếu sao kê…) — ghi chú bắt buộc, lưu vào nhật ký.</summary>
    public async Task ResolveReviewAsync(Guid publicId, ResolveReviewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var note = ChangeReason.Require(request.Note, null, "xử lý đối soát");
        var txn = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.PublicId == publicId, ct) ?? throw new NotFoundException("Giao dịch", publicId);
        if (!txn.NeedsReview) throw new ConflictException("NOT_FLAGGED", "Giao dịch này không có yêu cầu đối soát.");
        var was = txn.ReviewNote;
        txn.ResolveReview(note);
        if (audit is not null)
            await audit.RecordAsync(new CrudAuditEntry(nameof(PaymentTransaction), "Giao dịch thanh toán", txn.PublicId, CrudChange.Updated,
                $"Đã xử lý đối soát giao dịch {txn.TransactionCode}", new System.Text.Json.Nodes.JsonObject { ["needsReview"] = new System.Text.Json.Nodes.JsonArray(true, false) },
                $"{note} (đối soát: {was})"), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CrudPage<WebhookLogRow>> LogsAsync(WebhookLogSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = db.Set<WebhookLog>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.Provider)) query = query.Where(l => l.Provider == search.Provider);
        if (search.Keyword?.Trim().ToUpperInvariant() is { Length: > 0 } code) query = query.Where(l => l.TransactionCode == code);
        var size = Math.Clamp(search.PageSize, 1, 200);
        var index = Math.Max(1, search.PageIndex);
        var rows = await query.OrderByDescending(l => l.ReceivedAt).ThenByDescending(l => l.Id).Skip((index - 1) * size).Take(size)
            .Select(l => new WebhookLogRow(l.Id, l.Provider, l.TransactionCode, l.Result, l.Payload, l.RemoteIp, l.ReceivedAt)).ToListAsync(ct);
        return new CrudPage<WebhookLogRow>(rows, await query.CountAsync(ct), index, size);
    }
}
