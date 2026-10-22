using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Payment;
using Elib.Payment.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Payment.Application;

public sealed record CreatePaymentRequest(string SourceService, Guid SourceId, string Provider);

public sealed record ReaderCreatePaymentRequest(Guid ChargePublicId, string Provider);

public sealed record PaymentDto(
    Guid PublicId, string TransactionCode, string Provider, decimal Amount, string Status, DateTimeOffset ExpiresAt, string QrContent, string Label,
    string Kind, DateTimeOffset? PaidAt, bool NeedsReview, string? ReviewNote);

/// <summary><see cref="Providers"/>: cổng đơn vị đang bật — OPAC chỉ hiện nút của các cổng này.</summary>
public sealed record ChargeDto(Guid PublicId, string Kind, string SourceService, Guid SourceId, string Label, decimal Amount, string Status, string CardNo,
    IReadOnlyList<string> Providers);

/// <summary>Tiếp nhận sự kiện khoản thu từ service nguồn. Event cũ (version nhỏ hơn) bị bỏ qua; số tiền đổi hoặc khoản đóng thì mã đang chờ bị huỷ.</summary>
public sealed class ChargeIngest(ICrudDbContext db, TimeProvider clock)
{
    public async Task ApplyAsync(ChargeIssued e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var source = e.SourceService.Trim().ToLowerInvariant();
        var charge = await db.Set<Charge>().FirstOrDefaultAsync(c => c.SourceService == source && c.SourceId == e.SourceId, ct);
        if (charge is null)
        {
            charge = Charge.Create(e.Kind, source, e.SourceId, e.Version, e.ReaderPublicId, e.CardNo, e.Label, e.Amount);
            db.Set<Charge>().Add(charge);
            return;
        }
        var before = (charge.Amount, charge.Status);
        if (!charge.Apply(e.Kind, e.Version, e.ReaderPublicId, e.CardNo, e.Label, e.Amount, e.LastPaymentCode)) return;
        if (before != (charge.Amount, charge.Status)) await CancelPendingAsync(charge, ct);
    }

    public Task ApplyAsync(ChargeWaived e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        return CloseAsync(e.SourceService, e.SourceId, e.Version, (c, now) => c.MarkWaived(now), ct);
    }

    public Task ApplyAsync(ChargeSettled e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        return CloseAsync(e.SourceService, e.SourceId, e.Version, (c, now) => c.MarkSettled(now), ct);
    }

    private async Task CloseAsync(string sourceService, Guid sourceId, long version, Action<Charge, DateTimeOffset> close, CancellationToken ct)
    {
        var source = sourceService.Trim().ToLowerInvariant();
        var charge = await db.Set<Charge>().FirstOrDefaultAsync(c => c.SourceService == source && c.SourceId == sourceId, ct);
        if (charge is null || version < charge.SourceVersion) return;
        close(charge, clock.GetUtcNow());
        await CancelPendingAsync(charge, ct);
    }

    private async Task CancelPendingAsync(Charge charge, CancellationToken ct)
    {
        foreach (var txn in await db.Set<PaymentTransaction>().Where(t => t.ChargeId == charge.Id && t.Status == TransactionStatuses.Pending).ToListAsync(ct))
            txn.Cancel();
    }
}

