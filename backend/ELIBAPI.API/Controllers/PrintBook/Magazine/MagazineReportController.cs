using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

/// <summary>Báo cáo báo tạp chí — xem <see cref="ISerialReportService"/>. Tên trường JSON theo màn hình (subscriptionTitle, issn,
/// serialSeq, publishedDate…); trước đây API trả title/receivedCount/SERIAL_SEQ nên các cột trên màn hình trống.</summary>
[Route("api/PrintBook/Magazine/Report")]
[Authorize]
public class MagazineReportController(ISerialReportService reports, ISystemParameterService sysParam, ELIBAPIDbContext db) : BaseApiController
{
    private async Task<SerialReportFilter> Filter(MagazineReportRequest r)
    {
        var scope = await TenantScopeHelper.ResolveScopeAsync(db, r.TenantId, GetTenantId(), IsPrivilegedRole());
        return new(r.SubscriptionCode, r.DateFrom, r.DateTo, scope.TenantId, scope.IncludeShared, scope.All);
    }

    private static string StatusText(int? status) => status switch
    {
        1 => "Đã nhận", 2 => "Khiếu nại", 3 => "Thiếu", _ => "Dự kiến"
    };

    [HttpPost("ReceivedSummary")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> ReceivedSummary([FromBody] MagazineReportRequest r) =>
        Paged(await reports.ReceivedSummaryAsync(await Filter(r), r.PageIndex, r.PageSize), r);

    [HttpPost("ReceivedDetail")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> ReceivedDetail([FromBody] MagazineReportRequest r) =>
        Paged(await reports.ReceivedDetailAsync(await Filter(r), r.PageIndex, r.PageSize), r);

    [HttpPost("MissingClaimList")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> MissingClaimList([FromBody] MagazineReportRequest r) =>
        Paged(await reports.MissingClaimAsync(await Filter(r), r.PageIndex, r.PageSize), r);

    private OkObjectResult Paged<T>((List<T> Items, int Total) page, MagazineReportRequest r) =>
        Ok(ApiResponse<object>.Ok(new { items = page.Items, totalCount = page.Total, pageIndex = r.PageIndex ?? 1, pageSize = r.PageSize ?? 20 }));

    [HttpPost("ReceivedSummaryExport")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> ReceivedSummaryExport([FromBody] MagazineReportRequest r)
    {
        var (items, _) = await reports.ReceivedSummaryAsync(await Filter(r), 1, SerialReportService.ExportLimit);
        return await ExcelAsync("Tổng hợp nhận", "BÁO CÁO NHẬN ẤN PHẨM ĐỊNH KỲ (TỔNG HỢP)", "ReceivedSummary.xlsx",
            ["Mã đơn đặt", "Nhan đề", "ISSN", "Số kỳ nhận", "Số bản nhận", "Nhận lần cuối"],
            items.Select(g => new object?[] { g.SubscriptionId, g.SubscriptionTitle, g.Issn, g.ReceivedIssues, g.Quantity, g.LastReceived?.ToString("dd/MM/yyyy") }));
    }

    [HttpPost("ReceivedDetailExport")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> ReceivedDetailExport([FromBody] MagazineReportRequest r)
    {
        var (items, _) = await reports.ReceivedDetailAsync(await Filter(r), 1, SerialReportService.ExportLimit);
        return await ExcelAsync("Chi tiết nhận", "BÁO CÁO NHẬN ẤN PHẨM ĐỊNH KỲ (CHI TIẾT)", "ReceivedDetail.xlsx",
            ["Mã đơn đặt", "Nhan đề", "Kỳ số", "Ngày nhận", "Số lượng"],
            items.Select(i => new object?[] { i.SubscriptionId, i.SubscriptionTitle, i.SerialSeq, i.PublishedDate?.ToString("dd/MM/yyyy"), i.Quantity }));
    }

    [HttpPost("MissingClaimExport")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> MissingClaimExport([FromBody] MagazineReportRequest r)
    {
        var (items, _) = await reports.MissingClaimAsync(await Filter(r), 1, SerialReportService.ExportLimit);
        return await ExcelAsync("Danh sách đòi bổ sung", "DANH SÁCH ẤN PHẨM THIẾU/KHIẾU NẠI", "MissingClaim.xlsx",
            ["Mã đơn đặt", "Nhan đề", "Kỳ số", "Ngày dự kiến", "Ngày đòi", "Số lần đòi", "Trạng thái"],
            items.Select(i => new object?[] { i.SubscriptionId, i.SubscriptionTitle, i.SerialSeq, i.PlannedDate?.ToString("dd/MM/yyyy"),
                i.ClaimDate?.ToString("dd/MM/yyyy"), i.ClaimCount, StatusText(i.Status) }));
    }

    private async Task<FileContentResult> ExcelAsync(string sheet, string heading, string fileName, string[] headers, IEnumerable<object?[]> rows)
    {
        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));
        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add(sheet);
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, heading, headers.Length);
        for (var c = 0; c < headers.Length; c++) ws.Cell(startRow, c + 1).Value = headers[c];
        var row = startRow + 1;
        foreach (var values in rows)
        {
            for (var c = 0; c < values.Length; c++) ws.Cell(row, c + 1).Value = XLCellValue.FromObject(values[c]);
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, headers.Length);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}

public class MagazineReportRequest
{
    public string?   SubscriptionCode { get; set; }
    public DateTime? DateFrom         { get; set; }
    public DateTime? DateTo           { get; set; }
    public int?      PageIndex        { get; set; }
    public int?      PageSize         { get; set; }
    /// <summary>Đơn vị do tài khoản đặc quyền chọn (PublicId); user thường bị ép theo JWT.</summary>
    public Guid?     TenantId         { get; set; }
}
