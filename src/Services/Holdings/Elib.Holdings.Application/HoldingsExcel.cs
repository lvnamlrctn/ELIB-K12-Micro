using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>File Excel của các màn kho — tên thư viện lấy từ bản sao đơn vị trong DB của holdings (khung file: <see cref="ReportExcel"/>).</summary>
public sealed class HoldingsExcel(ICrudDbContext db, ITenantContext tenant, TimeProvider clock) : ReportExcel(clock)
{
    public override async Task<string?> LibraryNameAsync(CancellationToken ct)
    {
        var id = tenant.RequireTenantId();
        return await db.Set<TenantReplicaRecord>().AsNoTracking().Where(t => t.TenantId == id).Select(t => t.Name).FirstOrDefaultAsync(ct);
    }
}
