using System.Text.RegularExpressions;
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

    /// <summary>
    /// Bản sao CHỈ ĐỌC của tài liệu dùng chung do đơn vị SHARED quản lý (đồng bộ tự động, xem SharedEbookDataset): nội dung/trạng thái/file không sửa được ở đơn vị;
    /// đơn vị chỉ ẩn/hiện (<see cref="HiddenByTenant"/>), mượn/đặt trước/đếm lượt theo chính sách của mình.
    /// </summary>
    public bool IsReplica { get; private set; }

    /// <summary>Đơn vị chủ (SHARED) và PublicId của tài liệu gốc — khoá upsert của đồng bộ; chỉ đặt qua <see cref="CreateReplica"/>.</summary>
    public long? SourceTenantId { get; private set; }

    public Guid? SourcePublicId { get; private set; }

    /// <summary>Đơn vị tự ẩn bản sao khỏi OPAC/tìm kiếm; đồng bộ KHÔNG ghi đè.</summary>
    public bool HiddenByTenant { get; private set; }

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

    /// <summary>Tóm tắt (LRC: Abstract).</summary>
    public string? Abstract { get; private set; }

    /// <summary>Mô tả (LRC: Description).</summary>
    public string? Description { get; private set; }

    /// <summary>Tên tạp chí/báo (LRC: JournalName).</summary>
    public string? JournalName { get; private set; }

    public int? PublishDay { get; private set; }
    public int? PublishMonth { get; private set; }

    /// <summary>Năm xuất bản dạng số — suy từ <see cref="PublishDate"/> khi không nhập riêng; dùng để lọc khoảng năm.</summary>
    public int? PublishYear { get; private set; }

    /// <summary>Tác giả tách họ/tên (JSON). <see cref="Author"/> là bản ghép "; " của danh sách này để tìm kiếm và tương thích.</summary>
    public List<EbookPerson> Authors { get; private set; } = [];

    /// <summary>Người hướng dẫn (JSON). <see cref="OldAuthor"/> là bản ghép.</summary>
    public List<EbookPerson> Advisors { get; private set; } = [];

    /// <summary>Số định danh ISSN/ISMN/ISBN/URI/Gov't Doc #/khác (JSON).</summary>
    public List<EbookIdentifier> Identifiers { get; private set; } = [];

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

    /// <summary>Tăng mỗi lần nội dung/trạng thái/metadata đổi — dùng làm phiên bản cho sự kiện Ebook* (nơi nhận bỏ bản cũ hơn).</summary>
    public long Version { get; private set; } = 1;

    /// <summary>Đánh dấu nội dung liên quan (metadata, file) vừa đổi mà không qua <see cref="Update"/>.</summary>
    public void Touch(DateTimeOffset now)
    {
        Version++;
        LastUpdate = now;
    }

    public void RecordView() => TotalView++;
    public void RecordDownload() => TotalDownload++;

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
        item.Fill(fields, mediaPublicPrefix, tenantId, now);
        return item;
    }

    /// <summary>Tạo bản sao chỉ-đọc từ tài liệu gốc ở đơn vị chủ — chỉ đồng bộ dùng; ảnh bìa phải thuộc kho công khai của đơn vị chủ.</summary>
    public static EbookItem CreateReplica(EbookItemFields fields, string mediaPublicPrefix, long sourceTenantId, Guid sourcePublicId, DateTimeOffset? submited, DateTimeOffset now)
    {
        var item = new EbookItem { Submited = submited ?? now, IsReplica = true, SourceTenantId = sourceTenantId, SourcePublicId = sourcePublicId };
        item.Fill(fields, mediaPublicPrefix, sourceTenantId, now);
        return item;
    }

    /// <summary>Áp nội dung mới của tài liệu gốc lên bản sao (đồng bộ). Không đụng bộ đếm, <see cref="HiddenByTenant"/>.</summary>
    public void ApplyReplica(EbookItemFields fields, string mediaPublicPrefix, DateTimeOffset? submited, DateTimeOffset now)
    {
        if (!IsReplica || SourceTenantId is not { } source) throw new InvalidOperationException("Chỉ áp dụng cho bản sao dùng chung.");
        if (submited is not null) Submited = submited;
        Fill(fields, mediaPublicPrefix, source, now);
    }

    /// <summary>Các trường ghi được của tài liệu — đồng bộ dữ liệu dùng chung đọc từ tài liệu gốc ở đơn vị chủ để dựng bản sao.</summary>
    public EbookItemFields ToFields() => new(
        Title, CollectionId, SubjectId, TopicId, TypeId, Author, Publisher, PublishDate, Keyword, OtherTitle, Page, OldAuthor, Xml, Language, Free, AllowDownload,
        PrintCopies, OfflineDays, Show, IndexContent, Share, CoverUrl, LinkedBibPublicId, Status, Abstract, Description, JournalName, PublishDay, PublishMonth,
        PublishYear, Authors, Advisors, Identifiers);

    /// <summary>Tài liệu gốc bị xoá → bản sao ngừng xuất bản (giữ lượt mượn/đặt trước đang có).</summary>
    public void RetireReplica()
    {
        if (!IsReplica) throw new InvalidOperationException("Chỉ áp dụng cho bản sao dùng chung.");
        if (Status == IHasStatus.Inactive) return;
        Status = IHasStatus.Inactive;
        Version++;
    }

    /// <summary>Đơn vị ẩn/hiện bản sao — nội dung đổi nên tăng phiên bản (search bỏ/nhận lại).</summary>
    public void SetHidden(bool hidden, DateTimeOffset now)
    {
        if (!IsReplica) throw new BusinessRuleException("NOT_SHARED_REPLICA", "Chỉ ẩn/hiện được tài liệu dùng chung; tài liệu của đơn vị dùng trạng thái xuất bản.", 409);
        if (HiddenByTenant == hidden) return;
        HiddenByTenant = hidden;
        Touch(now);
    }

    /// <summary>Bản sao tài liệu dùng chung là chỉ đọc ở đơn vị (mã <c>SHARED_READ_ONLY</c>).</summary>
    public static BusinessRuleException ReadOnly(string title) =>
        new("SHARED_READ_ONLY", $"Tài liệu '{title}' là tài liệu dùng chung do quản trị hệ thống quản lý — đơn vị chỉ ẩn/hiện được.", 403);

    private void EnsureWritable()
    {
        if (IsReplica) throw ReadOnly(Title);
    }

    public void Update(EbookItemFields f, string mediaPublicPrefix, long tenantId, DateTimeOffset now)
    {
        EnsureWritable();
        Fill(f, mediaPublicPrefix, tenantId, now);
    }

    private void Fill(EbookItemFields f, string mediaPublicPrefix, long tenantId, DateTimeOffset now)
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
        Abstract = TextRules.Optional(f.Abstract, 20_000, "ABSTRACT_INVALID", "Tóm tắt");
        Description = TextRules.Optional(f.Description, 20_000, "DESCRIPTION_INVALID", "Mô tả");
        JournalName = TextRules.Optional(f.JournalName, 500, "JOURNAL_INVALID", "Tên tạp chí");
        PublishDay = f.PublishDay is null or (>= 1 and <= 31) ? f.PublishDay : throw new BusinessRuleException("PUBLISH_DAY_INVALID", "Ngày xuất bản phải từ 1 đến 31.");
        PublishMonth = f.PublishMonth is null or (>= 1 and <= 12) ? f.PublishMonth : throw new BusinessRuleException("PUBLISH_MONTH_INVALID", "Tháng xuất bản phải từ 1 đến 12.");
        PublishYear = f.PublishYear is null or (>= 1000 and <= 2999) ? f.PublishYear ?? YearOf(PublishDate)
            : throw new BusinessRuleException("PUBLISH_YEAR_INVALID", "Năm xuất bản không hợp lệ.");
        // Có danh sách → ghép lại chuỗi phẳng; không có (người ghi cũ chỉ gửi chuỗi) → giữ chuỗi và dựng danh sách từ chuỗi, để hai bên luôn khớp.
        if (f.Authors is not null)
        {
            Authors = EbookPerson.Clean(f.Authors);
            Author = EbookPerson.Join(Authors, 500, "AUTHOR_INVALID", "Tác giả");
        }
        else Authors = EbookPerson.Split(Author);
        if (f.Advisors is not null)
        {
            Advisors = EbookPerson.Clean(f.Advisors);
            OldAuthor = EbookPerson.Join(Advisors, 500, "OLD_AUTHOR_INVALID", "Người hướng dẫn");
        }
        else Advisors = EbookPerson.Split(OldAuthor);
        if (f.Identifiers is not null) Identifiers = EbookIdentifier.Clean(f.Identifiers);
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
        if (Status != f.Status) Status = StatusRules.Validate(f.Status);
        LastUpdate = now;
        Version++;
    }

    public void ChangeStatus(int status)
    {
        EnsureWritable();
        Status = StatusRules.Validate(status);
        Version++;
    }

    /// <summary>Đặt riêng ảnh bìa (vd. cắt từ một trang PDF) mà không đụng các trường khác.</summary>
    public void SetCover(string? coverUrl, string mediaPublicPrefix, long tenantId, DateTimeOffset now)
    {
        EnsureWritable();
        CoverUrl = EbookCover.Validate(coverUrl, mediaPublicPrefix, tenantId);
        Touch(now);
    }

    /// <summary>Đổi bộ sưu tập (thao tác hàng loạt) — nội dung đổi nên tăng phiên bản.</summary>
    public void MoveToCollection(long collectionId, DateTimeOffset now)
    {
        EnsureWritable();
        CollectionId = collectionId;
        Touch(now);
    }

    private static readonly Regex YearPattern = new(@"(?<!\d)([12]\d{3})(?!\d)", RegexOptions.CultureInvariant);

    /// <summary>Năm 1000–2999 đầu tiên trong chuỗi ngày xuất bản ("2010", "05/2010", "2010-05-01"); không có → null.</summary>
    public static int? YearOf(string? publishDate) =>
        publishDate is not null && YearPattern.Match(publishDate) is { Success: true } m ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
}

