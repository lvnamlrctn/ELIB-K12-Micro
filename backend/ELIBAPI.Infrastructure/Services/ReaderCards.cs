using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Tài liệu đang mượn hiển thị khi tra thẻ (vào/ra thư viện, mượn chìa khoá). Tên trường JSON giữ như cũ.</summary>
public sealed record ReaderCurrentLoan(long Id, string? Barcode, string? BibTitle, DateTime? BorrowDate, DateTime? DueDate,
    string? Status, int? RenewCount, double? FineValue, string? Location);

/// <summary>Tra bạn đọc theo số thẻ và tài liệu đang mượn — dùng chung cho vào/ra thư viện và mượn chìa khoá (port ELIB-LRC
/// 10-04; trước đây mỗi controller tự chép một bản, cùng lỗi coi phiếu đã trả kiểu cũ là đang mượn).</summary>
public static class ReaderCards
{
    /// <summary>Không phân biệt hoa thường, bỏ khoảng trắng 2 đầu; thẻ rỗng trả null. <paramref name="tenantId"/> null = mọi
    /// đơn vị (tài khoản hệ thống).</summary>
    public static async Task<Reader?> FindAsync(ELIBAPIDbContext db, string? cardNo, long? tenantId)
    {
        var card = (cardNo ?? "").Trim().ToLower();
        return card == "" ? null : await db.Readers.FirstOrDefaultAsync(x => x.Cardno!.Trim().ToLower() == card && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
    }

    /// <summary>Phiếu mở thật (<see cref="PrintLoans.Open"/>: Status khác "R" và chưa có BookIn) — dữ liệu cũ có phiếu đã trả
    /// nhưng Status vẫn "1"/"O". Nhan đề lấy trong 1 truy vấn (trước đây mỗi phiếu 2 truy vấn), ĐKCB khớp theo (mã, đơn vị).</summary>
    public static async Task<List<ReaderCurrentLoan>> CurrentLoansAsync(ELIBAPIDbContext db, Reader reader)
    {
        var loans = await PrintLoans.Open(db).Where(x => x.ReaderId == reader.Id).OrderByDescending(x => x.BorrowDate).ToListAsync();
        var codes = loans.Where(l => l.Barcode != null).Select(l => l.Barcode!).Distinct().ToList();
        var titles = (await (from bc in db.Barcodes
                             where bc.BarcodeValue != null && codes.Contains(bc.BarcodeValue) && bc.TenantId == reader.TenantId && bc.IsDelete != 2
                             join x in db.BibXmls on bc.BibId equals (long?)x.BibId
                             select new { bc.BarcodeValue, x.Title }).ToListAsync())
            .GroupBy(x => x.BarcodeValue!).ToDictionary(g => g.Key, g => g.First().Title);
        var storeIds = loans.Where(l => l.Store.HasValue).Select(l => l.Store!.Value).Distinct().ToList();
        var stores = storeIds.Count > 0 ? await db.Stores.Where(x => storeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name) : [];

        return loans.Select(l => new ReaderCurrentLoan(l.Id, l.Barcode, l.Barcode != null ? titles.GetValueOrDefault(l.Barcode) : null,
            l.BorrowDate, l.DueDate, l.Status, l.Renew, l.FineValue,
            l.Store is long s ? stores.GetValueOrDefault(s) : null)).ToList();
    }
}
