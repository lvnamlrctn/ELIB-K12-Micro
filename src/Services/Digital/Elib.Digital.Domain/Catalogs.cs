using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

internal static class TextRules
{
    public static string? Optional(string? value, int max, string code, string label)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} tối đa {max} ký tự.");
    }

    public static string Required(string? value, int max, string code, string label)
    {
        var v = (value ?? "").Trim();
        return v.Length is > 0 && v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} bắt buộc, tối đa {max} ký tự.");
    }
}

/// <summary>
/// Danh mục dạng cây dùng chung bởi <see cref="EbookCollection"/>/<see cref="EbookSubject"/>/<see cref="EbookTopic"/> (LRC: cả ba đều có
/// ParentId/Level/SortOrder). Cùng khuôn với Tenant's <c>Org</c> — Level luôn suy ra từ cha, tối đa <see cref="MaxDepth"/> cấp.
/// </summary>
public abstract class TreeCatalogItem : TenantEntity, IHasStatus
{
    public const int MaxDepth = 10;

    public string Name { get; private set; } = "";
    public long? ParentId { get; private set; }
    public int Level { get; private set; } = 1;
    public int SortOrder { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    protected abstract int MaxNameLength { get; }

    protected void Rename(string name)
    {
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and var length && length <= MaxNameLength
            ? n
            : throw new BusinessRuleException("NAME_INVALID", $"Tên bắt buộc, tối đa {MaxNameLength} ký tự.");
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public void Reorder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>Gắn vào cha mới (null = gốc) — Level suy từ cha. Không kiểm vòng lặp — Resource tự kiểm trước khi gọi (xem MoveCoreAsync).</summary>
    public void AttachTo(TreeCatalogItem? parent)
    {
        if (parent is not null && parent.Id == Id)
            throw new BusinessRuleException("CATALOG_PARENT_INVALID", "Không chọn chính mục này làm mục cha.");
        ParentId = parent?.Id;
        Level = (parent?.Level ?? 0) + 1;
        if (Level > MaxDepth) throw new BusinessRuleException("CATALOG_TOO_DEEP", $"Cây tối đa {MaxDepth} cấp.");
    }

    /// <summary>Con cháu đổi Level theo cha khi cha bị chuyển (xem TreeCatalogResource.MoveCoreAsync).</summary>
    public void ShiftLevel(int delta)
    {
        Level += delta;
        if (Level > MaxDepth) throw new BusinessRuleException("CATALOG_TOO_DEEP", $"Cây tối đa {MaxDepth} cấp.");
    }
}

/// <summary>Bộ sưu tập tài liệu số (LRC: EbookCollection, quyền DIGITAL_COLLECTIONS) — cây, ebook thuộc một bộ sưu tập.</summary>
#pragma warning disable CA1711 // tên theo đúng entity LRC (EbookCollection) — không phải kiểu collection (ICollection/IEnumerable)
public sealed class EbookCollection : TreeCatalogItem
#pragma warning restore CA1711
{
    private EbookCollection() { }

    protected override int MaxNameLength => 250;

    public string? Language { get; private set; }
    public string? Link { get; private set; }
    public bool AllowDownload { get; private set; }
    public bool Share { get; private set; }

    /// <summary>Ảnh bìa bộ sưu tập (LRC: Images) — id file ở media, không lưu đường dẫn thô.</summary>
    public Guid? CoverFileId { get; private set; }

    public static EbookCollection Create(string name, string? language, string? link, bool allowDownload, bool share, Guid? coverFileId)
    {
        var collection = new EbookCollection();
        collection.Update(name, language, link, allowDownload, share, coverFileId);
        return collection;
    }

    public void Update(string name, string? language, string? link, bool allowDownload, bool share, Guid? coverFileId)
    {
        Rename(name);
        Language = TextRules.Optional(language, 10, "LANGUAGE_INVALID", "Ngôn ngữ");
        Link = TextRules.Optional(link, 500, "LINK_INVALID", "Liên kết");
        AllowDownload = allowDownload;
        Share = share;
        CoverFileId = coverFileId;
    }
}

/// <summary>Môn loại tài liệu số (LRC: EbookSubject, quyền DIGITAL_SUBJECTS) — cây, theo khung phân loại DDC.</summary>
public sealed class EbookSubject : TreeCatalogItem
{
    private EbookSubject() { }

    protected override int MaxNameLength => 250;

    public string? Ddc { get; private set; }
    public string? Language { get; private set; }
    public string? Link { get; private set; }

    public static EbookSubject Create(string name, string? ddc, string? language, string? link)
    {
        var subject = new EbookSubject();
        subject.Update(name, ddc, language, link);
        return subject;
    }

    public void Update(string name, string? ddc, string? language, string? link)
    {
        Rename(name);
        Ddc = TextRules.Optional(ddc, 50, "DDC_INVALID", "Ký hiệu phân loại");
        Language = TextRules.Optional(language, 10, "LANGUAGE_INVALID", "Ngôn ngữ");
        Link = TextRules.Optional(link, 500, "LINK_INVALID", "Liên kết");
    }
}

/// <summary>Chủ đề tài liệu số (LRC: EbookTopic, quyền DIGITAL_TOPICS) — cây, có trang riêng trên OPAC (PageTitle/MetaDescription).</summary>
public sealed class EbookTopic : TreeCatalogItem
{
    private EbookTopic() { }

    protected override int MaxNameLength => 250;

    /// <summary>Bắt buộc đăng nhập mới xem được chủ đề (LRC: IsLogin).</summary>
    public bool RequiresLogin { get; private set; }

    public string? Description { get; private set; }
    public string? Keyword { get; private set; }
    public string? PageTitle { get; private set; }
    public string? MetaDescription { get; private set; }
    public string? Language { get; private set; }
    public string? Ddc { get; private set; }

    public static EbookTopic Create(
        string name, bool requiresLogin, string? description, string? keyword, string? pageTitle, string? metaDescription, string? language, string? ddc)
    {
        var topic = new EbookTopic();
        topic.Update(name, requiresLogin, description, keyword, pageTitle, metaDescription, language, ddc);
        return topic;
    }

    public void Update(
        string name, bool requiresLogin, string? description, string? keyword, string? pageTitle, string? metaDescription, string? language, string? ddc)
    {
        Rename(name);
        RequiresLogin = requiresLogin;
        Description = TextRules.Optional(description, 2000, "DESCRIPTION_INVALID", "Mô tả");
        Keyword = TextRules.Optional(keyword, 500, "KEYWORD_INVALID", "Từ khoá");
        PageTitle = TextRules.Optional(pageTitle, 250, "PAGE_TITLE_INVALID", "Tiêu đề trang");
        MetaDescription = TextRules.Optional(metaDescription, 500, "META_DESCRIPTION_INVALID", "Mô tả SEO");
        Language = TextRules.Optional(language, 10, "LANGUAGE_INVALID", "Ngôn ngữ");
        Ddc = TextRules.Optional(ddc, 50, "DDC_INVALID", "Ký hiệu phân loại");
    }
}

/// <summary>Loại tài liệu số (LRC: DigType, quyền DIGITAL_DIG_TYPES) — danh mục phẳng, mã không trùng trong đơn vị.</summary>
public sealed class EbookDigType : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxDescriptionLength = 250;

    private EbookDigType() { }

    public string Code { get; private set; } = "";
    public string? DescriptionVn { get; private set; }
    public string? DescriptionEn { get; private set; }
    public int SortOrder { get; private set; }
    public string? Language { get; private set; }

    public static EbookDigType Create(string code, string? descriptionVn, string? descriptionEn, int sortOrder, string? language)
    {
        var type = new EbookDigType();
        type.Update(code, descriptionVn, descriptionEn, sortOrder, language);
        return type;
    }

    public void Update(string code, string? descriptionVn, string? descriptionEn, int sortOrder, string? language)
    {
        Code = TextRules.Required(code, MaxCodeLength, "CODE_INVALID", "Mã loại tài liệu số");
        DescriptionVn = TextRules.Optional(descriptionVn, MaxDescriptionLength, "DESCRIPTION_INVALID", "Mô tả (Tiếng Việt)");
        DescriptionEn = TextRules.Optional(descriptionEn, MaxDescriptionLength, "DESCRIPTION_INVALID", "Mô tả (English)");
        SortOrder = sortOrder;
        Language = TextRules.Optional(language, 10, "LANGUAGE_INVALID", "Ngôn ngữ");
    }
}
