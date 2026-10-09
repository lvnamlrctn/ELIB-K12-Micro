using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicCounterStatsResponse
{
    public long Total     { get; set; }
    public int  Today     { get; set; }
    public int  LastWeek  { get; set; }
    public int  LastMonth { get; set; }
}

public interface IPublicCounterRepository
{
    Task<PublicCounterStatsResponse> GetStatsAsync(Guid? departmentPublicId);
    Task TrackVisitAsync(Guid? departmentPublicId, string? ip);
}

public class PublicCounterRepository(ELIBAPIDbContext db, IMemoryCache cache, ILogger<PublicCounterRepository> logger) : IPublicCounterRepository
{
    private async Task<long?> ResolveDeptId(Guid? publicId)
    {
        if (!publicId.HasValue || publicId == Guid.Empty) return null;
        return await db.Tenants
            .Where(d => d.PublicId == publicId.Value && d.IsDelete != 2)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<PublicCounterStatsResponse> GetStatsAsync(Guid? departmentPublicId)
    {
        var deptId   = await ResolveDeptId(departmentPublicId);
        var cacheKey = $"counter_stats_{deptId}";
        if (cache.TryGetValue(cacheKey, out PublicCounterStatsResponse? cached) && cached != null)
            return cached;

        try
        {
            var today    = DateTime.Today;
            var weekAgo  = today.AddDays(-7);
            var monthAgo = today.AddMonths(-1);
            var tomorrow = today.AddDays(1);

            // Dòng Id nhỏ nhất theo department lưu tổng lũy kế
            var baseQ = db.Counters.Where(x => x.IsDelete != 2);
            if (deptId.HasValue) baseQ = baseQ.Where(x => x.TenantId == deptId);

            var firstId = await baseQ.OrderBy(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync();
            var total   = firstId.HasValue
                ? await db.Counters.Where(x => x.Id == firstId.Value).Select(x => x.CounterValue ?? 0).FirstOrDefaultAsync()
                : 0;

            // Các bản ghi còn lại là lượt truy cập chi tiết
            var detailQ = db.Counters.Where(x => x.IsDelete != 2 && x.Id != firstId);
            if (deptId.HasValue) detailQ = detailQ.Where(x => x.TenantId == deptId);

            var todayCount = await detailQ.CountAsync(x => x.Submited >= today    && x.Submited < tomorrow);
            var weekCount  = await detailQ.CountAsync(x => x.Submited >= weekAgo);
            var monthCount = await detailQ.CountAsync(x => x.Submited >= monthAgo);

            var result = new PublicCounterStatsResponse
            {
                Total     = total,
                Today     = todayCount,
                LastWeek  = weekCount,
                LastMonth = monthCount
            };

            cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));
            return result;
        }
        catch (Exception ex)
        {
            // Bảng Counter lớn/thiếu index có thể gây timeout — không để lỗi thống kê phá trang public
            logger.LogWarning(ex, "GetStatsAsync failed for department {DeptId}, returning empty stats", deptId);
            var fallback = new PublicCounterStatsResponse();
            cache.Set(cacheKey, fallback, TimeSpan.FromSeconds(30));
            return fallback;
        }
    }

    public async Task TrackVisitAsync(Guid? departmentPublicId, string? ip)
    {
        try
        {
            var deptId = await ResolveDeptId(departmentPublicId);

            // Tăng bản ghi accumulator (Id nhỏ nhất của department)
            var firstQ = db.Counters.Where(x => x.IsDelete != 2);
            if (deptId.HasValue) firstQ = firstQ.Where(x => x.TenantId == deptId);
            var first = await firstQ.OrderBy(x => x.Id).FirstOrDefaultAsync();
            if (first != null)
                first.CounterValue = (first.CounterValue ?? 0) + 1;

            db.Counters.Add(new Counter
            {
                CounterValue = 1,
                Submited     = DateTime.Now,
                Ip           = ip,
                TenantId = deptId,
                PublicId     = Guid.NewGuid()
            });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Ghi nhận lượt truy cập không quan trọng bằng việc phục vụ trang — không throw lên client
            logger.LogWarning(ex, "TrackVisitAsync failed for department {DepartmentPublicId}", departmentPublicId);
        }
    }
}

