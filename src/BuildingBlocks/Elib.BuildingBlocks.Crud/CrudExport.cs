using ClosedXML.Excel;
using Elib.BuildingBlocks.Domain;

namespace Elib.BuildingBlocks.Crud;

/// <summary>
/// Một cột của file xuất. <see cref="Value"/> trả chuỗi, số hoặc <see cref="DateOnly"/> (ghi thành ô ngày dd/MM/yyyy).
/// Đặt <see cref="Header"/> trùng tiêu đề cột import thì file xuất nhập lại được.
/// </summary>
public sealed record CrudExportColumn<TRow>(string Key, string Header, Func<TRow, object?> Value);

/// <summary>Trường cho người dùng chọn khi xuất (monolith: GetExportFields).</summary>
public sealed record CrudExportField(string Code, string Name);

public static partial class CrudExcel
{
    /// <summary>Trần số dòng mỗi lần xuất — danh sách lớn hơn thì lọc bớt (theo lớp, khoá…).</summary>
    public const int MaxExportRows = 20000;

    /// <summary>Cột được chọn theo thứ tự khai báo; <paramref name="keys"/> rỗng = mọi cột. Mã lạ → lỗi (không âm thầm bỏ).</summary>
    public static IReadOnlyList<CrudExportColumn<TRow>> Select<TRow>(IReadOnlyList<CrudExportColumn<TRow>> columns, IReadOnlyCollection<string>? keys)
    {
        if (keys is null || keys.Count == 0) return columns;
        var unknown = keys.Where(k => !columns.Any(c => string.Equals(c.Key, k, StringComparison.OrdinalIgnoreCase))).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException("EXPORT_FIELD_INVALID", $"Trường xuất không hợp lệ: {string.Join(", ", unknown)}.");
        return [.. columns.Where(c => keys.Contains(c.Key, StringComparer.OrdinalIgnoreCase))];
    }

    /// <summary>File xuất: dòng 1 là tiêu đề (STT + các cột), dữ liệu từ dòng 2 — cùng bố cục với file import.</summary>
    public static byte[] Export<TRow>(string sheetName, IReadOnlyList<CrudExportColumn<TRow>> columns, IEnumerable<TRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName(sheetName));
        sheet.Cell(1, 1).Value = "STT";
        for (var c = 0; c < columns.Count; c++) sheet.Cell(1, c + 2).Value = columns[c].Header;
        var header = sheet.Range(1, 1, 1, columns.Count + 1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF9");

        var r = 1;
        foreach (var row in rows)
        {
            r++;
            sheet.Cell(r, 1).Value = r - 1;
            for (var c = 0; c < columns.Count; c++) Write(sheet.Cell(r, c + 2), columns[c].Value(row));
        }
        sheet.Columns(1, columns.Count + 1).AdjustToContents(1, Math.Min(r, 500), 6, 60);
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Write(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd/MM/yyyy";
                return;
            case int or long or decimal or double:
                cell.Value = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                return;
            default:
                // Chuỗi ghi dạng text: số thẻ "000123" giữ nguyên số 0 đầu.
                cell.Value = value.ToString();
                cell.Style.NumberFormat.Format = "@";
                return;
        }
    }
}
