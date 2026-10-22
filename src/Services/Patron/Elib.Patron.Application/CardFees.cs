using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Payment;
using Elib.Patron.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Patron.Application;

public sealed record CardFeeRequest(string CardNo, decimal Amount, string Reason);

public sealed record CardFeeCancelRequest(string? Reason);

public sealed class CardFeeSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public string? FeeStatus { get; set; }
}

public sealed record CardFeeDto(
    Guid PublicId, string CardNo, string ReaderName, decimal Amount, string Reason, string Status, DateTimeOffset? PaidAt, string? PaidVia, string? CreatedByName,
    DateTimeOffset CreatedAt);

/// <summary>
/// Phí cấp lại thẻ: lập khoản thu (cán bộ nhập số tiền + lý do), thu tiền mặt tại quầy, huỷ, và nhận tiền về qua mã QR của dịch vụ thanh toán.
/// Mọi thay đổi số phải thu phát <see cref="ChargeIssued"/>/<see cref="ChargeSettled"/>/<see cref="ChargeWaived"/> (outbox, cùng giao dịch).
/// </summary>
public sealed class CardFeeService(
    ICrudDbContext db, IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor, TimeProvider clock,
    CrudAuditContext? auditContext = null, ICrudAuditSink? audit = null)
{
    private const string EntityName = "Phí cấp lại thẻ";

    public async Task<CrudPage<CardFeeDto>> SearchAsync(CardFeeSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = db.Set<CardFee>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.CardNo)) query = query.Where(f => f.CardNo == Reader.NormalizeCardNo(search.CardNo));
        if (!string.IsNullOrWhiteSpace(search.FeeStatus)) query = query.Where(f => f.Status == search.FeeStatus);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang SQL
            query = query.Where(f => f.CardNo.StartsWith(term.ToUpper()) || f.ReaderName.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }
        var size = Math.Clamp(search.PageSize, 1, 200);
        var index = Math.Max(1, search.PageIndex);
        var rows = await query.OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id).Skip((index - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<CardFeeDto>([.. rows.Select(ToDto)], await query.CountAsync(ct), index, size);
    }

    public async Task<CardFeeDto> IssueAsync(CardFeeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var card = Reader.NormalizeCardNo(request.CardNo);
        var reader = await db.Set<Reader>().AsNoTracking().FirstOrDefaultAsync(r => r.CardNo == card, ct)
            ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{request.CardNo?.Trim()}'.", 404);
        if (await db.Set<CardFee>().AnyAsync(f => f.ReaderPublicId == reader.PublicId && f.Status == CardFeeStatuses.Open, ct))
            throw new ConflictException("CARD_FEE_EXISTS", $"Bạn đọc {reader.CardNo} đang có một khoản phí cấp lại thẻ chưa thu.");
        var fee = CardFee.Create(reader, request.Amount, request.Reason, auditContext?.Capture().ActorName);
        fee.PublicId = Guid.CreateVersion7();
        db.Set<CardFee>().Add(fee);
        await PublishChargeAsync(fee, ct);
        await AuditAsync(fee, CrudChange.Added, $"Lập phí cấp lại thẻ {Money(fee.Amount)} cho {fee.CardNo} — {fee.ReaderName}", fee.Reason, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(fee);
    }

    /// <summary>Thu tiền mặt tại quầy: đóng khoản thu và báo payment đóng mã QR đang chờ (không thu hai lần).</summary>
    public async Task<CardFeeDto> SettleCashAsync(Guid publicId, CancellationToken ct)
    {
        var fee = await RequireAsync(publicId, ct);
        fee.SettleCash(clock.GetUtcNow());
        await publisher.Publish(new ChargeSettled { TenantId = tenant.RequireTenantId(), Actor = new EventActor(actor.Id, actor.Kind), SourceService = "patron", SourceId = fee.PublicId, Version = fee.Version }, ct);
        await AuditAsync(fee, CrudChange.Updated, $"Thu tiền mặt phí cấp lại thẻ {Money(fee.Amount)} — {fee.CardNo}", null, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(fee);
    }

    /// <summary>Huỷ khoản phí (lập nhầm): lý do bắt buộc, ghi nhật ký.</summary>
    public async Task<CardFeeDto> CancelAsync(Guid publicId, string? reason, CancellationToken ct)
    {
        var why = ChangeReason.Require(reason, auditContext?.Capture().Reason, "huỷ phí cấp lại thẻ");
        var fee = await RequireAsync(publicId, ct);
        fee.Cancel();
        await publisher.Publish(new ChargeWaived { TenantId = tenant.RequireTenantId(), Actor = new EventActor(actor.Id, actor.Kind), SourceService = "patron", SourceId = fee.PublicId, Version = fee.Version }, ct);
        await AuditAsync(fee, CrudChange.Deleted, $"Huỷ phí cấp lại thẻ {Money(fee.Amount)} — {fee.CardNo}", why, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(fee);
    }

    /// <summary>Tiền về qua cổng thanh toán: đóng khoản (idempotent theo mã giao dịch). Đã thu/huỷ trước đó thì ghi nhật ký "cần hoàn tiền".</summary>
    public async Task<bool> ApplyPaymentAsync(PaymentSucceeded e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!string.Equals(e.SourceService, "patron", StringComparison.OrdinalIgnoreCase) || e.Kind != "CARD_REISSUE") return false;
        var fee = await db.Set<CardFee>().FirstOrDefaultAsync(f => f.PublicId == e.SourceId, ct);
        if (fee is null) return false;
        if (fee.PaymentCode == e.TransactionCode) return true;
        if (!fee.IsOpen)
        {
            await AuditAsync(fee, CrudChange.Updated, $"CẦN HOÀN TIỀN: khoản đã {(fee.Status == CardFeeStatuses.Paid ? "thu" : "huỷ")} nhưng tiền {Money(e.Amount)} vẫn về qua {e.Provider} (mã {e.TransactionCode})", null, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }
        fee.SettleByGateway(e.PaidAt, e.TransactionCode);
        await PublishChargeAsync(fee, ct); // Amount 0 + mã giao dịch: payment giữ khoản "đã thu"
        await AuditAsync(fee, CrudChange.Updated, $"Thu {Money(fee.Amount)} qua {e.Provider} (mã {e.TransactionCode}) — phí cấp lại thẻ {fee.CardNo}", null, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private Task PublishChargeAsync(CardFee fee, CancellationToken ct) => publisher.Publish(new ChargeIssued
    {
        TenantId = tenant.RequireTenantId(), Actor = new EventActor(actor.Id, actor.Kind), SourceService = "patron", SourceId = fee.PublicId, Kind = "CARD_REISSUE",
        ReaderPublicId = fee.ReaderPublicId, CardNo = fee.CardNo, Label = "Phí cấp lại thẻ", Amount = fee.IsOpen ? fee.Amount : 0, Version = fee.Version, LastPaymentCode = fee.PaymentCode,
    }, ct);

    private async Task<CardFee> RequireAsync(Guid publicId, CancellationToken ct) =>
        await db.Set<CardFee>().FirstOrDefaultAsync(f => f.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);

    private Task AuditAsync(CardFee fee, CrudChange change, string label, string? reason, CancellationToken ct) => audit is null
        ? Task.CompletedTask
        : audit.RecordAsync(new CrudAuditEntry(nameof(CardFee), EntityName, fee.PublicId, change, label, null, reason), ct);

    private static string Money(decimal amount) => amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    private static CardFeeDto ToDto(CardFee f) =>
        new(f.PublicId, f.CardNo, f.ReaderName, f.Amount, f.Reason, f.Status, f.PaidAt, f.PaidVia, f.CreatedByName, f.CreatedAt);
}
