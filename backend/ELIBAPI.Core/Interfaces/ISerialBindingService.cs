using ELIBAPI.Core.Common;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Đóng tập báo tạp chí (port ELIB-LRC 10-04): gom các số đã nhận (<c>SERIALITEM.STATUS = 1</c>) của một đơn đặt thành một
/// tập có số ĐKCB riêng. Mỗi số chỉ thuộc một tập đang dùng; số ĐKCB tập không trùng giữa các tập CÙNG ĐƠN VỊ.
/// <para>Tenant: tìm kiếm nhận phạm vi đã phân giải (<c>scopeTenantId</c>/<c>includeShared</c>/<c>all</c>); các thao tác theo Id
/// nhận <c>tenantId</c> (null = tài khoản đặc quyền, mọi đơn vị).</para>
/// </summary>
public interface ISerialBindingService
{
    Task<(List<SerialBindingView> Items, int Total)> SearchAsync(string? accessionNo, string? volumeTitle, long? storeId, int? pageIndex, int? pageSize,
        long? scopeTenantId, bool includeShared, bool all);

    Task<SerialBindingView?> GetAsync(Guid publicId, long? tenantId);

    /// <summary>Thêm (<paramref name="publicId"/> null) hoặc sửa. Danh sách số (<paramref name="input"/>.Items) null khi sửa thì
    /// giữ nguyên các số đã đóng; nhãn kỳ/ngày nhận lấy theo số báo trong CSDL, không theo dữ liệu gửi lên.</summary>
    Task<ServiceResult<SerialBindingView>> SaveAsync(Guid? publicId, SerialBindingInput input, long? tenantId, long? userId);

    Task<ServiceResult<bool>> DeleteAsync(Guid publicId, long? tenantId, long? userId);
}

public sealed record SerialBindingInput(string? AccessionNo, long? StoreId, string? VolumeTitle, long? SubscriptionId, string? SubscriptionTitle,
    DateTime? BindingDate, string? Note, IReadOnlyList<long>? IssueIds);

public sealed record SerialBindingIssue(long IssueId, string? SerialSeq, DateTime? PublishedDate);

/// <summary>Tên trường JSON phẳng như màn hình đọc (accessionNo, storeName, items…).</summary>
public sealed record SerialBindingView(long Id, Guid PublicId, string? AccessionNo, long? StoreId, string? StoreName, string? VolumeTitle,
    long? SubscriptionId, string? SubscriptionTitle, DateTime? BindingDate, string? Note, int ItemCount, List<SerialBindingIssue> Items,
    string? TenantName);