/// <summary>Một người (tác giả/người hướng dẫn): họ, tên, chức danh — lưu JSON trong cột của tài liệu.</summary>
public sealed class EbookPerson
{
    public const int MaxCount = 50;
    public const int MaxPart = 200;

    public string? Last { get; init; }
    public string? First { get; init; }
    public string? Title { get; init; }

    public string Display => string.Join(' ', new[] { Last, First }.Where(x => !string.IsNullOrWhiteSpace(x)));

    internal static List<EbookPerson> Clean(IEnumerable<EbookPerson> people)
    {
        var list = people.Select(p => new EbookPerson
        {
            Last = TextRules.Optional(p.Last, MaxPart, "PERSON_INVALID", "Họ"),
            First = TextRules.Optional(p.First, MaxPart, "PERSON_INVALID", "Tên"),
            Title = TextRules.Optional(p.Title, MaxPart, "PERSON_INVALID", "Chức danh"),
        }).Where(p => p.Last is not null || p.First is not null).ToList();
        return list.Count <= MaxCount ? list : throw new BusinessRuleException("PERSON_TOO_MANY", $"Tối đa {MaxCount} người.");
    }

    /// <summary>Tách chuỗi "A; B" của người ghi cũ: cả tên vào Last (không đoán họ/tên).</summary>
    internal static List<EbookPerson> Split(string? flat) =>
        string.IsNullOrWhiteSpace(flat) ? [] : [.. flat.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Take(MaxCount).Select(x => new EbookPerson { Last = x.Length <= MaxPart ? x : x[..MaxPart] })];

