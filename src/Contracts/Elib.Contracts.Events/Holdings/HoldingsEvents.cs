namespace Elib.Contracts.Events.Holdings;

/// <summary>
/// Trạng thái hiện tại của một bản sách (ĐKCB): đăng ký, xếp giá, chuyển kho, mất, thanh lý, xuất kho, xoá.
/// circulation giữ <c>ItemReplica</c>, search cập nhật số bản sẵn có — khoá theo <see cref="ItemPublicId"/>, chỉ ghi khi
/// <see cref="Version"/> lớn hơn bản đang có. Trạng thái "đang mượn" KHÔNG có ở đây — circulation sở hữu (docs 02 §dữ liệu dùng chung).
/// </summary>
public sealed record ItemChanged : IntegrationEvent
{
    public required Guid ItemPublicId { get; init; }

    /// <summary>Số ĐKCB (mã vạch) — duy nhất trong đơn vị, không phân biệt hoa/thường.</summary>
    public required string Barcode { get; init; }

    public required Guid BibPublicId { get; init; }

    /// <summary>MFN của biểu ghi (catalog).</summary>
    public required long Mfn { get; init; }

    public long? StoreId { get; init; }
    public string? StoreName { get; init; }

    /// <summary>
    /// Trạng thái vật lý như monolith: I = chưa xếp giá, R = sẵn sàng trên giá, L = mất, S = thanh lý, X = xuất kho.
    /// Chỉ R cho mượn được.
    /// </summary>
    public required string Status { get; init; }

    public bool Deleted { get; init; }
    public long Version { get; init; }
}
