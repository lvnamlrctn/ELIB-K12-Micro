using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Circulation/History")]
[Authorize]
public class CirculationHistoryController(ELIBAPIDbContext db, ISystemParameterService sysParam) : ControllerBase
{
    [HttpPost("Search")]
    [Permission("LOAN_HISTORY", "view")]
    public async Task<IActionResult> Search([FromBody] LoanHistorySearchRequest r)
    {
        var page = Math.Max(1, r.PageIndex);
        var size = Math.Max(1, r.PageSize > 0 ? r.PageSize : 20);

        var (items, total) = await BuildLoanHistoryRows(r, page, size);
        return Ok(ApiResponse<object>.Ok(new { items, totalCount = total, pageIndex = page, pageSize = size }));
    }

    private static readonly string[] ExportHeaders =
        ["STT", "Số thẻ", "Họ tên", "Barcode", "Tên sách", "Ngày mượn", "Hạn trả", "Ngày trả", "Người thực hiện", "Trạng thái"];

    private static string[] ExportRowValues(int stt, LoanHistoryRow x) =>
    [
        stt.ToString(),
        x.cardNo ?? "",
        x.readerName ?? "",
        x.barcode ?? "",
        x.bibTitle ?? "",
        x.borrowDate?.ToString("dd/MM/yyyy") ?? "",
        x.dueDate?.ToString("dd/MM/yyyy") ?? "",
        x.returnDate?.ToString("dd/MM/yyyy") ?? "",
        x.staffName ?? "",
        x.statusName
    ];

