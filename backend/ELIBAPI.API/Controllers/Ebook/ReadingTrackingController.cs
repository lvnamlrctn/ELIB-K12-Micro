using System.Security.Claims;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/ReadingTracking")]
[Authorize]
public class ReadingTrackingController(ELIBAPIDbContext db, IConfiguration config) : ControllerBase
{
    [HttpPost("Statistics")]
    [Permission("READING_TRACKING", "view")]
    public async Task<IActionResult> Statistics([FromBody] ReadingTrackingStatisticsRequest r)
    {
        var tenantId = GetEffectiveTenantId();

        var query = db.EbookLogs
            .Where(x => x.Type == 1 && x.IsDelete != 2)
            .Where(x => tenantId == null || x.TenantId == tenantId);

        if (r.FromDate.HasValue) query = query.Where(x => x.Submited >= r.FromDate);
        if (r.ToDate.HasValue)   query = query.Where(x => x.Submited <= r.ToDate);

        var totalReads = await query.CountAsync();
        var totalDocuments = await query.Select(x => x.Bookid).Distinct().CountAsync();
        var totalReaders = await query.Select(x => x.ReaderId).Distinct().CountAsync();

        string[] labels;
        int[] counts;

        if (r.GroupBy?.ToLower() == "day")
        {
            var grouped = await query
                .Where(x => x.Submited != null)
                .GroupBy(x => x.Submited!.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            labels = grouped.Select(g => g.Date.ToString("dd/MM")).ToArray();
            counts = grouped.Select(g => g.Count).ToArray();
        }
        else
        {
            var grouped = await query
                .Where(x => x.Submited != null)
                .GroupBy(x => new { x.Submited!.Value.Year, x.Submited!.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            labels = grouped.Select(g => $"T{g.Month}").ToArray();
            counts = grouped.Select(g => g.Count).ToArray();
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            labels,
            counts,
            totalReads,
            totalDocuments,
            totalReaders
        }));
    }

    [HttpPost("Search")]
    [Permission("READING_TRACKING", "view")]
    public async Task<IActionResult> Search([FromBody] ReadingTrackingSearchRequest r)
    {
        var tenantId = GetEffectiveTenantId();

        var query = from log in db.EbookLogs
                    join reader in db.Readers on log.ReaderId equals reader.Id into readers
                    from reader in readers.DefaultIfEmpty()
                    join itemXml in db.Set<EbookItemXml>() on log.Bookid equals itemXml.Id into xmls
                    from itemXml in xmls.DefaultIfEmpty()
                    where log.Type == 1 && log.IsDelete != 2
                    where tenantId == null || log.TenantId == tenantId
                    select new { log, reader, itemXml };

        if (r.FromDate.HasValue) query = query.Where(x => x.log.Submited >= r.FromDate);
        if (r.ToDate.HasValue)   query = query.Where(x => x.log.Submited <= r.ToDate);

        if (!string.IsNullOrWhiteSpace(r.Keyword))
        {
            var kw = r.Keyword.Trim();
            query = query.Where(x =>
                (x.reader.LastName + " " + x.reader.FirstName).Contains(kw)
                || (x.itemXml != null && x.itemXml.Title != null && x.itemXml.Title.Contains(kw)));
        }

        var recordsTotal = await query.CountAsync();

        var pageIndex = r.PageIndex < 1 ? 1 : r.PageIndex;
        var pageSize = r.PageSize < 1 ? 10 : r.PageSize;

        var items = await query
            .OrderByDescending(x => x.log.Submited)
            .Skip((Math.Max(pageIndex, 1) - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                readerName = x.reader != null
                    ? (x.reader.LastName ?? "") + " " + (x.reader.FirstName ?? "")
                    : "",
                itemTitle = x.itemXml != null ? x.itemXml.Title ?? "" : "",
                readAt = x.log.Submited,
                duration = 0
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, recordsTotal }));
    }

    private long? GetEffectiveTenantId()
    {
        var tenantId = long.TryParse(User.FindFirstValue("TenantId"), out var tid) ? tid : (long?)null;
        var roleCode   = User.FindFirstValue("RoleCode");
        var tenantCode = User.FindFirstValue("TenantCode");
        var adminRoles  = config.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        var roleCodes   = config.GetSection("ReadOnlyPolicy:RoleCodes").Get<string[]>() ?? [];
        var tenantCodes = config.GetSection("ReadOnlyPolicy:TenantCodes").Get<string[]>() ?? [];
        if ((roleCode != null && adminRoles.Concat(roleCodes).Contains(roleCode, StringComparer.OrdinalIgnoreCase))
            || (tenantCode != null && tenantCodes.Contains(tenantCode, StringComparer.OrdinalIgnoreCase)))
            tenantId = null;
        return tenantId;
    }
}

public class ReadingTrackingStatisticsRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? GroupBy { get; set; }
}

public class ReadingTrackingSearchRequest
{
    public string? Keyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
