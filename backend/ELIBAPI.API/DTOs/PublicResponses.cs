namespace ELIBAPI.API.DTOs;

// ==================== NEWS ====================
public class PublicNewsResponse
{
    public Guid PublicId { get; set; }
    public string? Title { get; set; }
    public string? Brief { get; set; }
    public string? Content { get; set; }
    public string? Images { get; set; }
    public string? Thumb { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? CategoryId { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? Keyword { get; set; }
    public string? Author { get; set; }
    public string? Source { get; set; }
    public string? Types { get; set; }
    public int? Status { get; set; }
    public int? AllowComment { get; set; }
    public int? TotalView { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaKeyword { get; set; }
    public string? MetaDescription { get; set; }
}

// ==================== MENU ====================
public class PublicCmsMenuNode
{
    public Guid PublicId { get; set; }
    public string? Label { get; set; }
    public string? Url { get; set; }
    public List<PublicCmsMenuNode>? Children { get; set; }
}

public class PublicMenuResponse
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public long? MenuType { get; set; }
    public string? Link { get; set; }
    public string? FriendUrl { get; set; }
    public int? SortOrder { get; set; }
    public int? Status { get; set; }
    public string? OpenType { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public long? ParentId { get; set; }
    public string? LinkType { get; set; }
    public string? SubId { get; set; }
    public int? IsLogIn { get; set; }
    public string? Icon { get; set; }
}

// ==================== CATEGORY ====================
public class PublicCategoryResponse
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public long? ParentId { get; set; }
    public long? Level { get; set; }
    public int? Status { get; set; }
    public int? Order { get; set; }
    public string? PortalId { get; set; }
    public int? IsLogin { get; set; }
    public string? Description { get; set; }
    public string? Keyword { get; set; }
    public string? PageTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? Language { get; set; }
    public string? Link { get; set; }
}

public class PublicDigTypeResponse
{
    public long   Id            { get; set; }
    public Guid   PublicId      { get; set; }
    public string? Code          { get; set; }
    public string? DescriptionVn { get; set; }
    public string? DescriptionEn { get; set; }
    public int?    SortOrder     { get; set; }
    public string? PortalId      { get; set; }
    public string? Language      { get; set; }
}

/// <summary>Danh mục "môn học/lĩnh vực" (Ebook.Subject) công khai -- đổ dropdown chọn hồ sơ quan
/// tâm của bạn đọc, để chatbot gợi ý tài liệu theo hồ sơ.</summary>
public class PublicEbookSubjectResponse
{
    public long   Id        { get; set; }
    public Guid   PublicId  { get; set; }
    public string? Name      { get; set; }
    public long?   ParentId  { get; set; }
    public long?   Level     { get; set; }
    public int?    SortOrder { get; set; }
}

/// <summary>Danh mục "chủ đề" (Ebook.Topic) công khai -- đổ dropdown chọn hồ sơ quan tâm của bạn
/// đọc, để chatbot gợi ý tài liệu theo hồ sơ.</summary>
public class PublicEbookTopicResponse
{
    public long   Id       { get; set; }
    public Guid   PublicId { get; set; }
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public long?   Level    { get; set; }
    public int?    Order    { get; set; }
}

// ==================== PHOTO ====================
/// <summary>Banner carousel trang chủ app mobile (7 trường theo mock của team mobile) — xem PublicHomeBannerController.</summary>
public class PublicHomeBannerResponse
{
    public string? Topic       { get; set; }
    public string? Title       { get; set; }
    public string? Desc        { get; set; }
    public string? Image       { get; set; }
    public string? Gradient    { get; set; }
    public string? TargetType  { get; set; }
    public string? TargetValue { get; set; }
    public int?    SortOrder   { get; set; }
}

public class PublicPhotoResponse
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public string? Brief { get; set; }
    public string? Image { get; set; }
    public string? Link { get; set; }
    public string? Postion { get; set; }
    public long? PhotoAlbumId { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Status { get; set; }
    public string? PortalId { get; set; }
    public int? SortOrder { get; set; }
    public string? Language { get; set; }
    public string? Types { get; set; }
}

// ==================== PHOTO ALBUM ====================
public class PublicPhotoAlbumResponse
{
    public long Id { get; set; }
    public string? PortalId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public string? Code { get; set; }
    public string? Language { get; set; }
    public int? Status { get; set; }
    public int? SortOrder { get; set; }
    public string? Types { get; set; }
    public string? Postions { get; set; }
    public int? IsSpecial { get; set; }
}

// ==================== EBOOK COLLECTION ====================
public class PublicEbookCollectionResponse
{
    /// <summary>Id số — cần để nối cây cha/con qua ParentId (cây bộ sưu tập OPAC, Đợt 20).</summary>
    public long Id { get; set; }
    public Guid PublicId { get; set; }
    public string? Name { get; set; }
    public long? ParentId { get; set; }
    public long? Level { get; set; }
    public int? Status { get; set; }
    public int? SortOrder { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? Link { get; set; }
    public int? Allowdownload { get; set; }
    public string? Images { get; set; }
    public int TotalItems { get; set; }
}

// ==================== TENANT ====================
public class PublicTenantResponse
{
    public long    Id       { get; set; }
    public Guid    PublicId { get; set; }
    public string? Code     { get; set; }
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}

public class ResolveByHostResponse
{
    public Guid    TenantId { get; set; }
    public string? Code     { get; set; }
    public string? Name     { get; set; }
    public string? LogoText { get; set; }
    public string? LogoUrl  { get; set; }
}
