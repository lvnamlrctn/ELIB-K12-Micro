using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>
/// Tài liệu số (LRC: EbookItem + EbookItemXml gộp — đúng cách Bib+BibXml đã gộp thành bibs ở catalog, không mất trường nào).
/// Status dùng đúng số của IHasStatus (LRC: byte Status 1 Ẩn/2 Xuất bản — request layer có IsPublished bool? ánh xạ y vậy).
/// </summary>
public sealed class EbookItem : TenantEntity, IHasStatus
{
    private EbookItem() { }

    public int Status { get; private set; } = IHasStatus.Active;

    public long? CollectionId { get; private set; }
    public long? SubjectId { get; private set; }
    public long? TopicId { get; private set; }
    public long? TypeId { get; private set; }

    // Từ EbookItemXml (LRC: bảng riêng khoá chung Id — gộp vào đây, như Bib/BibXml).
    public string Title { get; private set; } = "";
    public string? Author { get; private set; }
    public string? Publisher { get; private set; }
    public string? PublishDate { get; private set; }
    public string? Keyword { get; private set; }
    public string? OtherTitle { get; private set; }
    public string? Page { get; private set; }
    public string? OldAuthor { get; private set; }
    public string? Xml { get; private set; }

    public string? Language { get; private set; }

    /// <summary>Công khai, không cần đăng nhập (LRC: Free — request layer đã dùng bool? IsFree, 2=công khai).</summary>
    public bool Free { get; private set; }

    /// <summary>Được tải file (LRC: AllowDownload — request layer đã dùng bool? AllowDownload, 2=được tải).</summary>
    public bool AllowDownload { get; private set; }

    /// <summary>Số bản đọc/tải đồng thời tối đa (LRC: PrintCopies); null = không giới hạn.</summary>
    public int? PrintCopies { get; private set; }

    /// <summary>Số ngày giữ quyền truy cập sau khi mượn (LRC: OfflineDays); null = không giới hạn/theo chính sách.</summary>
    public int? OfflineDays { get; private set; }

    /// <summary>Hiện trên trang chủ/nổi bật (LRC: Show) — giữ nguyên kiểu int? vì LRC chưa chuẩn hoá thành cờ nhị phân rõ nghĩa.</summary>
    public int? Show { get; private set; }

    /// <summary>Đưa vào chỉ mục toàn văn (LRC: IndexContent) — chưa dùng ở MVP (hoãn digital-indexer), giữ để nạp lại khi làm sau.</summary>
    public int? IndexContent { get; private set; }

    /// <summary>Cho phép chia sẻ (LRC: Share) — chưa dùng ở MVP (hoãn tính năng đánh giá/chia sẻ).</summary>
    public int? Share { get; private set; }

    public int? FileVersion { get; private set; }
    public DateTimeOffset? IndexedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    public int TotalView { get; private set; }
    public int TotalDownload { get; private set; }

    public DateTimeOffset? Submited { get; private set; }

    /// <summary>Lần cập nhật nội dung gần nhất (LRC: LastUpdate — khác UpdatedAt chung, LRC lưu cả hai riêng).</summary>
    public DateTimeOffset? LastUpdate { get; private set; }

    /// <summary>Ảnh bìa (LRC: Images) — URL công khai đã upload qua media, xem <see cref="EbookCover"/>.</summary>
    public string? CoverUrl { get; private set; }

    /// <summary>Liên kết tới biểu ghi sách in ở catalog (mới — quyết định #2 của plan, không có bên LRC).</summary>
    public Guid? LinkedBibPublicId { get; private set; }

    public static EbookItem Create(EbookItemFields fields, string mediaPublicPrefix, long tenantId, DateTimeOffset now)
    {
        var item = new EbookItem { Submited = now };
        item.Update(fields, mediaPublicPrefix, tenantId, now);
        return item;
    }

