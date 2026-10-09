using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Quy ước phiếu mượn sách in (port từ ELIB-LRC). Trả qua hệ thống hiện tại: giữ BookOut với Status "R" + tạo 1 BookIn
/// (BookOutId = phiếu). Dữ liệu cũ: xoá BookOut khi trả (chỉ còn BookIn), hoặc — một số ít phiếu — để nguyên Status
/// "1"/"O" dù đã có BookIn.
/// KHÔNG tự lọc đơn vị: bên gọi lọc <c>TenantId</c> trên kết quả (hoặc so khớp mã ĐKCB kèm TenantId như
/// <see cref="PrintBookAvailability"/>) — mã ĐKCB chỉ duy nhất trong 1 đơn vị.
/// </summary>
public static class PrintLoans
{
    public const string ReturnedStatus = "R";

    /// <summary>Phiếu CHƯA trả theo cả hai cách ghi nhận: Status khác "R" VÀ chưa có BookIn khớp BookOutId.</summary>
    public static IQueryable<BookOut> Open(ELIBAPIDbContext db) =>
        db.BookOuts.Where(o => o.IsDelete != 2 && o.Status != ReturnedStatus
                               && !db.BookIns.Any(i => i.BookOutId == o.Id && i.IsDelete != 2));
}
