namespace Elib.Contracts.Events.Acquisition;

/// <summary>
/// Trạng thái hiện tại của một phiếu nhận (đơn nhận) bổ sung: thêm/sửa, thêm/sửa/bỏ dòng, hoàn tất (khoá), mở lại, xoá.
/// holdings giữ bản sao đầu phiếu (mã, ngày nhận, đã hoàn tất) để chọn sách theo đơn nhận khi giao nhận và lọc báo cáo kho theo
/// ngày nhận; reporting thống kê bổ sung theo dòng. Khoá theo <see cref="ReceiptPublicId"/>, chỉ ghi khi <see cref="Version"/> lớn hơn.
/// </summary>
public sealed record ReceiptChanged : IntegrationEvent
{
    public required Guid ReceiptPublicId { get; init; }

    /// <summary>Số đơn nhận (= id, như monolith ab_receipt.Code) — in trên phiếu, chọn ở màn giao nhận.</summary>
    public required long Code { get; init; }

    public string? Name { get; init; }

    /// <summary>Ngày nhận (ngày lịch VN) — báo cáo kho lọc theo ngày này.</summary>
    public DateOnly? ReceivedOn { get; init; }

    /// <summary>Kho nhận dự kiến (id kho của holdings).</summary>
    public long? StoreId { get; init; }

    public string? SourceName { get; init; }
    public string? SupplierName { get; init; }

    /// <summary>Đã hoàn tất — khoá, không thêm/bớt dòng và ĐKCB.</summary>
    public bool Completed { get; init; }

    public bool Deleted { get; init; }
    public long Version { get; init; }
    public IReadOnlyList<ReceiptLineState> Lines { get; init; } = [];
}

/// <summary>Một dòng của phiếu nhận: biểu ghi, số bản nhận (= số ĐKCB tối đa đăng ký được cho dòng), đơn giá.</summary>
public sealed record ReceiptLineState
{
    public required Guid LinePublicId { get; init; }
    public required Guid BibPublicId { get; init; }
    public required long Mfn { get; init; }
    public required int Quantity { get; init; }
    public decimal? Price { get; init; }
    public string? Currency { get; init; }
}