    public void Update(EbookItemFields f, string mediaPublicPrefix, long tenantId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(f);
        Title = TextRules.Required(f.Title, 1000, "TITLE_INVALID", "Nhan đề");
        Author = TextRules.Optional(f.Author, 500, "AUTHOR_INVALID", "Tác giả");
        Publisher = TextRules.Optional(f.Publisher, 500, "PUBLISHER_INVALID", "Nhà xuất bản");
        PublishDate = TextRules.Optional(f.PublishDate, 50, "PUBLISH_DATE_INVALID", "Năm xuất bản");
        Keyword = TextRules.Optional(f.Keyword, 1000, "KEYWORD_INVALID", "Từ khoá");
        OtherTitle = TextRules.Optional(f.OtherTitle, 1000, "OTHER_TITLE_INVALID", "Nhan đề khác");
        Page = TextRules.Optional(f.Page, 50, "PAGE_INVALID", "Số trang");
        OldAuthor = TextRules.Optional(f.OldAuthor, 500, "OLD_AUTHOR_INVALID", "Người hướng dẫn");
        Xml = TextRules.Optional(f.Xml, 50_000, "XML_INVALID", "Dữ liệu XML");
        Language = TextRules.Optional(f.Language, 10, "LANGUAGE_INVALID", "Ngôn ngữ");

        CollectionId = f.CollectionId;
        SubjectId = f.SubjectId;
        TopicId = f.TopicId;
        TypeId = f.TypeId;
        Free = f.Free;
        AllowDownload = f.AllowDownload;
        PrintCopies = f.PrintCopies is null or >= 0 ? f.PrintCopies : throw new BusinessRuleException("PRINT_COPIES_INVALID", "Số bản không được âm.");
        OfflineDays = f.OfflineDays is null or >= 0 ? f.OfflineDays : throw new BusinessRuleException("OFFLINE_DAYS_INVALID", "Số ngày mượn không được âm.");
        Show = f.Show;
        IndexContent = f.IndexContent;
        Share = f.Share;
        CoverUrl = EbookCover.Validate(f.CoverUrl, mediaPublicPrefix, tenantId);
        LinkedBibPublicId = f.LinkedBibPublicId;
        if (Status != f.Status) ChangeStatus(f.Status);
        LastUpdate = now;
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>Trường ghi được của tài liệu số qua màn quản trị (LRC: EbookItemRequest + EbookItemXml, không gồm MetaDataEntries — hoãn).</summary>
public sealed record EbookItemFields(
    string Title, long? CollectionId = null, long? SubjectId = null, long? TopicId = null, long? TypeId = null, string? Author = null,
    string? Publisher = null, string? PublishDate = null, string? Keyword = null, string? OtherTitle = null, string? Page = null,
    string? OldAuthor = null, string? Xml = null, string? Language = null, bool Free = false, bool AllowDownload = false, int? PrintCopies = null,
    int? OfflineDays = null, int? Show = null, int? IndexContent = null, int? Share = null, string? CoverUrl = null, Guid? LinkedBibPublicId = null,
    int Status = IHasStatus.Active);

/// <summary>File đính kèm của tài liệu số (LRC: EbookFile) — nội dung riêng tư ở media (mục đích ebook-file), tham chiếu bằng id file.</summary>
public sealed class EbookFile : TenantEntity
{
    private EbookFile() { }

    public long EbookItemId { get; private set; }

    /// <summary>LRC: Url — id file riêng tư ở media (mục đích <c>ebook-file</c>), không lưu đường dẫn thô.</summary>
    public Guid MediaFileId { get; private set; }

    public string? Type { get; private set; }

    /// <summary>Đã chuyển đổi khỏi định dạng gốc (LRC: IsConvert).</summary>
    public bool IsConvert { get; private set; }

    public string? FileType { get; private set; }
    public double? FileSize { get; private set; }
    public string? FileExt { get; private set; }
    public string? Description { get; private set; }
    public string? Source { get; private set; }
    public int? FormatId { get; private set; }
    public string? CheckSumAlgorithm { get; private set; }
    public int SortOrder { get; private set; }

    public static EbookFile Create(long ebookItemId, Guid mediaFileId, EbookFileFields fields)
    {
        var file = new EbookFile { EbookItemId = ebookItemId, MediaFileId = mediaFileId };
        file.Update(fields);
        return file;
    }

    public void Update(EbookFileFields f)
    {
        ArgumentNullException.ThrowIfNull(f);
        Type = TextRules.Optional(f.Type, 50, "FILE_TYPE_INVALID", "Loại file");
        IsConvert = f.IsConvert;
        FileType = TextRules.Optional(f.FileType, 50, "FILE_TYPE_INVALID", "Dạng file");
        FileSize = f.FileSize is null or >= 0 ? f.FileSize : throw new BusinessRuleException("FILE_SIZE_INVALID", "Dung lượng không được âm.");
        FileExt = TextRules.Optional(f.FileExt, 20, "FILE_EXT_INVALID", "Đuôi file");
        Description = TextRules.Optional(f.Description, 500, "DESCRIPTION_INVALID", "Mô tả");
        Source = TextRules.Optional(f.Source, 250, "SOURCE_INVALID", "Nguồn");
        FormatId = f.FormatId;
        CheckSumAlgorithm = TextRules.Optional(f.CheckSumAlgorithm, 50, "CHECKSUM_ALGORITHM_INVALID", "Thuật toán checksum");
        SortOrder = f.SortOrder;
    }
}

public sealed record EbookFileFields(
    string? Type = null, bool IsConvert = false, string? FileType = null, double? FileSize = null, string? FileExt = null, string? Description = null,
    string? Source = null, int? FormatId = null, string? CheckSumAlgorithm = null, int SortOrder = 0);
