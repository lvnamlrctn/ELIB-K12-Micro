using System.Globalization;
using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;

namespace Elib.BuildingBlocks.Crud;

public sealed record ExcelFile(string FileName, byte[] Content);

/// <summary>
/// Thông tin ghi ở đầu/cuối báo cáo (mẫu ELIB-LRC ReportSignoff): cơ quan chủ quản, địa danh ("Thái Nguyên, ngày … tháng … năm …"), người lập báo cáo.
/// Cơ quan chủ quản và địa danh do màn báo cáo gửi kèm yêu cầu xuất (header <c>X-Report-Parent</c>, <c>X-Report-Place</c>, mã hoá URL — nhớ theo tài khoản
/// ở trình duyệt); người lập báo cáo luôn lấy từ token (họ tên của người xuất), không tin client.
/// </summary>
public sealed record ReportSignature(string? Parent, string? Place, string? Maker)
{
    public static readonly ReportSignature None = new(null, null, null);

    public static ReportSignature From(HttpContext? context)
    {
        if (context is null) return None;
        var user = context.User;
        var maker = user.Identity?.IsAuthenticated == true
            ? user.FindFirstValue("name") is { Length: > 0 } name ? name : user.FindFirstValue("preferred_username")
            : null;
        return new ReportSignature(Header(context, "X-Report-Parent", 250), Header(context, "X-Report-Place", 100), maker?.Trim());
    }

    private static string? Header(HttpContext context, string name, int max)
    {
        if (!context.Request.Headers.TryGetValue(name, out var values)) return null;
        var raw = values.ToString();
        try { raw = Uri.UnescapeDataString(raw); }
        catch (UriFormatException) { /* header không mã hoá URL — dùng nguyên văn */ }
        raw = raw.Trim();
        return raw.Length == 0 ? null : raw[..Math.Min(max, raw.Length)];
    }
}

/// <summary>Đầu và cuối sheet báo cáo theo mẫu giấy: cơ quan chủ quản + tên thư viện; "Địa danh, ngày…", người lập báo cáo / thủ trưởng đơn vị.</summary>
public static class ReportSheet
{
    /// <summary>Ô A1 (và A2 khi có cơ quan chủ quản): chủ quản trên, tên thư viện dưới — tiêu đề bắt đầu ở dòng 3 như cũ.</summary>
    public static void WriteLetterhead(IXLWorksheet sheet, string library, ReportSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        var row = 1;
        if (signature.Parent is { Length: > 0 } parent)
        {
            sheet.Cell(row, 1).Value = parent.ToUpperInvariant();
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;
        }
        sheet.Cell(row, 1).Value = library;
        sheet.Cell(row, 1).Style.Font.Bold = true;
    }

    /// <summary>
    /// Chân báo cáo dưới dòng <paramref name="lastRow"/>: dòng ngày căn phải, hai khối chữ ký (người lập báo cáo kèm họ tên / thủ trưởng đơn vị).
    /// Trả về dòng cuối cùng đã ghi.
    /// </summary>
    public static int WriteFooter(IXLWorksheet sheet, int lastRow, int width, ReportSignature signature, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(signature);
        width = Math.Max(width, 2);
        var mid = width / 2;
        var local = now.ToOffset(TimeSpan.FromHours(7));
        var date = string.Create(CultureInfo.InvariantCulture, $"ngày {local:dd} tháng {local:MM} năm {local:yyyy}");
        var line = signature.Place is { Length: > 0 } place ? $"{place}, {date}" : $"N{date[1..]}";

        var row = lastRow + 2;
        sheet.Range(row, 1, row, width).Merge().SetValue(line).Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        row += 2;
        Block(sheet, row, 1, mid, "NGƯỜI LẬP BÁO CÁO");
        Block(sheet, row, mid + 1, width, "THỦ TRƯỞNG ĐƠN VỊ");
        row++;
        sheet.Range(row, 1, row, mid).Merge().SetValue("(Ký, ghi rõ họ tên)").Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        sheet.Range(row, mid + 1, row, width).Merge().SetValue("(Ký, ghi rõ họ tên)").Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        row += 4; // chỗ ký
        if (signature.Maker is { Length: > 0 } maker)
            sheet.Range(row, 1, row, mid).Merge().SetValue(maker).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        return row;
    }

    private static void Block(IXLWorksheet sheet, int row, int from, int to, string text) =>
        sheet.Range(row, from, row, to).Merge().SetValue(text).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
}

/// <summary>Một sheet báo cáo: tiêu đề, dòng phụ (khoảng ngày…), bảng có viền. Ô DateOnly ghi thành ngày dd/MM/yyyy, số ghi thành số.</summary>
public sealed record ExcelSheet(string Name, string Title, string? Subtitle, IReadOnlyList<string> Headers, IEnumerable<IReadOnlyList<object?>> Rows);

/// <summary>
/// File Excel của các màn báo cáo (monolith: ExcelReportHelper — letterhead tên thư viện, tiêu đề, bảng viền). Mỗi service nhận tên thư
/// viện từ bản sao đơn vị trong DB của chính nó (<see cref="LibraryNameAsync"/>), không gọi service tenant.
/// </summary>
public abstract class ReportExcel(TimeProvider clock, IHttpContextAccessor? http = null)
{
    /// <summary>Trần số dòng mỗi file xuất — như các màn danh sách khác.</summary>
    public const int MaxRows = CrudExcel.MaxExportRows;

    public abstract Task<string?> LibraryNameAsync(CancellationToken ct);

    public async Task<ExcelFile> BuildAsync(string fileStem, CancellationToken ct, params ExcelSheet[] sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        var library = (await LibraryNameAsync(ct) ?? "").ToUpperInvariant();
        var signature = ReportSignature.From(http?.HttpContext);
        var now = clock.GetUtcNow();
        using var workbook = new XLWorkbook();
        foreach (var s in sheets) Write(workbook.AddWorksheet(SheetName(s.Name)), library, signature, now, s);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var stamp = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return new ExcelFile($"{fileStem}-{stamp}.xlsx", stream.ToArray());
    }

    private static void Write(IXLWorksheet sheet, string library, ReportSignature signature, DateTimeOffset now, ExcelSheet s)
    {
        var width = Math.Max(s.Headers.Count + 1, 2);
        ReportSheet.WriteLetterhead(sheet, library, signature);
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
        ReportSheet.WriteFooter(sheet, r, width, signature, now);
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
