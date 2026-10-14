using Elib.BuildingBlocks.Domain;
using Elib.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Identity.Application;

/// <summary><see cref="Value"/> null hoặc rỗng = xoá tuỳ chọn (về mặc định của màn).</summary>
public sealed record SetPreferenceRequest(string? Value);

/// <summary>Tuỳ chọn giao diện của chính cán bộ đang đăng nhập (ELIB-LRC: AdminPreference) — không ai đọc/sửa được của người khác.</summary>
public sealed class StaffPreferenceService(IIdentityDb db, TimeProvider clock)
{
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(long? tenantId, long userId, CancellationToken ct) =>
        await db.StaffPreferences.AsNoTracking().Where(p => p.TenantId == tenantId && p.UserId == userId)
            .OrderBy(p => p.Key).ToDictionaryAsync(p => p.Key, p => p.Value, StringComparer.Ordinal, ct);

    public async Task SetAsync(long? tenantId, long userId, string key, string? value, CancellationToken ct)
    {
        if (!StaffPreference.IsValidKey(key))
            throw new BusinessRuleException("PREFERENCE_KEY_INVALID", "Khoá tuỳ chọn chỉ gồm chữ thường, số, dấu chấm, gạch ngang (tối đa 100 ký tự).");
        if (value is { Length: > StaffPreference.MaxValueLength })
            throw new BusinessRuleException("PREFERENCE_TOO_LARGE", $"Giá trị tuỳ chọn tối đa {StaffPreference.MaxValueLength} ký tự.");

        var row = await db.StaffPreferences.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId && p.Key == key, ct);
        if (string.IsNullOrEmpty(value))
        {
            if (row is null) return;
            db.StaffPreferences.Remove(row);
        }
        else if (row is null)
        {
            if (await db.StaffPreferences.CountAsync(p => p.TenantId == tenantId && p.UserId == userId, ct) >= StaffPreference.MaxPerUser)
                throw new BusinessRuleException("PREFERENCE_LIMIT", $"Mỗi tài khoản lưu tối đa {StaffPreference.MaxPerUser} tuỳ chọn.");
            db.StaffPreferences.Add(new StaffPreference { TenantId = tenantId, UserId = userId, Key = key, Value = value, UpdatedAt = clock.GetUtcNow() });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = clock.GetUtcNow();
        }
        await db.SaveChangesAsync(ct);
    }
}
