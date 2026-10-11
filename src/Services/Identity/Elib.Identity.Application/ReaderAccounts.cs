using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Patron;
using Elib.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Identity.Application;

/// <summary>Trạng thái bạn đọc lấy thẳng từ patron (/internal, service token) — khi identity chưa nhận được event của bạn đọc đó.</summary>
public interface IReaderSource
{
    Task<ReaderChanged?> ReaderAsync(long tenantId, Guid readerPublicId, CancellationToken ct);
}

public sealed record ReaderAccountStatusRequest(IReadOnlyList<Guid> ReaderIds);

/// <summary>Tình trạng đăng nhập OPAC — không bao giờ trả mật khẩu hay chuỗi băm.</summary>
public sealed record ReaderAccountDto(
    Guid ReaderPublicId, bool HasPassword, bool CardNoPassword, bool MustChangePassword, bool LockedOut, DateTimeOffset? LastLoginAt);

/// <summary><see cref="UseCardNo"/> = mật khẩu mặc định là số thẻ; ngược lại đặt <see cref="Password"/>.</summary>
public sealed record SetReaderPasswordRequest(string? Password, bool UseCardNo);

public sealed record BulkReaderPasswordRequest(IReadOnlyList<Guid> ReaderIds, string? Password, bool UseCardNo);

/// <summary><see cref="NotFound"/>: bạn đọc không còn ở patron (đã xoá) — bỏ qua.</summary>
public sealed record BulkReaderPasswordResult(int UpdatedCount, IReadOnlyList<Guid> NotFound);

