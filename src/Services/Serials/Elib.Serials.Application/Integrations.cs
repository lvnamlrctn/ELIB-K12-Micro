namespace Elib.Serials.Application;

public sealed record SerialItemRegisterRequest(Guid BindingPublicId, long Mfn, string AccessionNo, long? StoreId, string? Note);

public sealed record SerialItemRegistered(Guid PublicId, string Barcode, long Mfn, long? StoreId, string? StoreName);

/// <summary>
/// ĐKCB của tập đóng ở holdings (/internal/tenants/{id}/serial-items, service token). Register idempotent theo <c>BindingPublicId</c>;
/// Remove gỡ ĐKCB (holdings chặn khi bản đã lưu thông) và idempotent.
/// </summary>
public interface IHoldingsSerialItems
{
    Task<SerialItemRegistered> RegisterAsync(long tenantId, SerialItemRegisterRequest request, CancellationToken ct);

    Task RemoveAsync(long tenantId, Guid bindingPublicId, CancellationToken ct);
}

public sealed record SupplierOption(Guid PublicId, string Name);

/// <summary>Nhà cung cấp của acquisition — tuỳ chọn: đơn vị không có phân hệ Bổ sung hoặc acquisition không trả lời thì trả danh sách rỗng.</summary>
public interface ISupplierSource
{
    Task<IReadOnlyList<SupplierOption>> ListAsync(long tenantId, CancellationToken ct);
}
