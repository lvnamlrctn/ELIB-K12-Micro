using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Domain;

namespace Elib.BuildingBlocks.Crud;

/// <summary>
/// Một cột của file import. Tiêu đề cột trong file được so khớp không phân biệt hoa thường/dấu với <see cref="Header"/>,
/// <see cref="Key"/> và <see cref="Aliases"/> — file mẫu cũ của monolith (cột "Name") vẫn dùng được.
/// </summary>
public sealed record CrudImportColumn(string Key, string Header, bool Required = false, string? Note = null, params string[] Aliases);

/// <summary>Một dòng dữ liệu đã đọc: giá trị theo <see cref="CrudImportColumn.Key"/> (đã trim, rỗng → null).</summary>
public sealed class CrudImportRow(int number, IReadOnlyDictionary<string, string?> values)
{
    /// <summary>Số dòng trong file Excel (dòng tiêu đề là 1) — để báo lỗi đúng dòng người dùng thấy.</summary>
    public int Number { get; } = number;

    public string? Get(string key) => values.GetValueOrDefault(key);

    public string Required(string key, string header) =>
        Get(key) ?? throw new BusinessRuleException("IMPORT_VALUE_REQUIRED", $"Thiếu giá trị cột \"{header}\".");

    public int? WholeNumber(string key, string header) => Get(key) is not { } text
        ? null
        : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new BusinessRuleException("IMPORT_VALUE_INVALID", $"Cột \"{header}\" phải là số nguyên (đang là \"{text}\").");

    /// <summary>Ô kiểu ngày của Excel (đọc thành yyyy-MM-dd) hoặc chữ dạng dd/MM/yyyy, d/M/yyyy, yyyy-MM-dd như người dùng gõ.</summary>
    public DateOnly? Date(string key, string header) => Get(key) is not { } text
        ? null
        : DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new BusinessRuleException("IMPORT_VALUE_INVALID", $"Cột \"{header}\" phải là ngày dạng dd/MM/yyyy (đang là \"{text}\").");

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy"];
}

/// <summary>Danh mục cho phép nhập từ Excel (thay action Import của từng controller monolith). Resource cài interface này thì có thêm 2 endpoint.</summary>
public interface ICrudImportable<out TRequest>
{
    IReadOnlyList<CrudImportColumn> ImportColumns { get; }

    /// <summary>Dòng Excel → request Thêm mới; lỗi dữ liệu ném <see cref="BusinessRuleException"/>.</summary>
    TRequest MapImportRow(CrudImportRow row);
}

public sealed record CrudImportError(int Row, string Message);

/// <summary>
/// Kết quả import. Có lỗi thì <see cref="Imported"/> = 0 — không ghi dòng nào (sửa file rồi nhập lại cả file, không lo nhập trùng nửa chừng).
/// </summary>
public sealed record CrudImportResult(int Imported, int Skipped, IReadOnlyList<CrudImportError> Errors)
{
    /// <summary>Thông báo tóm tắt (trường "detail" như problem+json để frontend hiện thẳng).</summary>
    public string Detail => Errors.Count > 0
        ? $"File có {Errors.Count} dòng lỗi, chưa nhập dòng nào. Sửa các dòng lỗi rồi nhập lại."
        : $"Đã nhập {Imported} bản ghi" + (Skipped > 0 ? $", bỏ qua {Skipped} dòng đã có." : ".");
}

/// <summary>Đọc/ghi file .xlsx cho import — không phụ thuộc DB, test được riêng.</summary>
public static partial class CrudExcel
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Giới hạn để một file lỗi/cố ý phá không làm nghẽn service.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    public const int MaxRows = 5000;

    /// <summary>File mẫu: một dòng tiêu đề (cột bắt buộc in đậm, có dấu *), ghi chú cột ở comment của ô tiêu đề.</summary>
    public static byte[] Template(string sheetName, IReadOnlyList<CrudImportColumn> columns)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName(sheetName));
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var cell = sheet.Cell(1, i + 1);
            cell.Value = column.Header + (column.Required ? " *" : "");
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF9");
            if (column.Note is { Length: > 0 } note) cell.CreateComment().AddText(note);
            sheet.Column(i + 1).Width = Math.Max(18, column.Header.Length + 6);
        }
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Đọc sheet đầu tiên: dòng 1 là tiêu đề, bỏ dòng trống. Lỗi định dạng file ném <see cref="BusinessRuleException"/>.</summary>
    public static IReadOnlyList<CrudImportRow> Read(Stream stream, IReadOnlyList<CrudImportColumn> columns)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new BusinessRuleException("IMPORT_FILE_INVALID", "Không đọc được file. Chỉ nhận file Excel .xlsx (tải file mẫu để dùng đúng định dạng).");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new BusinessRuleException("IMPORT_FILE_EMPTY", "File không có sheet nào.");
            var used = sheet.RangeUsed();
            if (used is null) throw new BusinessRuleException("IMPORT_FILE_EMPTY", "File không có dữ liệu.");

            var lastColumn = used.LastColumn().ColumnNumber();
            var positions = new Dictionary<string, int>();
            for (var c = 1; c <= lastColumn; c++)
            {
                var header = Normalize(sheet.Cell(1, c).GetString());
                var match = columns.FirstOrDefault(col => Names(col).Contains(header));
                if (match is not null) positions.TryAdd(match.Key, c);
            }
            var missing = columns.Where(c => c.Required && !positions.ContainsKey(c.Key)).Select(c => $"\"{c.Header}\"").ToList();
            if (missing.Count > 0)
                throw new BusinessRuleException("IMPORT_COLUMN_MISSING", $"Thiếu cột {string.Join(", ", missing)} ở dòng tiêu đề (dòng 1).");

            var lastRow = used.LastRow().RowNumber();
            if (lastRow - 1 > MaxRows)
                throw new BusinessRuleException("IMPORT_TOO_MANY_ROWS", $"Mỗi lần nhập tối đa {MaxRows} dòng.");

            var rows = new List<CrudImportRow>();
            for (var r = 2; r <= lastRow; r++)
            {
                var values = positions.ToDictionary(p => p.Key, p => Clean(CellText(sheet.Cell(r, p.Value))));
                if (values.Values.All(v => v is null)) continue;
                rows.Add(new CrudImportRow(r, values));
            }
            if (rows.Count == 0) throw new BusinessRuleException("IMPORT_NO_DATA", "File không có dòng dữ liệu nào (dòng 1 là tiêu đề).");
            return rows;
        }
    }

    private static HashSet<string> Names(CrudImportColumn column) =>
        [.. new[] { column.Header, column.Key }.Concat(column.Aliases).Select(Normalize)];

    /// <summary>Ô ngày → yyyy-MM-dd (không phụ thuộc định dạng hiển thị/locale của máy); ô khác → chữ như Excel hiển thị.</summary>
    private static string CellText(IXLCell cell) =>
        cell.DataType == XLDataType.DateTime
            ? cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : cell.GetFormattedString();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"Tên *" → "ten"; "Họ và tên" → "ho va ten" — so khớp tiêu đề không phân biệt hoa thường/dấu.</summary>
    private static string Normalize(string? text)
    {
        var decomposed = (text ?? "").Replace("*", "", StringComparison.Ordinal).Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch));
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string SheetName(string name)
    {
        var clean = new string(name.Where(ch => ch is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':')).ToArray()).Trim();
        return clean.Length is 0 ? "Data" : clean[..Math.Min(31, clean.Length)];
    }
}