    internal static string? Join(List<EbookPerson> people, int max, string code, string label) =>
        TextRules.Optional(people.Count == 0 ? null : string.Join("; ", people.Select(p => p.Display)), max, code, label);
}

/// <summary>Số định danh của tài liệu (LRC: numbers).</summary>
public sealed class EbookIdentifier
{
    public static readonly string[] Types = ["issn", "ismn", "isbn", "uri", "govdoc", "other"];
    public const int MaxCount = 20;

    public string Type { get; init; } = "other";
    public string Value { get; init; } = "";

    internal static List<EbookIdentifier> Clean(IEnumerable<EbookIdentifier> values)
    {
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Select(v => new EbookIdentifier
        {
            Type = v.Type?.Trim().ToLowerInvariant() is { } type && Types.Contains(type) ? type : throw new BusinessRuleException("IDENTIFIER_TYPE_INVALID", $"Loại số định danh phải là một trong: {string.Join(", ", Types)}."),
            Value = TextRules.Required(v.Value, 300, "IDENTIFIER_INVALID", "Số định danh"),
        }).ToList();
        return list.Count <= MaxCount ? list : throw new BusinessRuleException("IDENTIFIER_TOO_MANY", $"Tối đa {MaxCount} số định danh.");
    }
}

/// <summary>Trường ghi được của tài liệu số qua màn quản trị (LRC: EbookItemRequest + EbookItemXml, không gồm MetaDataEntries — hoãn).</summary>
public sealed record EbookItemFields(
    string Title, long? CollectionId = null, long? SubjectId = null, long? TopicId = null, long? TypeId = null, string? Author = null,
    string? Publisher = null, string? PublishDate = null, string? Keyword = null, string? OtherTitle = null, string? Page = null,
    string? OldAuthor = null, string? Xml = null, string? Language = null, bool Free = false, bool AllowDownload = false, int? PrintCopies = null,
    int? OfflineDays = null, int? Show = null, int? IndexContent = null, int? Share = null, string? CoverUrl = null, Guid? LinkedBibPublicId = null,
    int Status = IHasStatus.Active, string? Abstract = null, string? Description = null, string? JournalName = null, int? PublishDay = null,
    int? PublishMonth = null, int? PublishYear = null, IReadOnlyList<EbookPerson>? Authors = null, IReadOnlyList<EbookPerson>? Advisors = null,
    IReadOnlyList<EbookIdentifier>? Identifiers = null);

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

    /// <summary>File của bản sao tài liệu dùng chung: id file gốc ở đơn vị chủ (khoá upsert) và đơn vị chủ giữ nội dung — không copy PDF.</summary>
    public Guid? SourcePublicId { get; private set; }

    public long? SourceTenantId { get; private set; }

    public bool IsReplica => SourcePublicId is not null;

    public static EbookFile Create(long ebookItemId, Guid mediaFileId, EbookFileFields fields)
    {
        var file = new EbookFile { EbookItemId = ebookItemId, MediaFileId = mediaFileId };
        file.Fill(fields);
        return file;
    }

    public EbookFileFields ToFields() => new(Type, IsConvert, FileType, FileSize, FileExt, Description, Source, FormatId, CheckSumAlgorithm, SortOrder);

    public static EbookFile CreateReplica(long ebookItemId, Guid mediaFileId, EbookFileFields fields, long sourceTenantId, Guid sourcePublicId)
    {
        var file = new EbookFile { EbookItemId = ebookItemId, MediaFileId = mediaFileId, SourceTenantId = sourceTenantId, SourcePublicId = sourcePublicId };
        file.Fill(fields);
        return file;
    }

    /// <summary>Áp thay đổi của file gốc lên file bản sao (đồng bộ).</summary>
    public void ApplyReplica(Guid mediaFileId, EbookFileFields fields)
    {
        if (!IsReplica) throw new InvalidOperationException("Chỉ áp dụng cho file bản sao dùng chung.");
        MediaFileId = mediaFileId;
        Fill(fields);
    }

    public void Update(EbookFileFields f)
    {
        if (IsReplica) throw EbookItem.ReadOnly(Description ?? "file");
        Fill(f);
    }

    private void Fill(EbookFileFields f)
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
