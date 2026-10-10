namespace Elib.Contracts.Events.Circulation;

/// <summary>
/// Trạng thái hiện tại của một lượt mượn (mượn, gia hạn, sửa ghi chú, trả). holdings/search hiển thị "đang mượn", reporting thống kê,
/// notification gửi biên nhận — khoá theo <see cref="LoanPublicId"/>, chỉ ghi khi <see cref="Version"/> lớn hơn bản đang có.
/// Thay cho LoanCreated/LoanRenewed/LoanReturned trong docs 03: một event trạng thái, phân biệt bằng <see cref="ReturnedAt"/>/<see cref="RenewCount"/>.
/// </summary>
public sealed record LoanChanged : IntegrationEvent
{
    public required Guid LoanPublicId { get; init; }
    public required Guid ReaderPublicId { get; init; }
    public required string CardNo { get; init; }
    public required Guid ItemPublicId { get; init; }
    public required string Barcode { get; init; }
    public required long Mfn { get; init; }
    public long? CircPlaceId { get; init; }
    public required DateTimeOffset LoanedAt { get; init; }
    public required DateTimeOffset DueAt { get; init; }

    /// <summary>Null = đang mượn.</summary>
    public DateTimeOffset? ReturnedAt { get; init; }

    public int RenewCount { get; init; }
    public long Version { get; init; }
}
