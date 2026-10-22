using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Payment;
using Elib.Contracts.Events.Platform;
using Elib.Payment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Payment.Application;

/// <summary>Nguồn khoản thu đang nợ có sẵn từ trước khi đơn vị dùng thanh toán (circulation: phiếu phạt còn nợ). Infrastructure gọi /internal bằng service token.</summary>
public interface IChargeSources
{
    string[] Services { get; }

    Task<StatePage<ChargeIssued>> OpenChargesAsync(string service, long tenantId, long after, CancellationToken cancellationToken);
}

/// <summary>
/// Dựng khoản thu từ dữ liệu SẴN CÓ của service nguồn: phiếu phạt lập trước khi đơn vị mua phân hệ thanh toán (hoặc trước khi có service này) chưa từng phát
/// <c>ChargeIssued</c>, nên nếu không dựng lại thì bạn đọc không thấy nợ và cán bộ không sinh được mã. Chạy một lần cho mỗi (đơn vị, nguồn) rồi mỗi lần
/// cán bộ bấm "Đồng bộ lại"; sau đó event là đủ.
/// </summary>
public sealed class ChargeBackfill(ICrudDbContext db, IChargeSources sources, ChargeIngest ingest, TimeProvider clock, IServiceScopeFactory scopes)
{
    private const int MaxPages = 500;

    /// <summary>Số khoản mới/cập nhật đã đọc. <paramref name="force"/>: chạy lại cả khi đã đồng bộ.</summary>
    public async Task<int> RunAsync(long tenantId, bool force, CancellationToken ct)
    {
        var total = 0;
        foreach (var service in sources.Services)
        {
            var state = await db.Set<ChargeSyncState>().FirstOrDefaultAsync(s => s.SourceService == service, ct);
            if (state is not null && !force) continue;
            long? after = 0;
            for (var page = 0; after is not null && page < MaxPages; page++)
            {
                var result = await sources.OpenChargesAsync(service, tenantId, after.Value, ct);
                foreach (var e in result.Items)
                {
                    await ingest.ApplyAsync(e, ct);
                    total++;
                }
                await db.SaveChangesAsync(ct); // từng trang: dòng sau thấy dòng trước, và không giữ giao dịch dài
                after = result.Next;
            }
            if (state is null) db.Set<ChargeSyncState>().Add(state = ChargeSyncState.Create(service, clock.GetUtcNow()));
            else state.Touch(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }
        return total;
    }

    /// <summary>
    /// Khi cán bộ/bạn đọc không thấy khoản thu: đồng bộ lại nếu lần trước đã quá <paramref name="maxAge"/> (tránh dồn dập). Nguồn chưa sẵn sàng thì bỏ qua
    /// — người gọi báo "không tìm thấy" như bình thường, job nền sẽ thử lại.
    /// </summary>
    public async Task<bool> RefreshIfStaleAsync(long tenantId, TimeSpan maxAge, CancellationToken ct)
    {
        var newest = await db.Set<ChargeSyncState>().AsNoTracking().OrderByDescending(s => s.SyncedAt).Select(s => (DateTimeOffset?)s.SyncedAt).FirstOrDefaultAsync(ct);
        if (newest is { } at && clock.GetUtcNow() - at < maxAge) return false;
        try
        {
            await RunAsync(tenantId, force: true, ct);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>
    /// Làm tươi trong một scope + DbContext RIÊNG: đồng bộ có thể đụng chỉ mục duy nhất (hai bạn đọc cùng lúc, job nền, consumer cùng tạo một nguồn) — lỗi ấy
    /// không được làm hỏng DbContext của request đang gọi (thực thể thêm lỗi vẫn bị theo dõi và lần SaveChanges sau lại chèn lại). Request gọi xong
    /// truy vấn lại bằng DbContext của mình.
    /// </summary>
    public async Task<bool> RefreshIsolatedAsync(long tenantId, TimeSpan maxAge, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        try
        {
            return await scope.ServiceProvider.GetRequiredService<ChargeBackfill>().RefreshIfStaleAsync(tenantId, maxAge, ct);
        }
        catch (DbUpdateException)
        {
            return true; // bên khác vừa tạo cùng khoản — dữ liệu đã có, truy vấn lại là thấy
        }
    }

    /// <summary>Đơn vị đã đồng bộ ít nhất lần đầu với mọi nguồn chưa.</summary>
    public async Task<bool> IsSyncedAsync(CancellationToken ct) =>
        await db.Set<ChargeSyncState>().CountAsync(ct) >= sources.Services.Length;
}
