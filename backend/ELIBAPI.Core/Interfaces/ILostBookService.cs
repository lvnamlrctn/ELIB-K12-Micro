using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Sách mất (PrintBook.LostBook + <c>Barcode.Status = "L"</c>), port ELIB-LRC 10-04. Danh sách gồm cả ĐKCB đang "L" mà không
/// có bản ghi LostBook (dữ liệu cũ, hoặc mất do bạn đọc qua phiếu phạt "Mất tài liệu") — trước đây các bản này không hiện ở
/// đâu và không khôi phục được.
/// Đa đơn vị: danh sách lọc theo phạm vi đơn vị đã phân giải ở controller (<c>scopeTenantId</c>/<c>includeShared</c>/
/// <c>all</c>, xem TenantScopeHelper.ResolveScopeAsync); tra/báo mất/khôi phục theo ĐKCB tìm trong đơn vị JWT
/// (<c>tenantId</c> null = tài khoản hệ thống, mã trùng nhiều đơn vị thì báo lỗi).
/// </summary>
public interface ILostBookService
{
    /// <summary><paramref name="paged"/> false = toàn bộ (xuất Excel). Ngày "đến" không kèm giờ tính trọn ngày; lọc ngày chỉ áp
    /// cho bản có bản ghi báo mất (bản không có bản ghi không có ngày mất).</summary>
    Task<PagedResult<LostBookRow>> SearchAsync(LostBookSearchRequest request, bool paged, long? scopeTenantId, bool includeShared, bool all);

    Task<ServiceResult<LostBookLookup>> LookupAsync(string? barcode, long? tenantId);

    /// <summary>Báo mất: ghi LostBook + ĐKCB "L". Không cho bản đang mượn (xử lý qua phiếu phạt) hoặc đã thanh lý.</summary>
    Task<ServiceResult<LostBook>> MarkLostAsync(string? barcode, DateTime? lossDate, string? reason, long? userId, long? tenantId);

    /// <summary>Khôi phục: xoá bản ghi báo mất; ĐKCB đang "L" thì trả về "R".</summary>
    Task<ServiceResult<bool>> RestoreAsync(string? barcode, long? userId, long? tenantId);
}

/// <summary><c>Recorded</c> false = ĐKCB đang "L" nhưng không có bản ghi báo mất (<c>Id</c> = 0, không có ngày/lý do).</summary>
public sealed record LostBookRow(long Id, Guid? PublicId, string? Barcode, string? BibTitle, long? Store, string? StoreName,
    DateTime? Submited, string? Reason, bool Recorded, long? TenantId, string? TenantName);

public sealed record LostBookLookup(string? Barcode, long? Mfn, int? StoreId, string? StoreName, string? Status, string? StatusName, string? Isbd);
