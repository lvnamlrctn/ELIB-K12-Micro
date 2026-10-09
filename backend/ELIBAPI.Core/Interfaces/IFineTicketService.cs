using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Phiếu phạt lưu thông (màn hình Phiếu phạt): danh sách + tổng thu, lập phiếu thủ công, gom tài liệu quá hạn của bạn
/// đọc vào phiếu đang mở, xem/lưu chi tiết. Tách khỏi CFineTicketController; đơn giá lấy theo chính sách lưu thông
/// (PolicyCirc + PolicyCircFine, mã loại phí QUAHAN / XLKY / VANCHUYEN).
/// Đa đơn vị: <c>tenantId</c> = đơn vị trong JWT (null = tài khoản hệ thống, không giới hạn); phiếu/bạn đọc ngoài đơn
/// vị coi như không tồn tại; danh mục lý do phạt / chính sách / trạng thái ĐKCB lấy của đơn vị phiếu hoặc dùng chung.
/// </summary>
public interface IFineTicketService
{
    Task<PagedResult<FineTicketListRow>> SearchAsync(CFineTicketSearchRequest request);
    Task<FineTicketTotals> TotalsAsync(CFineTicketSearchRequest request);
    Task<ServiceResult<FineTicketDetail>> CreateAsync(CreateFineTicketRequest request, long? userId, long? tenantId);
    Task<ServiceResult<FineTicketDetail>> BuildForReaderAsync(BuildFineTicketRequest request, long? userId, long? tenantId);
    Task<ServiceResult<FineTicketDetail>> GetDetailAsync(Guid publicId, long? tenantId);
    Task<ServiceResult<FineTicketDetail>> SaveAsync(Guid publicId, SaveFineTicketRequest request, long? userId, long? tenantId);
}

// Tên thuộc tính giữ đúng như các đối tượng ẩn danh controller trả về trước đây (JSON camelCase không đổi).

public sealed record FineTicketListRow(
    long Id, Guid PublicId, string? Code, DateTime? FineDate, int? Status, string? CardNo, string? ReaderName,
    long? FineTypeId, string? FineTypeName, double? TotalAmount, double? PaidAmount, double Remaining, string? TenantName);

public sealed record FineTicketTotals(double TotalReceivable, double TotalReceived, double Remaining);

public sealed record FineTicketLine(
    long Id, string? Barcode, string? BibTitle, string? Fine_type_id, int OverdueDays, double UnitPrice, double? Value);

public sealed record FineFeeLegend(double OverdueRate, double TechFee, double ShippingFee);

public sealed record FineTicketDetail(
    long Id, Guid PublicId, string? Code, long? ReaderId, string? CardNo, string? ReaderName, DateTime? FineDate,
    int? Status, int? Lanphat, double? DiscountAmount, double? PaidAmount, double? TotalAmount, double Remaining,
    int? OwesDocument, long? FineTypeId, string? FineTypeName, int? FineMethodId, string? FineMethodName,
    DateTime? ReaderIssueDate, string? Note, List<FineTicketLine> Lines, FineFeeLegend FeeLegend);
