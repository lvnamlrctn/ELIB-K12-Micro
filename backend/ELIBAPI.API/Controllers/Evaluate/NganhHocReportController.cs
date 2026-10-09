using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Evaluate;

[Route("api/Evaluate/NganhHocReport")]
public class NganhHocReportController(ELIBAPIDbContext db, ISystemParameterService sysParam) : BaseApiController
{
    [HttpPost("Summary")]
    [Permission("NGANHHOC", "view")]
    public async Task<IActionResult> Summary([FromBody] NganhHocReportRequest r)
    {
        var courses = await BuildReport(r.MajorId);
        var stats = BuildStats(courses);
        return Ok(ApiResponse<object>.Ok(new { stats, courses }));
    }

    [HttpPost("ExportByCourse")]
    [Permission("NGANHHOC", "view")]
    public async Task<IActionResult> ExportByCourse([FromBody] NganhHocReportRequest r)
    {
        var courses = await BuildReport(r.MajorId);
        var majorName = await GetMajorName(r.MajorId);

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Tài liệu theo ngành học");
        var lastColumn = DocumentColumns.Length;
        var startRow = await WriteLetterheadWithSubtitle(ws, "DANH SÁCH TÀI LIỆU THEO NGÀNH HỌC", majorName, lastColumn);

        var lastRow = WriteDocumentRows(ws, courses, startRow, filter: null);
        ExcelReportHelper.ApplyTableBorders(ws, startRow, lastRow, 1, lastColumn);
        ws.Columns().AdjustToContents();

        return SaveWorkbook(wb, "danh-sach-tai-lieu-theo-nganh-hoc.xlsx");
    }

    [HttpPost("ExportAvailable")]
    [Permission("NGANHHOC", "view")]
    public async Task<IActionResult> ExportAvailable([FromBody] NganhHocReportRequest r)
        => await ExportByLinkStatus(r.MajorId, hasLink: true, "danh-sach-tai-lieu-san-co.xlsx", "DANH SÁCH TÀI LIỆU SẴN CÓ TẠI THƯ VIỆN");

    [HttpPost("ExportNotAvailable")]
    [Permission("NGANHHOC", "view")]
    public async Task<IActionResult> ExportNotAvailable([FromBody] NganhHocReportRequest r)
        => await ExportByLinkStatus(r.MajorId, hasLink: false, "danh-sach-tai-lieu-chua-san-co.xlsx", "DANH SÁCH TÀI LIỆU CHƯA SẴN CÓ TẠI THƯ VIỆN");

    private async Task<IActionResult> ExportByLinkStatus(long majorId, bool hasLink, string fileName, string title)
    {
        var courses = await BuildReport(majorId);
        var majorName = await GetMajorName(majorId);

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Tài liệu");
        var lastColumn = DocumentColumns.Length;
        var startRow = await WriteLetterheadWithSubtitle(ws, title, majorName, lastColumn);

        var lastRow = WriteDocumentRows(ws, courses, startRow, filter: d => d.HasLink == hasLink);
        ExcelReportHelper.ApplyTableBorders(ws, startRow, lastRow, 1, lastColumn);
        ws.Columns().AdjustToContents();

        return SaveWorkbook(wb, fileName);
    }

