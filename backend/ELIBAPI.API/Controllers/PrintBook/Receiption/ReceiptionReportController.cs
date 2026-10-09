using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Receiption/Report")]
[Authorize]
public class ReceiptionReportController(ELIBAPIDbContext db, ISystemParameterService sysParam) : ControllerBase
{
    [HttpPost("ReaderCount")]
    [Permission("RECEIPTION_REPORT", "view")]
    public async Task<IActionResult> ReaderCount([FromBody] ReaderCountReportRequest r)
    {
        var rows = await BuildReaderCountRows(r, r.Top is > 0 ? r.Top.Value : 100);

        int pageIndex = r.PageIndex ?? 1;
        int pageSize  = r.PageSize  ?? 20;
        var paged = rows.Skip((Math.Max(pageIndex, 1) - 1) * pageSize).Take(pageSize).ToList();

        return Ok(ApiResponse<object>.Ok(new { items = paged, totalCount = rows.Count, pageIndex, pageSize }));
    }

    [HttpPost("ReaderCountExport")]
    [Permission("RECEIPTION_REPORT", "view")]
    public async Task<IActionResult> ReaderCountExport([FromBody] ReaderCountReportRequest r)
    {
        var rows = await BuildReaderCountRows(r, r.Top is > 0 ? r.Top.Value : 5000);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Thống kê bạn đọc");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "BÁO CÁO THỐNG KÊ LƯỢT BẠN ĐỌC", 5);
        ws.Cell(startRow, 1).Value = "Số thẻ";
        ws.Cell(startRow, 2).Value = "Họ tên";
        ws.Cell(startRow, 3).Value = "Loại bạn đọc";
        ws.Cell(startRow, 4).Value = "Lớp";
        ws.Cell(startRow, 5).Value = "Số lượt vào cửa";

        int row = startRow + 1;
        foreach (var x in rows)
        {
            ws.Cell(row, 1).Value = x.cardNo ?? "";
            ws.Cell(row, 2).Value = x.fullName ?? "";
            ws.Cell(row, 3).Value = x.readerType ?? "";
            ws.Cell(row, 4).Value = x.className ?? "";
            ws.Cell(row, 5).Value = x.checkInCount;
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, 5);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "receiption-report.xlsx");
    }

    private async Task<List<ReaderCountRow>> BuildReaderCountRows(ReaderCountReportRequest r, int take)
    {
        var query = from co in db.CheckOuts
                    join rd in db.Readers on co.ReaderId equals rd.Id into readers
                    from rd in readers.DefaultIfEmpty()
                    where co.IsDelete != 2
                    select new { co, rd };

        var jwtTenantId = GetTenantId();
        if (jwtTenantId.HasValue) query = query.Where(x => x.co.TenantId == jwtTenantId);

        if (r.CircPlaceId.HasValue && r.CircPlaceId > 0) query = query.Where(x => x.co.StoreId == r.CircPlaceId);
        if (DateTime.TryParse(r.FromDate, out var fromDate)) query = query.Where(x => x.co.CheckInTime >= fromDate);
        if (DateTime.TryParse(r.ToDate, out var toDate))     query = query.Where(x => x.co.CheckInTime <= toDate);

        var grouped = await query
            .GroupBy(x => new { x.rd.Id, x.rd.Cardno, x.rd.FirstName, x.rd.LastName, x.rd.ReaderTypeId, x.rd.ClassId })
            .Select(g => new
            {
                cardNo       = g.Key.Cardno,
                fullName     = (g.Key.FirstName + " " + g.Key.LastName).Trim(),
                readerTypeId = g.Key.ReaderTypeId,
                classId      = g.Key.ClassId,
                checkInCount = g.Count()
            })
            .OrderByDescending(x => x.checkInCount)
            .Take(take)
            .ToListAsync();

        var readerTypeIds = grouped.Where(x => x.readerTypeId.HasValue).Select(x => x.readerTypeId!.Value).Distinct().ToList();
        var classIds      = grouped.Where(x => x.classId.HasValue).Select(x => x.classId!.Value).Distinct().ToList();
        var readerTypeNames = await db.ReaderTypes.Where(t => readerTypeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);
        var classNames      = await db.Classes.Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

        return grouped
            .Select(x => new ReaderCountRow(
                x.cardNo,
                x.fullName,
                x.readerTypeId.HasValue && readerTypeNames.TryGetValue(x.readerTypeId.Value, out var rtn) ? rtn : null,
                x.classId.HasValue && classNames.TryGetValue(x.classId.Value, out var cn) ? cn : null,
                x.checkInCount))
            .ToList();
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}

public record ReaderCountRow(string? cardNo, string? fullName, string? readerType, string? className, int checkInCount);

public class ReaderCountReportRequest
{
    public long?   CircPlaceId { get; set; }
    public string? FromDate    { get; set; }
    public string? ToDate      { get; set; }
    public int?    Top         { get; set; }
    public int?    PageIndex   { get; set; }
    public int?    PageSize    { get; set; }
}
