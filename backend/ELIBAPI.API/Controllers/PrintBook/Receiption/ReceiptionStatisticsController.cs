using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Receiption/Statistics")]
[Authorize]
public class ReceiptionStatisticsController(ELIBAPIDbContext db, ISystemParameterService sysParam) : ControllerBase
{
    [HttpPost("CheckIn")]
    [Permission("CHECKIN_STATS", "view")]
    public async Task<IActionResult> CheckInStats([FromBody] ReceiptionStatsRequest r)
    {
        var items = await BuildCheckInStats(r);
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpPost("CheckInExport")]
    [Permission("CHECKIN_STATS", "view")]
    public async Task<IActionResult> CheckInExport([FromBody] ReceiptionStatsRequest r)
    {
        var items = await BuildCheckInStats(r);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Thống kê vào cửa");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "THỐNG KÊ LƯỢT VÀO CỔNG", 2);
        ws.Cell(startRow, 1).Value = "Tiêu chí";
        ws.Cell(startRow, 2).Value = "Số lượt";

        int row = startRow + 1;
        foreach (var x in items)
        {
            ws.Cell(row, 1).Value = x.label;
            ws.Cell(row, 2).Value = x.count;
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, 2);

        return ExcelFile(wb, "checkin-statistics.xlsx");
    }

    private async Task<List<CheckInStatRow>> BuildCheckInStats(ReceiptionStatsRequest r)
    {
        var query =
            from co in db.CheckOuts
            where co.IsDelete != 2
            join rd in db.Readers on co.ReaderId equals rd.Id into rdj
            from rd in rdj.DefaultIfEmpty()
            select new { co, rd };

        var jwtTenantId = GetTenantId();
        if (jwtTenantId.HasValue) query = query.Where(x => x.co.TenantId == jwtTenantId);

        if (r.CircPlaceId.HasValue && r.CircPlaceId > 0) query = query.Where(x => x.co.StoreId == r.CircPlaceId);
        if (DateTime.TryParse(r.ReceiptDateFrom, out var fromDate)) query = query.Where(x => x.co.CheckInTime >= fromDate);
        if (DateTime.TryParse(r.ReceiptDateTo, out var toDate))     query = query.Where(x => x.co.CheckInTime <= toDate);

        var rows = await query.ToListAsync();

        Func<Reader?, long?> keySelector = r.Criterion switch
        {
            "class"      => rd => rd?.ClassId,
            "course"     => rd => rd?.CourseId,
            "department" => rd => rd?.OrgId,
            _            => rd => rd?.ReaderTypeId
        };
        var grouped = rows.GroupBy(x => keySelector(x.rd))
            .Select(g => new { key = g.Key, count = g.Count() }).ToList();

        var keys = grouped.Where(g => g.key.HasValue).Select(g => g.key!.Value).Distinct().ToList();
        var names = r.Criterion switch
        {
            "class"      => await db.Classes.Where(x => keys.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name),
            "course"     => await db.Courses.Where(x => keys.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name),
            "department" => await db.Orgs.Where(x => keys.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name),
            _            => await db.ReaderTypes.Where(x => keys.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name)
        };

        return grouped
            .Select(g => new CheckInStatRow(
                g.key?.ToString() ?? "",
                g.key.HasValue && names.TryGetValue(g.key.Value, out var n) ? n ?? "Không xác định" : "Không xác định",
                g.count))
            .OrderByDescending(x => x.count).ToList();
    }

    private static FileContentResult ExcelFile(XLWorkbook wb, string fileName)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new FileContentResult(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        { FileDownloadName = fileName };
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}

public class ReceiptionStatsRequest
{
    public string? Criterion       { get; set; }
    public long?   CircPlaceId     { get; set; }
    public string? ReceiptDateFrom { get; set; }
    public string? ReceiptDateTo   { get; set; }
}

public record CheckInStatRow(string key, string label, int count);
