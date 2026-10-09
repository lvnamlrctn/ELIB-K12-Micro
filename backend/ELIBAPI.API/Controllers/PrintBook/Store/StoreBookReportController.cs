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

// Báo cáo sách trong kho: sách hiện diện (không Mất/Thanh lý) đã nhận (ab_receipt.Receipt_Date) trong khoảng ngày, theo Kho.
// Logic dựng dữ liệu (BuildRowsAsync) đã chuyển sang StoreBookReportBuilder (Infrastructure) để dùng
// chung được với ScheduledReportEmailJob — controller chỉ còn phần HTTP + xuất Excel có letterhead+nhóm.
[Route("api/PrintBook/Store/BookReport")]
[Authorize]
public class StoreBookReportController(StoreBookReportBuilder builder, ISystemParameterService sysParam) : ControllerBase
{
    [HttpPost("Search")]
    [Permission("STORE_BOOK_REPORT", "view")]
    public async Task<IActionResult> Search([FromBody] StoreBookReportRequest r)
    {
        var (allRows, totalCount, titleCount, storeStats) = await builder.BuildRowsAsync(r, GetTenantId());
        var page = Math.Max(1, r.PageIndex);
        var size = Math.Max(1, r.PageSize > 0 ? r.PageSize : 20);
        var rows = allRows.Skip((Math.Max(page, 1) - 1) * size).Take(size).ToList();

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));
        var storeName     = await builder.GetStoreNameAsync(r.StoreId);
        var storeStatsDto = storeStats.Select(s => new { storeName = s.StoreName, titleCount = s.TitleCount, copyCount = s.CopyCount });

        return Ok(ApiResponse<object>.Ok(new { headers = StoreBookReportBuilder.Headers, rows, totalCount, titleCount, parentLibrary, libraryName, storeName, storeStats = storeStatsDto }));
    }

    [HttpPost("Export")]
    [Permission("STORE_BOOK_REPORT", "view")]
    public async Task<IActionResult> Export([FromBody] StoreBookReportRequest r)
    {
        var (rows, _, _, storeStats) = await builder.BuildRowsAsync(r, GetTenantId());
        var statsByStore = storeStats.ToDictionary(s => s.StoreName, s => (s.TitleCount, s.CopyCount));

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));
        var storeName     = await builder.GetStoreNameAsync(r.StoreId);
        var headers       = StoreBookReportBuilder.Headers;

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Sách trong kho");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH SÁCH TRONG KHO", headers.Length);

        var allStores = !r.StoreId.HasValue;

        var subtitleRange = ws.Range(startRow, 1, startRow, headers.Length);
        subtitleRange.Merge();
        ws.Cell(startRow, 1).Value = BuildSubtitle(storeName, allStores, r.DateFrom, r.DateTo);
        ws.Cell(startRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var headerRowIdx = startRow + 1;
        for (var i = 0; i < headers.Length; i++) ws.Cell(headerRowIdx, i + 1).Value = headers[i];
        var headerRow = ws.Row(headerRowIdx);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

        var row = headerRowIdx + 1;
        if (allStores)
        {
            foreach (var group in rows.GroupBy(x => x[^1]))
            {
                var groupName = string.IsNullOrEmpty(group.Key) ? "(Không xác định)" : group.Key;
                var (gTitleCount, gCopyCount) = statsByStore.TryGetValue(group.Key, out var gStats) ? gStats : (0, group.Count());

                var groupRange = ws.Range(row, 1, row, headers.Length);
                groupRange.Merge();
                ws.Cell(row, 1).Value = $"Kho: {groupName}   —   Số đầu sách: {gTitleCount}   —   Số bản: {gCopyCount}";
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;

                foreach (var values in group)
                {
                    for (var i = 0; i < values.Length; i++) ws.Cell(row, i + 1).Value = values[i];
                    row++;
                }
            }
        }
        else
        {
            foreach (var values in rows)
            {
                for (var i = 0; i < values.Length; i++) ws.Cell(row, i + 1).Value = values[i];
                row++;
            }
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"sach-trong-kho-{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    private static string BuildSubtitle(string? storeName, bool allStores, DateTime? from, DateTime? to)
    {
        var parts = new List<string>();
        if (allStores) parts.Add("Tất cả các kho");
        else if (!string.IsNullOrEmpty(storeName)) parts.Add($"Kho: {storeName}");
        if (from.HasValue || to.HasValue)
            parts.Add($"Từ ngày {from?.ToString("dd/MM/yyyy") ?? "…"} đến ngày {to?.ToString("dd/MM/yyyy") ?? "…"}");
        return string.Join("   —   ", parts);
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}
