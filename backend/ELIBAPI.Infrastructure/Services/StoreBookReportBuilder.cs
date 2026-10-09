using ClosedXML.Excel;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

// Logic dựng dữ liệu báo cáo sách trong kho — tách ra khỏi StoreBookReportController để dùng chung
// được từ cả action HTTP (Search/Export) lẫn ScheduledReportEmailJob. Chỉ di chuyển cơ học + đổi
// tenantId từ biến đọc claim trong controller thành tham số truyền vào.
public class StoreBookReportBuilder(ELIBAPIDbContext db)
{
    private const int RowCap = 5000;
    public static readonly string[] Headers = ["STT", "Đăng ký cá biệt", "Nhan đề", "Tác giả", "Phân loại", "Ngày nhận", "Kho"];

    public record StoreStatRow(string StoreName, int TitleCount, int CopyCount);

    /// Xuất file Excel đơn giản (không letterhead, không nhóm theo kho — dùng cho đính kèm email báo
    /// cáo định kỳ). Action HTTP Export vẫn tự dựng workbook riêng có letterhead+nhóm, không đổi.
    public async Task<byte[]> BuildExcelBytesAsync(StoreBookReportRequest r, long? tenantId)
    {
        var (rows, _, _, _) = await BuildRowsAsync(r, tenantId);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sách trong kho");
        for (var i = 0; i < Headers.Length; i++) ws.Cell(1, i + 1).Value = Headers[i];
        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;
        var row = 2;
        foreach (var values in rows)
        {
            for (var i = 0; i < values.Length; i++) ws.Cell(row, i + 1).Value = values[i];
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<string?> GetStoreNameAsync(int? storeId) =>
        storeId.HasValue ? await db.Stores.Where(s => s.Id == storeId).Select(s => s.Name).FirstOrDefaultAsync() : null;

    public async Task<(List<string[]> Rows, int TotalCount, int TitleCount, List<StoreStatRow> StoreStats)> BuildRowsAsync(StoreBookReportRequest r, long? tenantId)
    {
        var query =
            from bc in db.Barcodes
            where bc.IsDelete != 2 && bc.Status != "L" && bc.Status != "S"
            join ar in db.AbReceipts on bc.Receipt_Id equals ar.Id into arj
            from ar in arj.DefaultIfEmpty()
            join bx in db.BibXmls on bc.BibId equals bx.BibId into bxj
            from bx in bxj.DefaultIfEmpty()
            join st in db.Stores on (long?)bc.Store equals (long?)st.Id into stj
            from st in stj.DefaultIfEmpty()
            select new { bc, ar, bx, st };

        if (tenantId.HasValue) query = query.Where(x => x.bc.TenantId == tenantId);
        if (r.StoreId.HasValue) query = query.Where(x => x.bc.Store == r.StoreId);
        if (r.DateFrom.HasValue) query = query.Where(x => x.ar != null && x.ar.Receipt_Date >= r.DateFrom);
        if (r.DateTo.HasValue)   query = query.Where(x => x.ar != null && x.ar.Receipt_Date <= r.DateTo);

        var totalCount = await query.CountAsync();
        var titleCount = await query.Where(x => x.bc.BibId != null)
            .Select(x => x.bc.BibId).Distinct().CountAsync();

        var list = await query.OrderByDescending(x => x.ar != null ? x.ar.Receipt_Date : null).Take(RowCap).ToListAsync();

        var storeStats = list
            .GroupBy(x => x.st?.Name ?? "")
            .Select(g => new StoreStatRow(
                g.Key,
                g.Where(x => x.bc.BibId != null).Select(x => x.bc.BibId).Distinct().Count(),
                g.Count()))
            .ToList();

        var rows = new List<string[]>();
        var stt = 1;
        foreach (var x in list)
        {
            rows.Add([
                (stt++).ToString(),
                x.bc.BarcodeValue ?? "",
                x.bx?.Title ?? "",
                x.bx?.Author ?? "",
                x.bx?.DDC ?? "",
                x.ar?.Receipt_Date?.ToString("dd/MM/yyyy") ?? "",
                x.st?.Name ?? ""
            ]);
        }
        return (rows, totalCount, titleCount, storeStats);
    }
}