    [HttpPost("Export")]
    [Permission("LOAN_HISTORY", "view")]
    public async Task<IActionResult> Export([FromBody] LoanHistorySearchRequest r)
    {
        var (items, _) = await BuildLoanHistoryRows(r, null, null);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Lịch sử mượn trả");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "LỊCH SỬ MƯỢN TRẢ TÀI LIỆU", ExportHeaders.Length);
        for (var i = 0; i < ExportHeaders.Length; i++)
            ws.Cell(startRow, i + 1).Value = ExportHeaders[i];
        var headerRow = ws.Row(startRow);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

        var row = startRow + 1;
        foreach (var x in items)
        {
            var values = ExportRowValues(row - startRow, x);
            for (var i = 0; i < values.Length; i++)
                ws.Cell(row, i + 1).Value = values[i];
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, ExportHeaders.Length);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"lich-su-muon-tra-{LibraryClock.Now:yyyyMMddHHmmss}.xlsx");
    }

    [HttpPost("ExportPdf")]
    [Permission("LOAN_HISTORY", "view")]
    public async Task<IActionResult> ExportPdf([FromBody] LoanHistorySearchRequest r)
    {
        var (items, _) = await BuildLoanHistoryRows(r, null, null);

        var bytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Text("Lịch sử mượn trả").FontSize(14).Bold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);
                        foreach (var _ in ExportHeaders.Skip(1)) columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in ExportHeaders)
                            header.Cell().Background(Colors.Blue.Lighten3).Padding(4).Text(h).Bold();
                    });

                    var stt = 1;
                    foreach (var x in items)
                    {
                        foreach (var v in ExportRowValues(stt, x))
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(v);
                        stt++;
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();

        return File(bytes, "application/pdf", $"lich-su-muon-tra-{LibraryClock.Now:yyyyMMddHHmmss}.pdf");
    }

    private async Task<(List<LoanHistoryRow> items, int total)> BuildLoanHistoryRows(LoanHistorySearchRequest r, int? pageIndex, int? pageSize)
    {
        var today = LibraryClock.Today;

        var query =
            from bo in db.BookOuts
            where bo.IsDelete != 2
            join rd in db.Readers on bo.ReaderId equals rd.Id into rdj
            from rd in rdj.DefaultIfEmpty()
            join bc in db.Barcodes
                on new { K = bo.Barcode, T = bo.TenantId ?? 0 } equals new { K = bc.BarcodeValue, T = bc.TenantId ?? 0 } into bcj
            from bc in bcj.DefaultIfEmpty()
            join bx in db.BibXmls on bc.BibId equals bx.BibId into bxj
            from bx in bxj.DefaultIfEmpty()
            join st in db.Stores on bo.Store equals st.Id into stj
            from st in stj.DefaultIfEmpty()
            select new
            {
                bo, rd, bx, st,
                returnDate = db.BookIns.Where(bi => bi.BookOutId == bo.Id).Max(bi => (DateTime?)bi.ReturnDate),
                // Người thực hiện (port ELIB-LRC 10-03): cán bộ nhận trả (lượt trả mới nhất); chưa trả hoặc lượt trả không
                // ghi người thì cán bộ cho mượn. Không thêm cột CSDL — dùng UserId/CreatedRowBy luồng mượn/trả đã ghi sẵn.
                staffId = db.BookIns.Where(bi => bi.BookOutId == bo.Id && bi.IsDelete != 2).OrderByDescending(bi => bi.Id)
                              .Select(bi => (long?)bi.UserId ?? bi.CreatedRowBy).FirstOrDefault() ?? bo.UserId ?? bo.CreatedRowBy
            };

        var jwtTenantId = GetTenantId();
        if (jwtTenantId.HasValue) query = query.Where(x => x.bo.TenantId == jwtTenantId);

        if (!string.IsNullOrEmpty(r.CardNo))
        {
            var cn = r.CardNo.ToLower();
            query = query.Where(x => x.rd != null && x.rd.Cardno != null && x.rd.Cardno.ToLower() == cn);
        }
        if (!string.IsNullOrEmpty(r.Title))
        {
            var kw = r.Title.ToLower();
            query = query.Where(x => x.bx != null && x.bx.Title != null && x.bx.Title.ToLower().Contains(kw));
        }
        if (!string.IsNullOrEmpty(r.Barcode)) query = query.Where(x => x.bo.Barcode == r.Barcode);
        if (r.BibId.HasValue) query = query.Where(x => x.bx != null && x.bx.BibId == r.BibId);
        if (r.BorrowDateFrom.HasValue) query = query.Where(x => x.bo.BorrowDate >= r.BorrowDateFrom);
        if (r.BorrowDateTo.HasValue)   query = query.Where(x => x.bo.BorrowDate <= r.BorrowDateTo);
        if (r.CircPlaceId.HasValue)    query = query.Where(x => x.bo.CircPlace == r.CircPlaceId);
        if (r.ReaderTypeId.HasValue)   query = query.Where(x => x.rd != null && x.rd.ReaderTypeId == r.ReaderTypeId);
        if (r.OrgId.HasValue)          query = query.Where(x => x.rd != null && x.rd.OrgId == r.OrgId);
        // BookOut đã lọc theo đơn vị ở trên → kho của đơn vị khác chỉ cho kết quả rỗng.
        if (r.StoreId.HasValue)        query = query.Where(x => x.bo.Store == r.StoreId);
        if (r.ClassId.HasValue)        query = query.Where(x => x.rd != null && x.rd.ClassId == r.ClassId);
        if (r.CourseId.HasValue)       query = query.Where(x => x.rd != null && x.rd.CourseId == r.CourseId);
        // Lọc theo đúng giá trị cột "Người thực hiện" để cột và bộ lọc luôn khớp nhau.
        if (r.OperatorId.HasValue)     query = query.Where(x => x.staffId == r.OperatorId);
        if (r.DueDateFrom.HasValue)    query = query.Where(x => x.bo.DueDate >= r.DueDateFrom);
        if (r.DueDateTo.HasValue)      query = query.Where(x => x.bo.DueDate <= r.DueDateTo);
        if (r.ReturnDateFrom.HasValue) query = query.Where(x => x.returnDate >= r.ReturnDateFrom);
        if (r.ReturnDateTo.HasValue)   query = query.Where(x => x.returnDate <= r.ReturnDateTo);
        if (r.IsOverdue == true)
            query = query.Where(x => x.bo.DueDate.HasValue && x.bo.DueDate < today && x.returnDate == null);
        if (r.Status.HasValue)
        {
            query = r.Status.Value switch
            {
                // Đang mượn = Status khác "R" (dữ liệu cũ lưu "1"/"O").
                1 => query.Where(x => x.bo.Status != "R" && (x.bo.DueDate == null || x.bo.DueDate >= today)),
                2 => query.Where(x => x.bo.Status == "R"),
                3 => query.Where(x => x.bo.Status != "R" && x.bo.DueDate < today),
                // 4 = đã trả nhưng trả muộn (port ELIB-LRC: tách "quá hạn đang mượn" / "trả muộn").
                4 => query.Where(x => x.bo.Status == "R" && x.bo.DueDate != null && x.returnDate != null
                                      && x.returnDate.Value.Date > x.bo.DueDate.Value.Date),
                _ => query.Where(x => false)
            };
        }

        var total = await query.CountAsync();

        var ordered = query.OrderByDescending(x => x.bo.BorrowDate);
        var limited = pageIndex.HasValue || pageSize.HasValue
            ? ordered.Skip(((pageIndex ?? 1) - 1) * (pageSize ?? 20)).Take(pageSize ?? 20)
            : ordered.Take(5000);

        var rows = await limited.ToListAsync();

        // Tên cán bộ: 1 truy vấn riêng cho đúng các dòng của trang (không join vào truy vấn lịch sử lớn).
        var staffIds = rows.Where(x => x.staffId.HasValue).Select(x => x.staffId!.Value).Distinct().ToList();
        var staffNames = staffIds.Count == 0 ? new Dictionary<long, string>() : (await db.Users
                .Where(u => staffIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.LoginName }).ToListAsync())
            .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? (u.LoginName ?? "").Trim() : u.FullName.Trim());

        var items = rows.Select(x =>
        {
            var (status, statusName) = x.bo.Status switch
            {
                "R" => (2, "Đã trả"),
                _ when x.bo.DueDate.HasValue && x.bo.DueDate < today => (3, "Quá hạn"),
                _ => (1, "Đang mượn")
            };
            return new LoanHistoryRow(
                x.bo.Id,
                x.bo.PublicId,
                x.rd?.Cardno,
                x.rd != null ? $"{x.rd.FirstName} {x.rd.LastName}".Trim() : null,
                x.bo.Barcode,
                x.bx?.Title,
                x.bx?.DDC,
                x.bo.BorrowDate,
                x.bo.DueDate,
                x.returnDate,
                x.bo.Renew,
                x.st?.Name,
                status,
                statusName,
                x.bo.FineValue,
                x.bo.CircPlace,
                x.staffId,
                x.staffId.HasValue && staffNames.TryGetValue(x.staffId.Value, out var sn) ? sn : null);
        }).ToList();

        return (items, total);
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}

public record LoanHistoryRow(
    long id, Guid publicId, string? cardNo, string? readerName, string? barcode, string? bibTitle, string? callNumber,
    DateTime? borrowDate, DateTime? dueDate, DateTime? returnDate, int? renewCount, string? location,
    int status, string statusName, double? fineValue, long? circPlaceId, long? staffId, string? staffName);
