using System.Globalization;
using ClosedXML.Excel;

namespace Elib.BuildingBlocks.Crud;

public sealed record ExcelFile(string FileName, byte[] Content);

/// <summary>Một sheet báo cáo: tiêu đề, dòng phụ (khoảng ngày…), bảng có viền. Ô DateOnly ghi thành ngày dd/MM/yyyy, số ghi thành số.</summary>
public sealed record ExcelSheet(string Name, string Title, string? Subtitle, IReadOnlyList<string> Headers, IEnumerable<IReadOnlyList<object?>> Rows);

/// <summary>
/// File Excel của các màn báo cáo (monolith: ExcelReportHelper — letterhead tên thư viện, tiêu đề, bảng viền). Mỗi service nhận tên thư
/// viện từ bản sao đơn vị trong DB của chính nó (<see cref="LibraryNameAsync"/>), không gọi service tenant.
/// </summary>
public abstract class ReportExcel(TimeProvider clock)
{
    /// <summary>Trần số dòng mỗi file xuất — như các màn danh sách khác.</summary>
    public const int MaxRows = CrudExcel.MaxExportRows;

    public abstract Task<string?> LibraryNameAsync(CancellationToken ct);

    public async Task<ExcelFile> BuildAsync(string fileStem, CancellationToken ct, params ExcelSheet[] sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        var library = (await LibraryNameAsync(ct) ?? "").ToUpperInvariant();
        using var workbook = new XLWorkbook();
        foreach (var s in sheets) Write(workbook.AddWorksheet(SheetName(s.Name)), library, s);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var stamp = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return new ExcelFile($"{fileStem}-{stamp}.xlsx", stream.ToArray());
    }

    private static void Write(IXLWorksheet sheet, string library, ExcelSheet s)
    {
        var width = Math.Max(s.Headers.Count + 1, 2);
        sheet.Cell(1, 1).Value = library;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Range(3, 1, 3, width).Merge().SetValue(s.Title).Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        if (s.Subtitle is { Length: > 0 } sub)
            sheet.Range(4, 1, 4, width).Merge().SetValue(sub).Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        const int head = 6;
        sheet.Cell(head, 1).Value = "STT";
        for (var c = 0; c < s.Headers.Count; c++) sheet.Cell(head, c + 2).Value = s.Headers[c];
        sheet.Range(head, 1, head, width).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF9"));
        var r = head;
        foreach (var row in s.Rows.Take(MaxRows))
        {
            r++;
            sheet.Cell(r, 1).Value = r - head;
            for (var c = 0; c < row.Count; c++) Value(sheet.Cell(r, c + 2), row[c]);
        }
        var border = sheet.Range(head, 1, r, width).Style.Border;
        border.SetOutsideBorder(XLBorderStyleValues.Thin);
        border.SetInsideBorder(XLBorderStyleValues.Thin);
        sheet.Columns(1, width).AdjustToContents(head, Math.Min(r, head + 500), 6, 60);
        sheet.SheetView.FreezeRows(head);
    }

    private static void Value(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd/MM/yyyy";
                return;
            case DateTimeOffset at:
                cell.Value = at.ToOffset(TimeSpan.FromHours(7)).DateTime;
                cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                return;
            case int or long or decimal or double:
                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return;
            default:
                // Chữ: ĐKCB "000123" giữ số 0 đầu.
                cell.Value = value.ToString();
                cell.Style.NumberFormat.Format = "@";
                return;
        }
    }

    private static string SheetName(string name)
    {
        var clean = new string(name.Where(ch => ch is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':')).ToArray()).Trim();
        return clean.Length is 0 ? "Data" : clean[..Math.Min(31, clean.Length)];
    }

    /// <summary>"Từ ngày … đến ngày …" cho dòng phụ của báo cáo; không lọc ngày → null.</summary>
    public static string? Period(DateOnly? from, DateOnly? to) => (from, to) switch
    {
        (null, null) => null,
        ({ } f, null) => string.Create(CultureInfo.InvariantCulture, $"Từ ngày {f:dd/MM/yyyy}"),
        (null, { } t) => string.Create(CultureInfo.InvariantCulture, $"Đến ngày {t:dd/MM/yyyy}"),
        ({ } f, { } t) => string.Create(CultureInfo.InvariantCulture, $"Từ ngày {f:dd/MM/yyyy} đến ngày {t:dd/MM/yyyy}"),
    };

    public static void EnsureRange(DateOnly? from, DateOnly? to)
    {
        if (from is { } f && to is { } t && f > t)
            throw new Elib.BuildingBlocks.Domain.BusinessRuleException("DATE_RANGE_INVALID", "Từ ngày phải trước đến ngày.");
    }
}
