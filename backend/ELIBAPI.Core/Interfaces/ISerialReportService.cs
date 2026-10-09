namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Báo cáo báo tạp chí (màn Báo cáo ấn phẩm định kỳ). <c>SERIALITEM.STATUS</c>: 0/null dự kiến · 1 đã nhận · 2 khiếu nại · 3 thiếu;
/// <c>PUBLISHED_DATE</c> là ngày nhận. Lọc "Mã đơn đặt": số = Id đơn đặt, chữ = một phần nhan đề (không phân biệt hoa thường).
/// Port ELIB-LRC 10-04. Tenant: chỉ đơn đặt trong phạm vi <see cref="SerialReportFilter"/> (trước đây báo cáo gồm mọi đơn vị).
/// </summary>
public interface ISerialReportService
{
    /// <summary>Tổng hợp theo đơn đặt các số nhận trong khoảng ngày nhận.</summary>
    Task<(List<SerialReceivedSummaryRow> Items, int Total)> ReceivedSummaryAsync(SerialReportFilter filter, int? pageIndex, int? pageSize);

    /// <summary>Các số nhận trong khoảng ngày nhận, mới nhất trước.</summary>
    Task<(List<SerialReportRow> Items, int Total)> ReceivedDetailAsync(SerialReportFilter filter, int? pageIndex, int? pageSize);

    /// <summary>Số chưa nhận cần đòi: đã khiếu nại, đánh dấu thiếu, hoặc quá ngày dự kiến mà chưa về; lọc theo ngày dự kiến.</summary>
    Task<(List<SerialReportRow> Items, int Total)> MissingClaimAsync(SerialReportFilter filter, int? pageIndex, int? pageSize);
}

/// <summary><c>ScopeTenantId</c>/<c>IncludeShared</c>/<c>All</c>: phạm vi đơn vị đã phân giải (xem TenantScopeHelper.ResolveScopeAsync).</summary>
public sealed record SerialReportFilter(string? Subscription, DateTime? DateFrom, DateTime? DateTo,
    long? ScopeTenantId, bool IncludeShared, bool All);

/// <summary>Tên trường JSON khớp màn hình: subscriptionTitle, issn, quantity (tổng số bản nhận), receivedIssues (số kỳ).</summary>
public sealed record SerialReceivedSummaryRow(long? SubscriptionId, string? SubscriptionTitle, string? Issn, int Quantity, int ReceivedIssues, DateTime? LastReceived);

public sealed record SerialReportRow(long Id, long? SubscriptionId, string? SubscriptionTitle, string? Issn, string? SerialSeq, DateTime? PlannedDate,
    DateTime? PublishedDate, DateTime? ClaimDate, int? ClaimCount, int? Status, int? Quantity);
