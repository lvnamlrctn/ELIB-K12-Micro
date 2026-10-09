using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace ELIBAPI.API.Helpers;

/// Khối tiêu đề chuẩn (đơn vị chủ quản/thư viện/tên báo cáo) + kẻ bảng, dùng chung cho mọi export Excel.
public static class ExcelReportHelper
{
    /// Ghi dòng 1 = tên đơn vị chủ quản, dòng 2 = tên thư viện, dòng 4 = tên báo cáo (merge, căn giữa).
    /// Trả về số dòng bắt đầu vẽ bảng dữ liệu (header cột).
    public static int WriteLetterhead(IXLWorksheet ws, string? parentLibrary, string? libraryName, string reportTitle, int lastColumn)
    {
        lastColumn = Math.Max(lastColumn, 1);

        ws.Cell(1, 1).Value = parentLibrary ?? "";
        ws.Cell(1, 1).Style.Font.Bold = true;

        ws.Cell(2, 1).Value = libraryName ?? "";
        ws.Cell(2, 1).Style.Font.Bold = true;

        var titleRange = ws.Range(4, 1, 4, lastColumn);
        titleRange.Merge();
        ws.Cell(4, 1).Value = reportTitle;
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 1).Style.Font.FontSize = 14;
        ws.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        return 6;
    }

    /// Đặt font mặc định (Times New Roman, cỡ 13) cho toàn bộ workbook — gọi ngay sau khi tạo XLWorkbook.
    public static void ApplyDefaultFont(XLWorkbook wb)
    {
        wb.Style.Font.FontName = "Times New Roman";
        wb.Style.Font.FontSize = 13;
    }

    public static string StripHtml(string? html) =>
        string.IsNullOrEmpty(html) ? "" : Regex.Replace(html, "<.*?>", "").Trim();

    public static void ApplyTableBorders(IXLWorksheet ws, int firstRow, int lastRow, int firstCol, int lastCol)
    {
        if (lastRow < firstRow || lastCol < firstCol) return;
        var range = ws.Range(firstRow, firstCol, lastRow, lastCol);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }
}