    [HttpPost("ExportCourseList")]
    [Permission("NGANHHOC", "view")]
    public async Task<IActionResult> ExportCourseList([FromBody] NganhHocReportRequest r)
    {
        var courses = await BuildReport(r.MajorId);
        var majorName = await GetMajorName(r.MajorId);

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Danh sách môn học");
        var lastColumn = 4;
        var startRow = await WriteLetterheadWithSubtitle(ws, "DANH SÁCH MÔN HỌC THEO NGÀNH HỌC", majorName, lastColumn);

        ws.Cell(startRow, 1).Value = "STT";
        ws.Cell(startRow, 2).Value = "Mã môn";
        ws.Cell(startRow, 3).Value = "Tên Môn";
        ws.Cell(startRow, 4).Value = "Số tài liệu";

        var row = startRow + 1;
        var stt = 1;
        foreach (var c in courses)
        {
            ws.Cell(row, 1).Value = stt++;
            ws.Cell(row, 2).Value = c.MaMon;
            ws.Cell(row, 3).Value = c.TenMon;
            ws.Cell(row, 4).Value = c.Documents.Count;
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, lastColumn);
        ws.Columns().AdjustToContents();

        return SaveWorkbook(wb, "danh-sach-mon-hoc-theo-nganh-hoc.xlsx");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly string[] DocumentColumns =
        ["STT", "Mã môn", "Tên Môn", "Nhan đề", "Tác giả", "Nhà xuất bản", "Năm xuất bản", "Loại tài liệu", "Hiện trạng", "Tài liệu in", "Tài liệu số"];

    /// Ghi bảng tài liệu (11 cột, nhóm theo môn — STT/Mã môn/Tên Môn chỉ ghi ở dòng đầu mỗi môn), có thể lọc theo tài liệu. Trả về dòng cuối cùng đã ghi.
    private static int WriteDocumentRows(IXLWorksheet ws, List<CourseReportRow> courses, int startRow, Func<DocumentReportRow, bool>? filter)
    {
        for (var col = 0; col < DocumentColumns.Length; col++)
            ws.Cell(startRow, col + 1).Value = DocumentColumns[col];

        var row = startRow + 1;
        var stt = 1;
        foreach (var c in courses)
        {
            var docs = filter == null ? c.Documents : c.Documents.Where(filter).ToList();
            if (docs.Count == 0) continue;

            ws.Cell(row, 1).Value = stt++;
            ws.Cell(row, 2).Value = c.MaMon;
            ws.Cell(row, 3).Value = c.TenMon;
            row++;

            foreach (var d in docs)
            {
                ws.Cell(row, 4).Value = d.Title;
                ws.Cell(row, 5).Value = d.Author;
                ws.Cell(row, 6).Value = d.Publisher;
                ws.Cell(row, 7).Value = d.PublishDate;
                ws.Cell(row, 8).Value = d.LoaiTaiLieu == 2 ? "Tài liệu tham khảo" : "Tài liệu chính";
                ws.Cell(row, 9).Value = d.HasLink ? "Hiện có" : "Chưa có";
                ws.Cell(row, 10).Value = d.PrintCopyCount;
                ws.Cell(row, 11).Value = d.HasDigital ? "Có" : "";
                row++;
            }
        }
        return row - 1;
    }

    private async Task<string> GetMajorName(long majorId)
        => await db.NganhHocs.Where(x => x.Id == majorId).Select(x => x.MajorsName).FirstOrDefaultAsync() ?? "";

    private async Task<int> WriteLetterheadWithSubtitle(IXLWorksheet ws, string title, string majorName, int lastColumn)
    {
        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, title, lastColumn);

        var subtitleRange = ws.Range(5, 1, 5, lastColumn);
        subtitleRange.Merge();
        ws.Cell(5, 1).Value = $"Ngành học : {majorName}";
        ws.Cell(5, 1).Style.Font.Bold = true;
        ws.Cell(5, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        return startRow;
    }

    private static IActionResult SaveWorkbook(XLWorkbook wb, string fileName)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new FileContentResult(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") { FileDownloadName = fileName };
    }

    private static object BuildStats(List<CourseReportRow> courses)
    {
        var docs = courses.SelectMany(c => c.Documents).ToList();
        static object Bucket(List<DocumentReportRow> d) => new { total = d.Count, available = d.Count(x => x.HasLink) };
        return new
        {
            main      = Bucket(docs.Where(d => d.LoaiTaiLieu != 2).ToList()),
            reference = Bucket(docs.Where(d => d.LoaiTaiLieu == 2).ToList()),
            overall   = Bucket(docs)
        };
    }

    private async Task<List<CourseReportRow>> BuildReport(long majorId)
    {
        var tenantId = GetTenantId();

        var linkQuery = db.NganhMonHocs.Where(x => x.IsDelete != 2 && x.MajorId == majorId);
        if (tenantId.HasValue) linkQuery = linkQuery.Where(x => x.TenantId == tenantId);
        var monHocIds = await linkQuery.Select(x => x.MonHocId!.Value).Distinct().ToListAsync();

        var courses = await db.MonHocs
            .Where(x => x.IsDelete != 2 && monHocIds.Contains(x.Id))
            .OrderBy(x => x.MaMon)
            .Select(x => new { x.Id, x.MaMon, x.TenMon })
            .ToListAsync();

        var docQuery = db.TaiLieus.Where(x => x.IsDelete != 2 && x.MonHocId != null && monHocIds.Contains(x.MonHocId!.Value));
        if (tenantId.HasValue) docQuery = docQuery.Where(x => x.TenantId == tenantId);
        var documents = await docQuery
            .Select(x => new { x.MonHocId, x.Title, x.Author, x.Publisher, x.PublishDate, x.LoaiTaiLieu, x.BibId, x.EBookId })
            .ToListAsync();

        var bibIds = documents.Where(d => d.BibId.HasValue).Select(d => d.BibId!.Value).Distinct().ToList();
        var copyCounts = bibIds.Count == 0
            ? new Dictionary<long, int>()
            : await db.Barcodes.Where(b => b.IsDelete != 2 && b.BibId.HasValue && bibIds.Contains(b.BibId.Value))
                .GroupBy(b => b.BibId!.Value)
                .Select(g => new { BibId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BibId, x => x.Count);

        return courses.Select(c => new CourseReportRow
        {
            MaMon = c.MaMon ?? "",
            TenMon = c.TenMon ?? "",
            Documents = documents.Where(d => d.MonHocId == c.Id).Select(d => new DocumentReportRow
            {
                Title = d.Title ?? "",
                Author = d.Author ?? "",
                Publisher = d.Publisher ?? "",
                PublishDate = d.PublishDate ?? "",
                LoaiTaiLieu = d.LoaiTaiLieu ?? 1,
                HasLink = d.BibId.HasValue || d.EBookId.HasValue,
                HasDigital = d.EBookId.HasValue,
                PrintCopyCount = d.BibId.HasValue && copyCounts.TryGetValue(d.BibId.Value, out var cnt) ? cnt : 0
            }).ToList()
        }).ToList();
    }
}

public class NganhHocReportRequest
{
    public long MajorId { get; set; }
}

public class CourseReportRow
{
    public string MaMon { get; set; } = "";
    public string TenMon { get; set; } = "";
    public List<DocumentReportRow> Documents { get; set; } = [];
}

public class DocumentReportRow
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string PublishDate { get; set; } = "";
    public int LoaiTaiLieu { get; set; }
    public bool HasLink { get; set; }
    public bool HasDigital { get; set; }
    public int PrintCopyCount { get; set; }
}
