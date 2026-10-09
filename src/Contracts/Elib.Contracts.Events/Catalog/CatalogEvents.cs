namespace Elib.Contracts.Events.Catalog;

/// <summary>
/// Trạng thái hiện tại của một biểu ghi thư mục (thêm/sửa/ẩn/hiện/xoá). holdings, circulation, acquisition, search giữ
/// snapshot hiển thị từ event này (docs 02 §dữ liệu dùng chung), khoá theo <see cref="BibPublicId"/>.
/// Bản sao chỉ ghi khi <see cref="Version"/> lớn hơn bản đang có. MARC đầy đủ không đi theo event — lấy qua API catalog khi cần.
/// </summary>
public sealed record BibChanged : IntegrationEvent
{
    public required Guid BibPublicId { get; init; }

    /// <summary>Số MFN (= id biểu ghi, như monolith) — in trên nhãn, phiếu mượn.</summary>
    public required long Mfn { get; init; }

    public long? BibTypeId { get; init; }
    public required string Title { get; init; }
    public string? Author { get; init; }
    public string? Publisher { get; init; }
    public string? PublishYear { get; init; }
    public IReadOnlyList<string> Isbns { get; init; } = [];

    /// <summary>Ký hiệu phân loại DDC (082$a).</summary>
    public string? Ddc { get; init; }

    /// <summary>2 = hiện trên OPAC, 1 = ẩn.</summary>
    public int Status { get; init; }

    public bool Deleted { get; init; }
    public long Version { get; init; }
}
