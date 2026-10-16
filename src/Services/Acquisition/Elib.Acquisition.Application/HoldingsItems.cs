using Elib.Contracts.Events.Acquisition;

namespace Elib.Acquisition.Application;

/// <summary>
/// Yêu cầu đăng ký ĐKCB gửi holdings (/internal/tenants/{id}/receipt-items/Register) — kèm trạng thái hiện tại của phiếu để holdings
/// khoá số lượng theo dòng mà không chờ event. Cùng dạng JSON với <c>ReceiptItemsRequest</c> của holdings.
/// </summary>
public sealed record HoldingsRegisterRequest(
    ReceiptChanged Receipt, Guid LinePublicId, int Quantity, string? Prefix, int Digits, long? StartNumber, string? Barcode, long? StoreId);

/// <summary>Bản sách vừa đăng ký (các trường cần dùng của <c>ItemDto</c> holdings).</summary>
public sealed record RegisteredItem(Guid PublicId, string Barcode, long Mfn, long? StoreId, string? StoreName, string Status);

/// <summary>
/// Bộ lọc báo cáo gửi holdings (/receipt-items/Report, /StoreCounts). <see cref="ReceiptPublicIds"/> = các phiếu nhận khớp bộ lọc ở
/// acquisition; null = mọi bản kể cả bản không qua phiếu nhận. Cùng dạng JSON với <c>ReceiptItemReportRequest</c> của holdings.
/// </summary>
public sealed record HoldingsReportRequest(
    IReadOnlyList<Guid>? ReceiptPublicIds, long? StoreId, int Skip, int Take, long? MfnFrom = null, long? MfnTo = null, string? Title = null,
    string? Author = null, string? Publisher = null, string? PublishYear = null);

public sealed record HoldingsReportRow(
    string Barcode, long Mfn, Guid? ReceiptPublicId, Guid? LinePublicId, long? StoreId, string? StoreName, string? Title, string? Author, string? Publisher,
    string? PublishYear, string? Ddc, DateOnly RegisteredOn);

public sealed record HoldingsReportPage(IReadOnlyList<HoldingsReportRow> Items, int Total);

public sealed record HoldingsStoreCount(long? StoreId, string? StoreName, int Copies);

public sealed record HoldingsStore(long Id, string Name);

/// <summary>Bản sách của phiếu nhận ở holdings; <see cref="Delivered"/>: đã nằm ở phiếu giao nhận.</summary>
public sealed record ReceiptItem(
    Guid PublicId, Guid? LinePublicId, string Barcode, long Mfn, long? StoreId, string? StoreName, string Status, bool OnLoan, bool Delivered,
    DateTimeOffset CreatedAt);

/// <summary>
/// ĐKCB theo phiếu nhận ở holdings (service sở hữu bản sách). Lỗi nghiệp vụ của holdings (vượt số lượng, trùng mã, bản đã xếp giá…)
/// trả lại nguyên mã + thông báo; holdings không trả lời được → 503 <c>HOLDINGS_UNAVAILABLE</c>.
/// </summary>
public interface IHoldingsReceiptItems
{
    /// <summary>Trang bản sách theo số ĐKCB cho báo cáo (sổ ĐKCB); holdings lọc theo các phiếu nhận khớp bộ lọc ở acquisition.</summary>
    Task<HoldingsReportPage> ReportAsync(long tenantId, HoldingsReportRequest request, CancellationToken ct);

    /// <summary>Số bản theo kho (báo cáo phân bổ kho).</summary>
    Task<IReadOnlyList<HoldingsStoreCount>> StoreCountsAsync(long tenantId, HoldingsReportRequest request, CancellationToken ct);

    /// <summary>Tên kho — phiếu nhận chỉ giữ id kho.</summary>
    Task<IReadOnlyList<HoldingsStore>> StoresAsync(long tenantId, CancellationToken ct);

    Task<IReadOnlyList<RegisteredItem>> RegisterAsync(long tenantId, HoldingsRegisterRequest request, CancellationToken ct);

    Task<IReadOnlyList<ReceiptItem>> ListAsync(long tenantId, Guid receiptPublicId, CancellationToken ct);

    Task RemoveAsync(long tenantId, Guid receiptPublicId, Guid itemPublicId, CancellationToken ct);
}
