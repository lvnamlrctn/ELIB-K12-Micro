using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

/// <summary>Trạng thái đồng bộ dữ liệu dùng chung — màn của đơn vị SHARED. <see cref="LastRunAt"/> null = chưa chạy lượt nào kể từ khi service khởi động.</summary>
public sealed record SharedSyncStatus(bool IsShared, DateTimeOffset? LastRunAt, int Applied, int Pending, int Failed, string? Error);

/// <summary>Trạng thái và nút "Đồng bộ ngay" cho đơn vị SHARED (đơn vị khác không dùng được: <c>NOT_SHARED_TENANT</c>).</summary>
public sealed class SharedSyncControl(ICrudDbContext db, ITenantContext tenant, SharedSyncMonitor monitor)
{
    public async Task<SharedSyncStatus> StatusAsync(CancellationToken ct)
    {
        await RequireSharedAsync(ct);
        var last = monitor.Last;
        return new SharedSyncStatus(true, last?.At, last?.Applied ?? 0, last?.Pending ?? 0, last?.Failed ?? 0, last?.Error);
    }

    public async Task<SharedSyncStatus> RunAsync(CancellationToken ct)
    {
        await RequireSharedAsync(ct);
        monitor.RunNow();
        return await StatusAsync(ct);
    }

    private async Task RequireSharedAsync(CancellationToken ct)
    {
        var id = tenant.RequireTenantId();
        if (!await db.Set<TenantReplicaRecord>().AsNoTracking().AnyAsync(t => t.TenantId == id && t.Code == SharedTenant.Code, ct))
            throw new BusinessRuleException("NOT_SHARED_TENANT", "Chỉ đơn vị dữ liệu dùng chung (mã SHARED) mới quản lý đồng bộ.", 403);
    }
}
