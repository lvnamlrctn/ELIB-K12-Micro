using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ExcelDataReader;
using ELIBAPI.Core.DTOs.Request;

namespace ELIBAPI.Infrastructure.Services;
public sealed class ReaderImportMapping
{
    public int HeaderRow { get; set; } = 1;
    public Dictionary<string, int?>? Columns { get; set; }
}
/// <summary>
/// Đọc file Excel nhập bạn đọc theo ánh xạ cột do người dùng chọn (port ELIB-LRC 09-15/09-21, Đợt 20).
/// Khác LRC: ở ELIB <c>LastName</c> = Họ, <c>FirstName</c> = Tên (LRC ngược lại) — nhãn và cách tách "Họ và Tên"
/// một cột đã đảo cho đúng. Không có ánh xạ → đọc theo bố cục cột cố định cũ của ELIB (A=STT, B=Số thẻ, C=Họ,
/// D=Tên … P=Số căn cước, Q=UID thẻ chip), nên file mẫu cũ vẫn dùng được.
/// </summary>
public static class ReaderImportWorkbook
{
    public static readonly Dictionary<string, string> Fields = new() {
        ["Cardno"] = "Số thẻ", ["LastName"] = "Họ", ["FirstName"] = "Tên", ["SexText"] = "Giới tính", ["BirthDateText"] = "Ngày sinh",
        ["Email"] = "Email", ["Password"] = "Mật khẩu", ["Phone"] = "Điện thoại", ["Address"] = "Địa chỉ", ["IssueDateText"] = "Ngày cấp",
        ["ExpireDateText"] = "Ngày hết hạn", ["ClassName"] = "Lớp", ["CourseName"] = "Khóa học", ["OrgName"] = "Đơn vị", ["CitizenId"] = "Số căn cước", ["CardUid"] = "UID thẻ" };
    private static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        .Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant().Replace(" ", "").Replace("_", "");
    private static IExcelDataReader Open(Stream stream) { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { LeaveOpen = true }); }
    private static void Header(IExcelDataReader reader, int row) {
        if (row < 1 || row > 20) throw new InvalidOperationException("Dòng tiêu đề phải nằm trong 1–20.");
        for (var i = 0; i < row; i++) if (!reader.Read()) throw new InvalidOperationException("Không tìm thấy dòng tiêu đề.");
        if (reader.FieldCount > 100) throw new InvalidOperationException("Tệp có quá 100 cột.");
    }
    public static object Inspect(Stream stream, int headerRow)
    {
        using var reader = Open(stream); Header(reader, headerRow);
        var headers = Enumerable.Range(0, reader.FieldCount).Select(i => new { index = i, name = Convert.ToString(reader.GetValue(i)) ?? $"Cột {i + 1}" }).ToList();
        var suggested = Fields.ToDictionary(f => f.Key, f => {
            var matches = headers.Where(h => Normalize(h.name) == Normalize(f.Key) || Normalize(h.name) == Normalize(f.Value)).ToList();
            return matches.Count == 1 ? (int?)matches[0].index : null;
        });
        var fullNameMatches = headers.Where(h => Normalize(h.name) == Normalize("Họ và Tên") || Normalize(h.name) == Normalize("Họ tên") || Normalize(h.name) == Normalize("FullName")).ToList();
        suggested["FullName"] = fullNameMatches.Count == 1 ? (int?)fullNameMatches[0].index : null;
        var fields = Fields.Select(f => new { key = f.Key, label = f.Value }).Append(new { key = "FullName", label = "Họ và Tên (một cột, tự tách Họ/Tên)" });
        return new { headers, fields, suggested, headerRow };
    }
    public static List<ReaderImportRow> Read(Stream stream, ReaderImportMapping? mapping)
    {
        mapping ??= new(); using var reader = Open(stream); Header(reader, mapping.HeaderRow);
        var columns = mapping.Columns != null ? new Dictionary<string, int?>(mapping.Columns)
            : Fields.Keys.Select((key, index) => (key, index)).ToDictionary(x => x.key, x => (int?)(x.index + 1));
        int? fullNameColumn = columns.Remove("FullName", out var fnCol) ? fnCol : null;
        if (mapping.Columns != null && (!columns.TryGetValue("Cardno", out var cardColumn) || cardColumn == null)) throw new InvalidOperationException("Cần ánh xạ cột Số thẻ.");
        if (columns.Keys.Any(k => !Fields.ContainsKey(k)) || columns.Values.Any(i => i < 0 || (mapping.Columns != null && i >= reader.FieldCount))
            || (fullNameColumn.HasValue && (fullNameColumn < 0 || (mapping.Columns != null && fullNameColumn >= reader.FieldCount)))) throw new InvalidOperationException("Ánh xạ chứa cột không hợp lệ.");
        var mapped = columns.Values.Where(x => x != null).ToList();
        if (fullNameColumn.HasValue) mapped.Add(fullNameColumn);
        if (mapped.Count != mapped.Distinct().Count()) throw new InvalidOperationException("Mỗi cột chỉ được ánh xạ vào một trường.");
        var rows = new List<ReaderImportRow>(); var number = mapping.HeaderRow;
        while (reader.Read()) {
            number++; var row = new ReaderImportRow { RowNumber = number };
            foreach (var field in Fields.Keys) {
                if (!columns.TryGetValue(field, out var index) || index == null || index >= reader.FieldCount) continue;
                var value = reader.GetValue(index.Value); if (value == null || value == DBNull.Value) continue;
                string? text;
                if (field.EndsWith("DateText")) {
                    if (value is DateTime date) text = date.ToString("yyyy-MM-dd");
                    else if (value is double serial && serial > 0 && serial < 2958466) text = DateTime.FromOADate(serial).ToString("yyyy-MM-dd");
                    else { text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
                        if (DateTime.TryParseExact(text, ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "yyyyMMdd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) text = parsed.ToString("yyyy-MM-dd");
                    }
                } else {
                    text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
                    var format = reader.GetNumberFormatString(index.Value);
                    if (value is double numeric && format != null && Regex.IsMatch(format, "^0{2,}$") && format.Length <= 30) text = numeric.ToString(format, CultureInfo.InvariantCulture);
                }
                typeof(ReaderImportRow).GetProperty(field)!.SetValue(row, text);
            }
            if (fullNameColumn.HasValue && fullNameColumn.Value < reader.FieldCount) {
                var value = reader.GetValue(fullNameColumn.Value);
                var text = value == null || value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
                if (!string.IsNullOrWhiteSpace(text)) {
                    var parts = Regex.Split(text, @"\s+");
                    // "Lê Văn Nam" → Họ (LastName) "Lê Văn", Tên (FirstName) "Nam".
                    row.FirstName = parts[^1];
                    row.LastName = parts.Length > 1 ? string.Join(' ', parts[..^1]) : "";
                }
            }
            if (Fields.Keys.All(f => string.IsNullOrWhiteSpace((string?)typeof(ReaderImportRow).GetProperty(f)!.GetValue(row)))) continue;
            rows.Add(row); if (rows.Count > 50000) throw new InvalidOperationException("Tệp vượt quá 50.000 dòng dữ liệu.");
        }
        return rows;
    }
    public static byte[] Errors(List<ReaderImportRow> rows, IEnumerable<string> errors)
    {
        var issues = errors.Select(error => (Match: Regex.Match(error, @"^Dòng (\d+):\s*(.*)$"), Error: error)).Where(x => x.Match.Success)
            .GroupBy(x => int.Parse(x.Match.Groups[1].Value)).ToDictionary(g => g.Key, g => string.Join("; ", g.Select(x => x.Match.Groups[2].Value).Distinct()));
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Dòng cần sửa");
        var headers = new[] { "STT" }.Concat(Fields.Values).Concat(["Dòng gốc", "Nguyên nhân", "Gợi ý sửa"]).ToArray();
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        var outputRow = 2;
        for (var i = 0; i < rows.Count; i++) {
            var row = rows[i]; var sourceRow = row.RowNumber > 0 ? row.RowNumber : i + 2;
            if (!issues.TryGetValue(sourceRow, out var reason)) continue;
            sheet.Cell(outputRow, 1).Value = outputRow - 1;
            var column = 2;
            foreach (var field in Fields.Keys) { sheet.Cell(outputRow, column++).Value = field == "Password" ? "" : (string?)typeof(ReaderImportRow).GetProperty(field)!.GetValue(row) ?? ""; }
            sheet.Cell(outputRow, column++).Value = sourceRow; sheet.Cell(outputRow, column++).Value = reason;
            sheet.Cell(outputRow, column).Value = reason.Contains("trùng") ? "Kiểm tra số thẻ và bỏ dòng trùng." : reason.Contains("Ngày") ? "Dùng ngày dd/MM/yyyy hoặc yyyy-MM-dd." : reason.Contains("Email") ? "Sửa địa chỉ email." : "Đối chiếu số thẻ và mã/tên danh mục đang dùng.";
            outputRow++;
        }
        sheet.Row(1).Style.Font.Bold = true; sheet.SheetView.FreezeRows(1); sheet.Columns().Width = 22;
        var info = workbook.AddWorksheet("Hướng dẫn"); info.Cell(1, 1).Value = "Sửa các dòng ở trang đầu rồi nhập lại. Giữ Số thẻ và chọn đúng loại bạn đọc. Cột Mật khẩu được để trống; điền lại nếu nghiệp vụ yêu cầu. Các cột Dòng gốc/Nguyên nhân/Gợi ý sửa không cần ánh xạ.";
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }
}
