using System.Text;
using ExcelDataReader;
using ELIBAPI.Core.DTOs.Request;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Đọc file Excel nhập hàng loạt cho "barcode-reregister-import"/"inventory-import" (Đợt 22.5).
/// Khác <c>ReaderImportWorkbook</c>: bố cục cột CỐ ĐỊNH, không có ánh xạ tùy chọn — chỉ 2-3 cột, không đáng
/// làm màn chọn cột như nhập bạn đọc.</summary>
internal static class ImportWorkbookReader
{
    private static bool _providerRegistered;

    public static IExcelDataReader Open(Stream stream)
    {
        if (!_providerRegistered) { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); _providerRegistered = true; }
        return ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });
    }

    /// <summary>Bỏ qua dòng tiêu đề (dòng 1), trả về true nếu còn dữ liệu để đọc.</summary>
    public static void SkipHeader(IExcelDataReader reader)
    {
        if (!reader.Read()) throw new InvalidOperationException("File Excel trống.");
    }

    public static string? Cell(IExcelDataReader reader, int index)
    {
        if (index >= reader.FieldCount) return null;
        var value = reader.GetValue(index);
        return value == null ? null : Convert.ToString(value)?.Trim();
    }

    public static long? CellAsLong(IExcelDataReader reader, int index)
    {
        var text = Cell(reader, index);
        return long.TryParse(text, out var v) ? v : null;
    }
}

/// <summary>Cột: A = Mã vạch cũ, B = Mã vạch mới, C = Mã kho (tuỳ chọn, để trống = giữ nguyên kho).</summary>
public static class BarcodeReRegisterImportWorkbook
{
    public static List<BarcodeReRegisterRow> Read(Stream stream)
    {
        using var reader = ImportWorkbookReader.Open(stream);
        ImportWorkbookReader.SkipHeader(reader);
        var rows = new List<BarcodeReRegisterRow>();
        while (reader.Read())
        {
            var oldBarcode = ImportWorkbookReader.Cell(reader, 0);
            var newBarcode = ImportWorkbookReader.Cell(reader, 1);
            if (string.IsNullOrWhiteSpace(oldBarcode) && string.IsNullOrWhiteSpace(newBarcode)) continue; // dòng trống cuối file
            rows.Add(new BarcodeReRegisterRow
            {
                OldBarcode = oldBarcode,
                NewBarcode = newBarcode,
                StoreId = ImportWorkbookReader.CellAsLong(reader, 2),
            });
        }
        if (rows.Count == 0) throw new InvalidOperationException("Không có dữ liệu để nhập.");
        return rows;
    }
}

/// <summary>Cột: A = Mã vạch, B = Mã kho (tuỳ chọn — dùng để tính "đúng/sai vị trí kho", không bắt buộc).</summary>
public static class InventoryImportWorkbook
{
    public static List<InventoryImportRow> Read(Stream stream)
    {
        using var reader = ImportWorkbookReader.Open(stream);
        ImportWorkbookReader.SkipHeader(reader);
        var rows = new List<InventoryImportRow>();
        while (reader.Read())
        {
            var barcode = ImportWorkbookReader.Cell(reader, 0);
            if (string.IsNullOrWhiteSpace(barcode)) continue; // dòng trống cuối file
            rows.Add(new InventoryImportRow { Barcode = barcode, StoreId = ImportWorkbookReader.CellAsLong(reader, 1) });
        }
        if (rows.Count == 0) throw new InvalidOperationException("Không có dữ liệu để nhập.");
        return rows;
    }
}
