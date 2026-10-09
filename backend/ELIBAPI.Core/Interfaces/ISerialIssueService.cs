using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Đăng ký ấn phẩm định kỳ: duyệt đăng ký và quản lý các kỳ (dự kiến, nhận, khiếu nại, thống kê) — port ELIB-LRC 10-03.
/// Trạng thái kỳ: 0 dự kiến · 1 đã nhận · 2 khiếu nại · 3 thiếu · 4 trễ.
/// <para>Tenant: <c>tenantId</c> null = tài khoản đặc quyền (mọi đơn vị); có giá trị thì chỉ thao tác được đăng ký của đúng
/// đơn vị đó. Kỳ ấn phẩm luôn theo đăng ký (đơn vị của kỳ = đơn vị của đăng ký).</para>
/// </summary>
public interface ISerialIssueService
{
    /// <summary>Đăng ký đã duyệt thì không được sửa / xoá.</summary>
    Task<bool> IsApprovedAsync(Guid subscriptionPublicId, long? tenantId);
    Task<ServiceResult<SerialApproval>> ApproveAsync(Guid subscriptionPublicId, long? tenantId, long? userId);

    Task<ServiceResult<SerialIssuePage>> SearchAsync(SerialReceiptSearchRequest request, long? tenantId);
    Task<ServiceResult<SerialIssueView>> SaveAsync(SerialIssueSaveRequest request, long? tenantId, long? userId);
    Task<ServiceResult<SerialIssueView>> ClaimAsync(long id, long? tenantId, long? userId);
    /// <summary>Sinh trước các kỳ dự kiến theo tần suất + mẫu đánh số của đăng ký (tối đa 366 kỳ).</summary>
    Task<ServiceResult<List<SerialIssueView>>> PredictAsync(PredictIssuesRequest request, long? tenantId, long? userId);
    Task<ServiceResult<SerialIssueStats>> StatisticsAsync(long subscriptionId, long? tenantId);
    Task<ServiceResult<SerialIssueView?>> LastReceivedAsync(long subscriptionId, long? tenantId);
    Task<ServiceResult<string>> DeleteAsync(long? id, Guid? publicId, long? tenantId, long? userId);
}

public record SerialApproval(Guid PublicId, bool Approved, DateTime? ApprovedDate);

public record SerialIssuePage(List<SerialIssueView> Items, int TotalCount, int PageIndex, int PageSize);

public record SerialIssueStats(int Expected, int Received, int Claimed, int Missing, int Late);

public record SerialIssueView(
    long Id, long? SubscriptionId, string? SerialSeq, int? SerialSeqX, int? SerialSeqY, int? SerialSeqZ, int? IsSpecial, int? Status,
    int? Quantity, DateTime? PlannedDate, DateTime? PublishedDate, DateTime? ClaimDate, int? ClaimCount, string? Note,
    bool? IsMerged, int? SortOrder, Guid PublicId);
