using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.EntityFrameworkCore;

namespace Elib.Catalog.Application;

/// <summary>Trạng thái đồng bộ dữ liệu dùng chung cho màn của đơn vị SHARED: <see cref="Last"/> = lượt chạy gần nhất của service này (null = chưa chạy lượt nào).</summary>
public sealed record SharedSyncStatusDto(bool IsShared, SharedSyncResult? Last);

/// <summary>Xem trạng thái / yêu cầu chạy đồng bộ ngay — chỉ đơn vị mang mã <see cref="SharedTenant.Code"/> (mã dành riêng).</summary>
public sealed class SharedSyncControl(ICrudDbContext db, ITenantContext tenant, SharedSyncMonitor monitor)
{
    public async Task<SharedSyncStatusDto> StatusAsync(CancellationToken ct)
    {
        await EnsureSharedAsync(ct);
        return new SharedSyncStatusDto(true, monitor.Last);
    }

    public async Task<SharedSyncStatusDto> RunAsync(CancellationToken ct)
    {
        await EnsureSharedAsync(ct);
        monitor.RunNow();
        return new SharedSyncStatusDto(true, monitor.Last);
    }

    private async Task EnsureSharedAsync(CancellationToken ct)
    {
        var id = tenant.RequireTenantId();
        var code = await db.Set<TenantReplicaRecord>().AsNoTracking().Where(t => t.TenantId == id).Select(t => t.Code).FirstOrDefaultAsync(ct);
        if (!string.Equals(code, SharedTenant.Code, StringComparison.Ordinal))
            throw new BusinessRuleException("NOT_SHARED_TENANT", "Chỉ đơn vị dữ liệu dùng chung (mã SHARED) dùng được chức năng này.", 403);
    }
}
