using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Thanh lý tài liệu (PrintBook.Thanhly + <c>Barcode.Status = "S"</c>), port ELIB-LRC 10-04. Danh sách trả đủ trường màn
/// hình cần (trước đây API trả entity thô nên mọi cột trống và nút "Huỷ thanh lý" không làm gì).
/// Đa đơn vị: danh sách lọc theo phạm vi controller phân giải (<c>scopeTenantId</c>/<c>includeShared</c>/<c>all</c>);
/// thanh lý/huỷ theo ĐKCB tìm trong đơn vị JWT (<c>tenantId</c>).
/// </summary>
public interface ILiquidateService
{
    Task<PagedResult<LiquidateRow>> SearchAsync(ThanhlySearchRequest request, bool paged, long? scopeTenantId, bool includeShared, bool all);

    /// <summary>Không cho bản đang mượn (kể cả "R" còn phiếu mượn mở) hoặc đang ra kho (X). Bản đã báo mất vẫn thanh lý được.</summary>
    Task<ServiceResult<Thanhly>> LiquidateAsync(string? barcode, string? reason, DateTime? date, long? userId, long? tenantId);

    /// <summary>Huỷ thanh lý: ĐKCB đang "S" trở về "L" nếu còn bản ghi báo mất, không thì "R".</summary>
    Task<ServiceResult<bool>> CancelAsync(string? barcode, long? userId, long? tenantId);
}

public sealed record LiquidateRow(int Id, Guid PublicId, long? BarcodeId, string? Barcode, long? Mfn, string? BibTitle, string? Author,
    string? Publisher, long? StoreId, string? StoreName, DateTime? LiquidateDate, string? Reason, long? TenantId, string? TenantName);