/// <summary>
/// Mật khẩu OPAC của bạn đọc (monolith: Reader/ResetPassword, Reader/BulkResetPassword, BulkUpdate "changePassword"). Cán bộ có quyền
/// READERS:edit đặt mật khẩu hoặc dùng số thẻ làm mật khẩu mặc định; bạn đọc luôn phải đổi ở lần đăng nhập đầu.
/// </summary>
public sealed class ReaderAccountService(IIdentityDb db, ITenantContext tenant, IPasswordHasher hasher, IReaderSource readers, TimeProvider clock,
    IdentityAudit audit)
{
    public const int MaxBulk = 2000;

    /// <summary>Upsert hồ sơ theo event của patron. Chưa lưu — nơi gọi SaveChanges.</summary>
    public async Task<ReaderAccount> ApplyAsync(ReaderChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var account = await db.ReaderAccounts.FirstOrDefaultAsync(x => x.ReaderPublicId == e.ReaderPublicId, ct);
        if (account is null) db.ReaderAccounts.Add(account = new ReaderAccount { ReaderPublicId = e.ReaderPublicId });
        account.ApplyProfile(e.Version, e.CardNo, e.FullName, e.Email, e.Status, e.ExpireDate, e.Deleted);
        return account;
    }

    public async Task<IReadOnlyList<ReaderAccountDto>> StatusAsync(ReaderAccountStatusRequest request, CancellationToken ct)
    {
        tenant.RequireTenantId();
        var ids = Distinct(request.ReaderIds);
        var accounts = await db.ReaderAccounts.AsNoTracking().Where(x => ids.Contains(x.ReaderPublicId) && !x.Deleted).ToDictionaryAsync(x => x.ReaderPublicId, ct);
        var now = clock.GetUtcNow();
        return ids.Select(id => accounts.TryGetValue(id, out var a)
            ? ToDto(a, now)
            : new ReaderAccountDto(id, false, false, false, false, null)).ToList();
    }

    public async Task<ReaderAccountDto> SetPasswordAsync(Guid readerPublicId, SetReaderPasswordRequest request, CancellationToken ct)
    {
        var hash = HashFor(request.Password, request.UseCardNo);
        var account = await LoadAsync(readerPublicId, ct)
            ?? throw new NotFoundException("bạn đọc", readerPublicId);
        Apply(account, hash);
        await audit.TenantAsync(tenant.RequireTenantId(), "READER_PASSWORD", "Reader", readerPublicId.ToString(),
            $"Đặt mật khẩu OPAC cho bạn đọc {account.CardNo} — {account.FullName}" + (hash is null ? " (mật khẩu = số thẻ)" : ""), ct);
        await db.SaveChangesAsync(ct);
        return ToDto(account, clock.GetUtcNow());
    }

    public async Task<BulkReaderPasswordResult> BulkSetPasswordAsync(BulkReaderPasswordRequest request, CancellationToken ct)
    {
        var ids = Distinct(request.ReaderIds);
        if (ids.Count == 0) throw new BusinessRuleException("READER_IDS_REQUIRED", "Chưa chọn bạn đọc nào.");
        if (ids.Count > MaxBulk) throw new BusinessRuleException("READER_IDS_TOO_MANY", $"Mỗi lần đặt tối đa {MaxBulk} bạn đọc.");
        // Một chuỗi băm cho cả lượt (cùng mật khẩu): BCrypt cost 12 cho từng người thì vài trăm bạn đọc mất cả phút.
        var hash = HashFor(request.Password, request.UseCardNo);

        var notFound = new List<Guid>();
        var updated = 0;
        foreach (var id in ids)
        {
            if (await LoadAsync(id, ct) is not { } account)
            {
                notFound.Add(id);
                continue;
            }
            Apply(account, hash);
            updated++;
        }
        if (updated > 0)
            await audit.TenantAsync(tenant.RequireTenantId(), "READER_PASSWORD_BULK", "Reader", null,
                $"Đặt mật khẩu OPAC cho {updated} bạn đọc" + (hash is null ? " (mật khẩu = số thẻ)" : " (mật khẩu chung)"), ct);
        await db.SaveChangesAsync(ct);
        return new BulkReaderPasswordResult(updated, notFound);
    }

    /// <summary>Mở khoá tạm do đăng nhập sai nhiều lần.</summary>
    public async Task<ReaderAccountDto> UnlockAsync(Guid readerPublicId, CancellationToken ct)
    {
        var account = await LoadAsync(readerPublicId, ct) ?? throw new NotFoundException("bạn đọc", readerPublicId);
        account.Credentials.Unlock();
        await audit.TenantAsync(tenant.RequireTenantId(), "READER_ACCOUNT_UNLOCK", "Reader", readerPublicId.ToString(),
            $"Mở khoá đăng nhập OPAC của bạn đọc {account.CardNo} — {account.FullName}", ct);
        await db.SaveChangesAsync(ct);
        return ToDto(account, clock.GetUtcNow());
    }

    private string? HashFor(string? password, bool useCardNo)
    {
        if (useCardNo) return null;
        ReaderAccount.EnsurePasswordRules(password);
        return hasher.Hash(password!);
    }

    private static void Apply(ReaderAccount account, string? hash)
    {
        if (hash is null) account.UseCardNoPassword();
        else account.SetPassword(hash);
    }

    /// <summary>
    /// Tài khoản của bạn đọc; identity chưa có (bạn đọc vừa tạo, hoặc tạo trước khi identity nhận event bạn đọc) thì hỏi patron rồi lưu.
    /// Bạn đọc đã xoá/không có → null.
    /// </summary>
    private async Task<ReaderAccount?> LoadAsync(Guid readerPublicId, CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var account = await db.ReaderAccounts.FirstOrDefaultAsync(x => x.ReaderPublicId == readerPublicId, ct);
        if (account is null && await readers.ReaderAsync(tenantId, readerPublicId, ct) is { } state)
        {
            account = await ApplyAsync(state, ct);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Consumer vừa ghi cùng bạn đọc (event tới đúng lúc) — dùng bản của consumer.
                db.ReaderAccounts.Entry(account).State = EntityState.Detached;
                account = await db.ReaderAccounts.FirstOrDefaultAsync(x => x.ReaderPublicId == readerPublicId, ct);
            }
        }
        return account is { Deleted: false } ? account : null;
    }

    private static List<Guid> Distinct(IReadOnlyList<Guid>? ids) => (ids ?? []).Where(id => id != Guid.Empty).Distinct().ToList();

    private static ReaderAccountDto ToDto(ReaderAccount a, DateTimeOffset now) => new(
        a.ReaderPublicId, a.HasPassword, a.CardNoPassword, a.Credentials.MustChangePassword, a.Credentials.IsLockedOut(now), a.Credentials.LastLoginAt);
}
