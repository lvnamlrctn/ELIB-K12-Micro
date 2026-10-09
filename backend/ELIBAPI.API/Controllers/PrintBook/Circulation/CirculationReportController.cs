using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

// Báo cáo lưu thông: 1 màn hình, 11 loại báo cáo chọn qua ReportType, cột động theo từng loại.
// Logic dựng dữ liệu (BuildXxxAsync) đã chuyển sang CirculationReportBuilder (Infrastructure) để dùng
// chung được với ScheduledReportEmailJob — controller chỉ còn phần HTTP + xuất Excel có letterhead.
[Route("api/PrintBook/Circulation/Report")]
[Authorize]
public class CirculationReportController(CirculationReportBuilder builder, ISystemParameterService sysParam) : ControllerBase
{
    [HttpPost("Search")]
    [Permission("CIRC_REPORT", "view")]
    public async Task<IActionResult> Search([FromBody] CirculationReportRequest r)
    {
        var (headers, allRows) = await builder.BuildReportAsync(r, GetTenantId());
        var page = Math.Max(1, r.PageIndex);
        var size = Math.Max(1, r.PageSize > 0 ? r.PageSize : 20);
        var rows = allRows.Skip((Math.Max(page, 1) - 1) * size).Take(size).ToList();

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        // Dòng tổng tính trên toàn bộ dữ liệu, không phải chỉ trang đang xem.
        var totalRow = CirculationReportBuilder.TotalRow(r.ReportType, allRows);
        return Ok(ApiResponse<object>.Ok(new { headers, rows, totalCount = allRows.Count, totalRow, parentLibrary, libraryName }));
    }

    [HttpPost("Export")]
    [Permission("CIRC_REPORT", "view")]
    public async Task<IActionResult> Export([FromBody] CirculationReportRequest r)
    {
        var (headers, rows) = await builder.BuildReportAsync(r, GetTenantId());

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Báo cáo lưu thông");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, CirculationReportBuilder.ReportTitle(r.ReportType), headers.Length);
        for (var i = 0; i < headers.Length; i++) ws.Cell(startRow, i + 1).Value = headers[i];
        var headerRow = ws.Row(startRow);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

        var row = startRow + 1;
        foreach (var values in rows)
        {
            for (var i = 0; i < values.Length; i++) ws.Cell(row, i + 1).Value = values[i];
            row++;
        }
        if (CirculationReportBuilder.TotalRow(r.ReportType, rows) is { } total)
        {
            for (var i = 0; i < total.Length; i++) ws.Cell(row, i + 1).Value = total[i];
            ws.Row(row).Style.Font.Bold = true;
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"bao-cao-luu-thong-{LibraryClock.Now:yyyyMMddHHmmss}.xlsx");
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}
