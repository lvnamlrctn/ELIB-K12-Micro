using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>
/// Chính sách tài liệu số theo loại bạn đọc (LRC: PolicyDigital, quyền DIGITAL_POLICIES); null = áp cho mọi loại bạn đọc.
/// Mỗi loại bạn đọc (kể cả "mọi loại") chỉ một chính sách — cụ thể hơn <see cref="PolicyDigitalByCollection"/> ghi đè theo bộ sưu tập.
/// </summary>
public sealed class PolicyDigital : TenantEntity
{
    private PolicyDigital() { }

    public long? ReaderTypeId { get; private set; }

    /// <summary>Số trang tối đa được đọc/tải của một tài liệu; null = không giới hạn (LRC: MaxPage).</summary>
    public int? MaxPage { get; private set; }

    /// <summary>Dung lượng tối đa (byte) được tải; null = không giới hạn (LRC: MaxSize).</summary>
    public long? MaxSize { get; private set; }

    /// <summary>Số tài liệu được mượn cùng lúc; null = không giới hạn (LRC: MaxDocument).</summary>
    public int? MaxDocument { get; private set; }

    /// <summary>Số giờ giữ chỗ sau khi đặt trước tới lượt, quá hạn tự huỷ (LRC: ReservationExpireHours).</summary>
    public int? ReservationExpireHours { get; private set; }

    public static PolicyDigital Create(long? readerTypeId, int? maxPage, long? maxSize, int? maxDocument, int? reservationExpireHours)
    {
        var policy = new PolicyDigital();
        policy.Update(readerTypeId, maxPage, maxSize, maxDocument, reservationExpireHours);
        return policy;
    }

    public void Update(long? readerTypeId, int? maxPage, long? maxSize, int? maxDocument, int? reservationExpireHours)
    {
        ReaderTypeId = readerTypeId;
        MaxPage = maxPage is null or >= 0 ? maxPage : throw new BusinessRuleException("MAX_PAGE_INVALID", "Số trang tối đa không được âm.");
        MaxSize = maxSize is null or >= 0 ? maxSize : throw new BusinessRuleException("MAX_SIZE_INVALID", "Dung lượng tối đa không được âm.");
        MaxDocument = maxDocument is null or >= 0
            ? maxDocument
            : throw new BusinessRuleException("MAX_DOCUMENT_INVALID", "Số tài liệu được mượn không được âm.");
        ReservationExpireHours = reservationExpireHours is null or >= 1
            ? reservationExpireHours
            : throw new BusinessRuleException("RESERVATION_HOURS_INVALID", "Số giờ giữ đặt trước phải lớn hơn 0.");
    }

    /// <summary>Độ cụ thể khi chọn chính sách áp dụng — càng lớn càng ưu tiên.</summary>
    public int Specificity => ReaderTypeId is null ? 0 : 1;
}

/// <summary>
/// Chính sách tài liệu số theo loại bạn đọc × bộ sưu tập (LRC: PolicyDigitalByCollection, quyền DIGITAL_POLICIES_BY_COLLECTION) —
/// ghi đè <see cref="PolicyDigital"/> cho một bộ sưu tập cụ thể.
/// </summary>
#pragma warning disable CA1711 // tên theo đúng entity LRC (PolicyDigitalByCollection) — không phải kiểu collection (ICollection/IEnumerable)
public sealed class PolicyDigitalByCollection : TenantEntity
#pragma warning restore CA1711
{
    private PolicyDigitalByCollection() { }

    public long? ReaderTypeId { get; private set; }
    public long CollectionId { get; private set; }
    public int? MaxPage { get; private set; }
    public long? MaxSize { get; private set; }
    public int? MaxDocument { get; private set; }

    /// <summary>Được xem (LRC: Read) — tắt thì bộ sưu tập ẩn với loại bạn đọc này dù tài liệu công khai.</summary>
    public bool Read { get; private set; } = true;

    /// <summary>Được bình luận (LRC: Comment) — chưa có nghiệp vụ bình luận ở MVP, chỉ lưu cấu hình.</summary>
    public bool Comment { get; private set; }

    /// <summary>Được tải file (LRC: Download).</summary>
    public bool Download { get; private set; }

    /// <summary>Số ngày mượn áp cho bộ sưu tập này; null = theo tài liệu/chính sách chung (LRC: OfflineDays).</summary>
    public int? OfflineDays { get; private set; }

    public int? ReservationExpireHours { get; private set; }

    public static PolicyDigitalByCollection Create(
        long? readerTypeId, long collectionId, int? maxPage, long? maxSize, int? maxDocument, bool read, bool comment, bool download,
        int? offlineDays, int? reservationExpireHours)
    {
        var policy = new PolicyDigitalByCollection();
        policy.Update(readerTypeId, collectionId, maxPage, maxSize, maxDocument, read, comment, download, offlineDays, reservationExpireHours);
        return policy;
    }

    public void Update(
        long? readerTypeId, long collectionId, int? maxPage, long? maxSize, int? maxDocument, bool read, bool comment, bool download,
        int? offlineDays, int? reservationExpireHours)
    {
        ReaderTypeId = readerTypeId;
        CollectionId = collectionId > 0 ? collectionId : throw new BusinessRuleException("COLLECTION_REQUIRED", "Chọn bộ sưu tập.");
        MaxPage = maxPage is null or >= 0 ? maxPage : throw new BusinessRuleException("MAX_PAGE_INVALID", "Số trang tối đa không được âm.");
        MaxSize = maxSize is null or >= 0 ? maxSize : throw new BusinessRuleException("MAX_SIZE_INVALID", "Dung lượng tối đa không được âm.");
        MaxDocument = maxDocument is null or >= 0
            ? maxDocument
            : throw new BusinessRuleException("MAX_DOCUMENT_INVALID", "Số tài liệu được mượn không được âm.");
        Read = read;
        Comment = comment;
        Download = download;
        OfflineDays = offlineDays is null or >= 0 ? offlineDays : throw new BusinessRuleException("OFFLINE_DAYS_INVALID", "Số ngày mượn không được âm.");
        ReservationExpireHours = reservationExpireHours is null or >= 1
            ? reservationExpireHours
            : throw new BusinessRuleException("RESERVATION_HOURS_INVALID", "Số giờ giữ đặt trước phải lớn hơn 0.");
    }

    public int Specificity => ReaderTypeId is null ? 0 : 1;
}