/// <summary>Sinh mã thanh toán (VietQR / VNPAY) cho một khoản thu và đọc lại trạng thái.</summary>
public sealed class PaymentService(
    ICrudDbContext db, PaymentAccountService accounts, ChargeBackfill backfill, ITenantContext tenant, TimeProvider clock, ICrudAuditSink? audit = null)
{
    /// <summary>Khoản thu theo nguồn (cán bộ chọn phiếu phạt / phiếu sao chụp trên màn của họ).</summary>
    public async Task<PaymentDto> CreateAsync(CreatePaymentRequest request, PaymentContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = request.SourceService.Trim().ToLowerInvariant();
        var charge = await db.Set<Charge>().FirstOrDefaultAsync(c => c.SourceService == source && c.SourceId == request.SourceId, ct);
        if (charge is null && await backfill.RefreshIsolatedAsync(tenant.RequireTenantId(), TimeSpan.FromSeconds(30), ct))
            charge = await db.Set<Charge>().FirstOrDefaultAsync(c => c.SourceService == source && c.SourceId == request.SourceId, ct);
        if (charge is null) throw new BusinessRuleException("CHARGE_NOT_FOUND", "Khoản thu chưa có ở dịch vụ thanh toán (chưa đồng bộ hoặc không còn nợ).", 404);
        return await CreateForAsync(charge, request.Provider, context, ct);
    }

    /// <summary>Bạn đọc tự thanh toán khoản nợ của chính mình — khoản của người khác coi như không tồn tại.</summary>
    public async Task<PaymentDto> CreateForReaderAsync(ReaderCreatePaymentRequest request, Guid readerPublicId, PaymentContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var charge = await db.Set<Charge>().FirstOrDefaultAsync(c => c.PublicId == request.ChargePublicId && c.ReaderPublicId == readerPublicId, ct)
            ?? throw new BusinessRuleException("CHARGE_NOT_FOUND", "Không tìm thấy khoản cần thanh toán.", 404);
        return await CreateForAsync(charge, request.Provider, context, ct);
    }

    private async Task<PaymentDto> CreateForAsync(Charge charge, string providerName, PaymentContext context, CancellationToken ct)
    {
        var provider = PaymentProviders.Normalize(providerName);
        if (!charge.IsOpen || charge.Amount <= 0)
            throw new BusinessRuleException("CHARGE_CLOSED", "Khoản này không còn phải thu (đã thanh toán, đã thu tại quầy hoặc đã miễn).", 409);
        var account = await db.Set<PaymentAccount>().AsNoTracking().FirstOrDefaultAsync(ct);
        if (account is null || !account.Supports(provider))
            throw new BusinessRuleException("PROVIDER_NOT_ENABLED", $"Đơn vị chưa bật cổng thanh toán {provider}.", 409);

        var now = clock.GetUtcNow();
        var pendings = await db.Set<PaymentTransaction>().Where(t => t.ChargeId == charge.Id && t.Status == TransactionStatuses.Pending).ToListAsync(ct);
        // Mã còn hạn cùng cổng, cùng số tiền → dùng lại (bấm hai lần không sinh hai mã). Còn lại huỷ để một khoản chỉ có một mã chờ.
        if (pendings.FirstOrDefault(t => t.Provider == provider && t.Amount == charge.Amount && t.ExpiresAt > now) is { } reuse) return ToDto(reuse, charge);
        foreach (var old in pendings) old.Cancel();

        var code = PaymentTransaction.NewCode(now);
        var expires = now.AddMinutes(account.ExpireMinutes);
        var qr = provider == PaymentProviders.VietQr
            ? VietQrPayload.Build(account.BankBin!, account.AccountNo!, charge.Amount, code, account.AccountName)
            : VnPayUrl.Build(account.VnPayPaymentUrl, account.VnPayTmnCode!,
                accounts.Reveal(account.ProtectedVnPayHashSecret) ?? throw new BusinessRuleException("VNPAY_SECRET_MISSING", "Không đọc được HashSecret VNPAY — nhập lại trong cấu hình tài khoản nhận tiền.", 409),
                code, charge.Amount, $"{charge.Label} {charge.CardNo}", context.ReturnUrl, context.IpAddress, now, expires);
        var txn = PaymentTransaction.Create(charge, provider, qr, code, now, account.ExpireMinutes);
        db.Set<PaymentTransaction>().Add(txn);
        if (audit is not null)
            await audit.RecordAsync(new CrudAuditEntry(nameof(PaymentTransaction), "Giao dịch thanh toán", Guid.CreateVersion7(), CrudChange.Added,
                $"Tạo mã {provider} {code} — {charge.Label} ({PaymentAccount.Money(charge.Amount)})"), ct);
        await db.SaveChangesAsync(ct);
        return ToDto(txn, charge);
    }

    /// <summary>Trạng thái một giao dịch. <paramref name="readerPublicId"/> khác null: chỉ giao dịch của chính bạn đọc đó. Hết hạn thì đóng luôn.</summary>
    public async Task<PaymentDto> GetAsync(Guid publicId, Guid? readerPublicId, CancellationToken ct)
    {
        var row = await (from t in db.Set<PaymentTransaction>()
                         join c in db.Set<Charge>() on t.ChargeId equals c.Id
                         where t.PublicId == publicId && (readerPublicId == null || c.ReaderPublicId == readerPublicId)
                         select new { t, c }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Giao dịch", publicId);
        if (row.t.Expire(clock.GetUtcNow())) await db.SaveChangesAsync(ct);
        return ToDto(row.t, row.c);
    }

    /// <summary>Cổng đơn vị đang bật — màn của cán bộ chỉ hiện nút của các cổng này (không lộ cấu hình/bí mật).</summary>
    public async Task<IReadOnlyList<string>> EnabledProvidersAsync(CancellationToken ct)
    {
        var account = await db.Set<PaymentAccount>().AsNoTracking().FirstOrDefaultAsync(ct);
        return [.. PaymentProviders.All.Where(p => account?.Supports(p) == true)];
    }

    /// <summary>Các khoản còn nợ của bạn đọc + cổng đang bật (tab "Phí / Nợ của tôi").</summary>
    public async Task<IReadOnlyList<ChargeDto>> MyChargesAsync(Guid readerPublicId, CancellationToken ct)
    {
        if (!await backfill.IsSyncedAsync(ct)) await backfill.RefreshIsolatedAsync(tenant.RequireTenantId(), TimeSpan.FromSeconds(30), ct); // đơn vị mới mua: dựng nợ có sẵn
        var account = await db.Set<PaymentAccount>().AsNoTracking().FirstOrDefaultAsync(ct);
        var providers = PaymentProviders.All.Where(p => account?.Supports(p) == true).ToList();
        var charges = await db.Set<Charge>().AsNoTracking().Where(c => c.ReaderPublicId == readerPublicId && c.Status == ChargeStatuses.Open && c.Amount > 0)
            .OrderBy(c => c.CreatedAt).ToListAsync(ct);
        return [.. charges.Select(c => new ChargeDto(c.PublicId, c.Kind, c.SourceService, c.SourceId, c.Label, c.Amount, c.Status, c.CardNo, providers))];
    }

    private static PaymentDto ToDto(PaymentTransaction t, Charge c) =>
        new(t.PublicId, t.TransactionCode, t.Provider, t.Amount, t.Status, t.ExpiresAt, t.QrContent, c.Label, c.Kind, t.PaidAt, t.NeedsReview, t.ReviewNote);
}

/// <summary>Thông tin lời gọi cần cho URL VNPAY (địa chỉ IP người thanh toán, địa chỉ quay lại sau khi trả tiền).</summary>
public sealed record PaymentContext(string IpAddress, string ReturnUrl);
