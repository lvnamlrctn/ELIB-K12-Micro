namespace ELIBAPI.Core.DTOs.Request;

// ==================== FACE RECOGNITION ====================
public class IdentifyFaceRequest
{
    public string ImageBase64 { get; set; } = "";
}

// ==================== BASE ====================
public class SearchRequest
{
    public string? Keyword   { get; set; }
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
    public int?    Status    { get; set; }
    public int     PageIndex { get; set; } = 1;
    public int     PageSize  { get; set; } = 10;
    public Guid?   TenantId  { get; set; }
}

public class PublicSearchRequest
{
    public string? Keyword      { get; set; }
    public string? PortalId     { get; set; }
    public string? Language     { get; set; }
    public Guid   TenantId { get; set; }
    public int     PageIndex    { get; set; } = 1;
    public int     PageSize     { get; set; } = 10;
}

public class ChangeStatusRequest
{
    public Guid PublicId { get; set; }
    public int  Status   { get; set; }
}

public class ChangeIsSpecialRequest
{
    public Guid PublicId { get; set; }
    public bool IsSpecial { get; set; }
}

// ==================== NEWS ====================
public class NewsRequest
{
    public string?   Title          { get; set; }
    public string?   Brief          { get; set; }
    public string?   Content        { get; set; }
    public string?   Images         { get; set; }
    public string?   Thumb          { get; set; }
    public DateTime? StartTime      { get; set; }
    public DateTime? EndTime        { get; set; }
    public long?     CategoryId     { get; set; }
    public int?      EventId        { get; set; }
    public string?   PortalId       { get; set; }
    public string?   Language       { get; set; }
    public string?   Keyword        { get; set; }
    public string?   Author         { get; set; }
    public string?   Source         { get; set; }
    public string?   Types          { get; set; }
    public string?   Clourse        { get; set; }
    public int?      Status         { get; set; }
    public int?      AllowComment   { get; set; }
    public string?   MetaTitle      { get; set; }
    public string?   MetaKeyword    { get; set; }
    public string?   MetaDescription { get; set; }
    public string?   MaleAudio      { get; set; }
    public string?   FaleAudio      { get; set; }
    public string?   ContentAudio   { get; set; }
    public string?   BriefAudio     { get; set; }
    public string?   TitleAudio     { get; set; }
}

public class NewsSearchRequest : SearchRequest
{
    public long?   CategoryId { get; set; }
    public string? Types      { get; set; }
}

// ==================== CATEGORY ====================
public class CategoryRequest
{
    public string? Name            { get; set; }
    public long?   ParentId        { get; set; }
    public long?   Level           { get; set; }
    public int?    Status          { get; set; }
    public int?    Order           { get; set; }
    public string? PortalId        { get; set; }
    public int?    IsLogin         { get; set; }
    public string? Description     { get; set; }
    public string? Keyword         { get; set; }
    public string? PageTitle       { get; set; }
    public string? MetaDescription { get; set; }
    public string? Language        { get; set; }
    public string? Link            { get; set; }
}

public class CategorySearchRequest : SearchRequest
{
    public long? ParentId { get; set; }
    public long? Level    { get; set; }
}

public class UpdateOrderRequest
{
    public Guid PublicId { get; set; }
    public int  NewOrder { get; set; }
}

public class MoveCategoryRequest
{
    public long? NewParentId { get; set; }
    public int   NewOrder    { get; set; }
}

// ==================== MENU ====================
public class MenuRequest
{
    public string? Name      { get; set; }
    public long?   MenuType  { get; set; }
    public string? Link      { get; set; }
    public string? FriendUrl { get; set; }
    public int?    SortOrder { get; set; }
    public int?    Status    { get; set; }
    public string? OpenType  { get; set; }
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
    public long?   ParentId  { get; set; }
    public string? LinkType  { get; set; }
    public string? SubId     { get; set; }
    public int?    IsLogIn   { get; set; }
    public string? Icon      { get; set; }
}

public class MenuSearchRequest : SearchRequest
{
    public Guid? MenuType { get; set; }
    public long? ParentId { get; set; }
}

// ==================== MENU TYPE ====================
public class MenuTypeRequest
{
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? Language    { get; set; }
    public string? PortalId    { get; set; }
    public string? Code        { get; set; }
}

public class MenuTypeSearchRequest : SearchRequest { }

// ==================== MODULE ====================
public class ModuleRequest
{
    public string? Name       { get; set; }
    public string? Link       { get; set; }
    public string? Icon       { get; set; }
    public long?   ParentId   { get; set; }
    public string? Language   { get; set; }
    public string? PortalId   { get; set; }
    public long?   Group      { get; set; }
    public int?    SortOrder  { get; set; }
    public string? ModuleCode { get; set; }
    public string? Type       { get; set; }
    public string? FolderPage { get; set; }
    public int?    Status     { get; set; }
}

public class ModuleSearchRequest : SearchRequest
{
    public long? ParentId { get; set; }
}

// ==================== MODULE ROLES ====================
public class ModuleRolesRequest
{
    public long?  RolesId    { get; set; }
    public long?  ModuleId   { get; set; }
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
    public byte?  Can_access { get; set; }
    public byte?  Can_add    { get; set; }
    public byte?  Can_edit   { get; set; }
    public byte?  Can_delete { get; set; }
    public byte?  Can_view   { get; set; }
}

public class ModuleRolesSearchRequest : SearchRequest
{
    public long? RolesId  { get; set; }
    public long? ModuleId { get; set; }
}

// ==================== PERMISSION ====================
public class PermissionRequest
{
    public long?  UserId    { get; set; }
    public long?  ModuleId  { get; set; }
    public byte?  Can_Access { get; set; }
    public byte?  Can_Add   { get; set; }
    public byte?  Can_Edit  { get; set; }
    public byte?  Can_Delete { get; set; }
    public byte?  Can_View  { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}

public class PermissionSearchRequest : SearchRequest
{
    public long? UserId   { get; set; }
    public long? ModuleId { get; set; }
}

// ==================== PHOTO ====================
public class PhotoRequest
{
    public string? Name         { get; set; }
    public string? Brief        { get; set; }
    public string? Image        { get; set; }
    public string? Link         { get; set; }
    public string? Postion      { get; set; }
    public long?   PhotoAlbumId { get; set; }
    public int?    Width        { get; set; }
    public int?    Height       { get; set; }
    public int?    Status       { get; set; }
    public string? PortalId     { get; set; }
    public int?    SortOrder    { get; set; }
    public string? Language     { get; set; }
    public string? Types        { get; set; }
}

public class PhotoSearchRequest : SearchRequest
{
    public long?   PhotoAlbumId { get; set; }
    public string? Types        { get; set; }
}

// ==================== PHOTO ALBUM ====================
public class PhotoAlbumRequest
{
    public string? PortalId    { get; set; }
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? Image       { get; set; }
    public string? Code        { get; set; }
    public string? Language    { get; set; }
    public int?    Status      { get; set; }
    public int?    SortOrder   { get; set; }
    public string? Types       { get; set; }
    public string? Postions    { get; set; }
    public bool?   IsSpecial   { get; set; }
}

public class PhotoAlbumSearchRequest : SearchRequest
{
    public string? Types { get; set; }
}

// ==================== ATTACH FILE ====================
public class AttachFileRequest
{
    public string? Name     { get; set; }
    public string? Url      { get; set; }
    public double? FileSize { get; set; }
    public long?   NewsId   { get; set; }
}

public class AttachFileSearchRequest : SearchRequest
{
    public long? NewsId { get; set; }
    /// <summary>Lọc theo PublicId của tin (trang quản lý file đính kèm đi từ danh sách tin).</summary>
    public Guid? NewsPublicId { get; set; }
}

// ==================== ROLES ====================
public class RolesRequest
{
    public string? Name     { get; set; }
    public string? Code     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? App      { get; set; }
}

public class RolesSearchRequest : SearchRequest { }

// ==================== CMS: ADS ====================
public class AdsRequest
{
    public string? Name       { get; set; }
    public string? Image      { get; set; }
    public int?    Width      { get; set; }
    public int?    Height     { get; set; }
    public string? Link       { get; set; }
    public int?    Status     { get; set; }
    public int?    SortOrder  { get; set; }
    public long?   AdsGroupId { get; set; }
}
public class AdsSearchRequest : SearchRequest { public long? AdsGroupId { get; set; } }

// ==================== CMS: ADS GROUP ====================
public class AdsGroupRequest
{
    public string? Type     { get; set; }
    public string? Name     { get; set; }
    public int?    Width    { get; set; }
    public int?    Height   { get; set; }
    public int?    Status   { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class AdsGroupSearchRequest : SearchRequest { }

// ==================== CMS: BANNER ====================
public class BannerRequest
{
    public string? Name     { get; set; }
    public string? Url      { get; set; }
    public string? Link     { get; set; }
    public int?    Width    { get; set; }
    public int?    Height   { get; set; }
    public int?    SortOrder{ get; set; }
    public int?    Status   { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class BannerSearchRequest : SearchRequest { }

// ==================== CMS: CONTACT ====================
public class ContactRequest
{
    public string?   Name           { get; set; }
    public string?   Email          { get; set; }
    public string?   Content        { get; set; }
    public string?   Mobile         { get; set; }
    public string?   Address        { get; set; }
    public DateTime? CreatedDate    { get; set; }
    public int?      ContactGroupId { get; set; }
    public int?      Status         { get; set; }
    public string?   PortalId       { get; set; }
    public string?   Language       { get; set; }
    public string?   Title          { get; set; }
}
public class ContactSearchRequest : SearchRequest { public int? ContactGroupId { get; set; } }

// ==================== CMS: CONTACT GROUP ====================
public class ContactGroupRequest
{
    public string? Name        { get; set; }
    public string? Email       { get; set; }
    public int?    SortOrder   { get; set; }
    public string? Description { get; set; }
    public int?    Status      { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
}
public class ContactGroupSearchRequest : SearchRequest { }

// ==================== CMS: COUNTER ====================
public class CounterRequest
{
    public long?     CounterValue { get; set; }
    public DateTime? Submited     { get; set; }
    public string?   Ip           { get; set; }
}
public class CounterSearchRequest : SearchRequest { }

// ==================== CMS: CUSTOMER ====================
public class CustomerRequest
{
    public string? Name    { get; set; }
    public string? Address { get; set; }
    public string? Phone   { get; set; }
}
public class CustomerSearchRequest : SearchRequest { }

// ==================== CMS: EVENT NEWS ====================
public class EventNewsRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class EventNewsSearchRequest : SearchRequest { }

// ==================== CMS: ITEM (CmsItem) ====================
public class CmsItemRequest
{
    public long?   ItemTypeId { get; set; }
    public string? Name       { get; set; }
    public string? Content    { get; set; }
    public string? Link       { get; set; }
    public int?    Status     { get; set; }
    public string? PortalId   { get; set; }
    public string? Language   { get; set; }
}
public class CmsItemSearchRequest : SearchRequest { public long? ItemTypeId { get; set; } }

// ==================== CMS: ITEM TYPE ====================
public class ItemTypeRequest
{
    public string? Name     { get; set; }
    public string? Code     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class ItemTypeSearchRequest : SearchRequest { }

// ==================== CMS: LINK ====================
public class LinkRequest
{
    public string? Name        { get; set; }
    public string? LinkUrl     { get; set; }
    public string? Description { get; set; }
    public string? Images      { get; set; }
    public int?    Status      { get; set; }
    public long?   LinkGroupId { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
}
public class LinkSearchRequest : SearchRequest { public long? LinkGroupId { get; set; } }

// ==================== CMS: LINK GROUP ====================
public class LinkGroupRequest
{
    public string? Name     { get; set; }
    public int?    Status   { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class LinkGroupSearchRequest : SearchRequest { }

// ==================== CMS: NEWS COMMENT ====================
public class NewsCommentRequest
{
    public string?   Content     { get; set; }
    public long?     NewsId      { get; set; }
    public string?   PortalId    { get; set; }
    public string?   Language    { get; set; }
    public string?   Email       { get; set; }
    public string?   Name        { get; set; }
    public string?   Title       { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int?      Status      { get; set; }
}
public class NewsCommentSearchRequest : SearchRequest { public long? NewsId { get; set; } }

// ==================== CMS: PAGE ====================
public class PageRequest
{
    public string? PageCode { get; set; }
    public string? PageName { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class PageSearchRequest : SearchRequest { }

// ==================== CMS: SUPPORT ONLINE ====================
public class SupportonlineRequest
{
    public string? Name     { get; set; }
    public string? Yahoo    { get; set; }
    public string? Skype    { get; set; }
    public string? Facebook { get; set; }
    public string? Email    { get; set; }
    public string? Phone    { get; set; }
    public string? Mobile   { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? Wellcome { get; set; }
}
public class SupportonlineSearchRequest : SearchRequest { }

// ==================== CMS: VIDEO ====================
public class VideoRequest
{
    public string?  Name        { get; set; }
    public string?  Link        { get; set; }
    public int      SortOrder   { get; set; }
    public string?  Description { get; set; }
    public string?  Images      { get; set; }
    public string?  VideoType   { get; set; }
    public int      Status      { get; set; }
    public string?  PortalId    { get; set; }
    public string?  Language    { get; set; }
    public DateOnly Submited    { get; set; }
}
public class VideoSearchRequest : SearchRequest { public string? VideoType { get; set; } }

// ==================== DBO: CHUC VU ====================
public class ChucVuRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public int?    Status   { get; set; }
}
public class ChucVuSearchRequest : SearchRequest { }

// ==================== DBO: CLASS ====================
public class ClassRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    
}
public class ClassSearchRequest : SearchRequest { }

// ==================== DBO: CONFIG IMPORT READER ====================
public class ConfigImportReaderRequest
{
    public string? Code  { get; set; }
    public string? Value { get; set; }
}
public class ConfigImportReaderSearchRequest : SearchRequest { }

// ==================== DBO: COURSE ====================
public class DboCoursRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public int?    Status   { get; set; }
}
public class DboCoursSearchRequest : SearchRequest { }

// ==================== DBO: CURRENCY ====================
public class CurrencyRequest
{
    public string? Code         { get; set; }
    public string? Name         { get; set; }
    public double? ExchangeRate { get; set; }
    public int?    Status       { get; set; }
}
public class CurrencySearchRequest : SearchRequest { }

// ==================== DBO: DEGREE ====================
public class DboDegreeRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    
}
public class DboDegreeSearchRequest : SearchRequest { }

// ==================== DBO: TENANT ====================
public class TenantRequest
{
    public string? Code     { get; set; }
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public int?    Status   { get; set; }
    public string? Host     { get; set; }
    public string? LogoText { get; set; }
    public string? LogoUrl  { get; set; }
}
public class TenantSearchRequest : SearchRequest { }

// ==================== DBO: ETHENIC ====================
public class EthenicRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public int?    Status   { get; set; }
}
public class EthenicSearchRequest : SearchRequest { }

// ==================== DBO: GROUP READER ====================
public class GroupReaderRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public int?    Status   { get; set; }
}
public class GroupReaderSearchRequest : SearchRequest { }

// ==================== DBO: GROUP USER ====================
public class GroupUserRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public int?    Status   { get; set; }
}
public class GroupUserSearchRequest : SearchRequest { }

// ==================== DBO: NATION ====================
public class NationRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public int?    Status   { get; set; }
}
public class NationSearchRequest : SearchRequest { }

// ==================== DBO: ORG ====================
public class OrgRequest
{
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public int?    Level    { get; set; }
    public int?    Status   { get; set; }
    public int?    Order    { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? Link     { get; set; }
}
public class OrgSearchRequest : SearchRequest { public long? ParentId { get; set; } }

// ==================== DBO: PROF ====================
public class ProfRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    
}
public class ProfSearchRequest : SearchRequest { }

// ==================== DBO: READER ====================
public class ReaderRequest
{
    public string?   FirstName    { get; set; }
    public string?   LastName     { get; set; }
    public string?   Cardno       { get; set; }
    /// <summary>Số căn cước công dân.</summary>
    public string?   CitizenId    { get; set; }
    /// <summary>UID chip thẻ (RFID/NFC) — chuẩn hoá bằng <c>CardUid.Normalize</c>, không trùng trong đơn vị.</summary>
    public string?   CardUid      { get; set; }
    public string?   Email        { get; set; }
    public string?   Phone        { get; set; }
    public string?   Address      { get; set; }
    public long?     OrgId        { get; set; }
    public long?     ReaderTypeId { get; set; }
    public long?     ClassId      { get; set; }
    public long?     CourseId     { get; set; }
    public long?     DegreeId     { get; set; }
    public long?     EthenicId    { get; set; }
    public long?     ProfId       { get; set; }
    public double?   Blane        { get; set; }
    public DateTime? CreatedDate  { get; set; }
    public DateTime? ExpireDate   { get; set; }
    public DateTime? IssueDate    { get; set; }
    public DateTime? BirthDate    { get; set; }
    public string?   Password     { get; set; }
    public string?   PortalId     { get; set; }
    public string?   Language     { get; set; }
    public string?   Photo        { get; set; }
    public int?      Status       { get; set; }
    public int?      Sex          { get; set; }
}
public class ReaderSearchRequest : SearchRequest
{
    public string?   Cardno       { get; set; }
    public string?   FirstName    { get; set; }
    public string?   LastName     { get; set; }
    public long?     ReaderTypeId { get; set; }
    public long?     OrgId        { get; set; }
    public long?     ClassId      { get; set; }
    public long?     CourseId     { get; set; }
    public DateTime? IssuedFrom   { get; set; }
    public DateTime? IssuedTo     { get; set; }
    public DateTime? ExpiredFrom  { get; set; }
    public DateTime? ExpiredTo    { get; set; }
}
public class ReaderBulkUpdateRequest
{
    public List<Guid>?       PublicIds { get; set; }
    public ReaderBulkFilter? Filter    { get; set; }

    public DateTime? ExpireDate   { get; set; }
    public DateTime? IssueDate    { get; set; }
    public long?     ClassId      { get; set; }
    public long?     CourseId     { get; set; }
    public long?     ReaderTypeId { get; set; }
    public int?      Status       { get; set; }
}
public class ReaderBatchUpdateRequest : ReaderSearchRequest
{
    /// <summary>changeClass | changeCourse | changeReaderType | changeIssueDate | changeExpireDate | changeStatus | changePassword</summary>
    public string  Action { get; set; } = "";
    public string? Value  { get; set; }
}
public class ReaderBulkFilter
{
    public long?   OrgId        { get; set; }
    public long?   ReaderTypeId { get; set; }
    public long?   ClassId      { get; set; }
    public int?    Status       { get; set; }
    public string? PortalId     { get; set; }
}

// ==================== DBO: READER EXPORT / IMPORT ====================
public class ReaderExportRequest : ReaderSearchRequest
{
    public List<string>? Fields { get; set; }
}

public class ReaderImportRow
{
    public int     RowNumber      { get; set; }
    public string? Cardno         { get; set; }
    public string? LastName       { get; set; }
    public string? FirstName      { get; set; }
    public string? CitizenId      { get; set; }
    public string? CardUid        { get; set; }
    public string? SexText        { get; set; }
    public string? BirthDateText  { get; set; }
    public string? Email          { get; set; }
    public string? Password       { get; set; }
    public string? Phone          { get; set; }
    public string? Address        { get; set; }
    public string? IssueDateText  { get; set; }
    public string? ExpireDateText { get; set; }
    public string? ClassName      { get; set; }
    public string? CourseName     { get; set; }
    public string? OrgName        { get; set; }
}

public class ReaderImportResult
{
    public int          TotalRows    { get; set; }
    public int          SuccessCount { get; set; }
    public int          FailedCount  { get; set; }
    public int          SkippedCount { get; set; }
    public List<string> Errors       { get; set; } = new();
    /// <summary>Chỉ có khi gọi với previewOnly=true (Đợt 10, tác vụ nền) — bảng đối chiếu trước/sau, chưa
    /// ghi DB.</summary>
    public ReaderMutationReview? Review { get; set; }
    /// <summary>Lớp/Khóa học/Đơn vị đã tự tạo (hoặc "sẽ tạo" khi xem trước) do bật tự tạo — Đợt 20.</summary>
    public List<string> CreatedRefs { get; set; } = new();
}

// ==================== DBO: READER DELETE ====================
public class ReaderDeleteRequest
{
    public string?   FirstName    { get; set; }
    public string?   LastName     { get; set; }
    public string?   Cardno       { get; set; }
    public string?   Email        { get; set; }
    public string?   Phone        { get; set; }
    public string?   Address      { get; set; }
    public long?     OrgId        { get; set; }
    public long?     ReaderTypeId { get; set; }
    public long?     ClassId      { get; set; }
    public long?     CourseId     { get; set; }
    public long?     DegreeId     { get; set; }
    public long?     EthenicId    { get; set; }
    public long?     ProfId       { get; set; }
    public double?   Blane        { get; set; }
    public DateTime? ExpireDate   { get; set; }
    public DateTime? IssueDate    { get; set; }
    public DateTime? BirthDate    { get; set; }
    public string?   Password     { get; set; }
    public string?   PortalId     { get; set; }
    public string?   Language     { get; set; }
    public string?   Photo        { get; set; }
    public int?      Status       { get; set; }
    public int?      Sex          { get; set; }
}
public class ReaderDeleteSearchRequest : SearchRequest
{
    public string?   FirstName      { get; set; }
    public string?   LastName       { get; set; }
    public string?   Cardno         { get; set; }
    public long?     OrgId          { get; set; }
    public long?     ReaderTypeId   { get; set; }
    public long?     ClassId        { get; set; }
    public long?     CourseId       { get; set; }
    public int?      Status         { get; set; }
    public DateTime? IssueDateFrom  { get; set; }
    public DateTime? IssueDateTo    { get; set; }
    public DateTime? ExpireDateFrom { get; set; }
    public DateTime? ExpireDateTo   { get; set; }
}

// ==================== DBO: READER IN GROUP ====================
public class ReaderInGroupRequest
{
    public long? ReaderId      { get; set; }
    public long? GroupReaderId { get; set; }
}
public class ReaderInGroupSearchRequest : SearchRequest
{
    public long? GroupReaderId { get; set; }
    public long? ReaderId      { get; set; }
}

// ==================== DBO: READER TRACKING LOGIN ====================
public class ReaderTrackingLoginRequest
{
    public long?     ReaderId   { get; set; }
    public string?   Cardnumber { get; set; }
    public DateTime? LoginTime  { get; set; }
    public DateTime? LogOutTime { get; set; }
    public string?   Ip         { get; set; }
    public string?   SessionId  { get; set; }
}
public class ReaderTrackingLoginSearchRequest : SearchRequest { public long? ReaderId { get; set; } }

// ==================== DBO: READER TYPE ====================
public class ReaderTypeRequest
{
    public string Name     { get; set; } = string.Empty;
    public string PortalId { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}
public class ReaderTypeSearchRequest : SearchRequest { }

// ==================== DBO: SIP2 LOG ====================
public class Sip2LogRequest
{
    public string?   Request  { get; set; }
    public string?   Response { get; set; }
    public DateTime? Submited { get; set; }
}
public class Sip2LogSearchRequest : SearchRequest { }

// ==================== DBO: SYSTEM PARAMETER ====================
public class SystemParameterRequest
{
    public string? Code          { get; set; }
    public string? DescriptionVn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? Value         { get; set; }
    public string? PortalId      { get; set; }
    public string? Type          { get; set; }
    public string? Language      { get; set; }
}
public class SystemParameterSearchRequest : SearchRequest { }

// ==================== DBO: USER LOG ====================
public class UserLogSearchRequest : SearchRequest
{
    public long?   UserId       { get; set; }
    public string? ActionType   { get; set; }
    public string? Application  { get; set; }
    public string? SubmitedFrom { get; set; }
    public string? SubmitedTo   { get; set; }
}

// ==================== DBO: USERS ====================
public class UsersRequest
{
    public string?   FullName      { get; set; }
    public string?   LoginName     { get; set; }
    public string?   Email         { get; set; }
    public string?   Phone         { get; set; }
    public string?   PortalId      { get; set; }
    public string?   Language      { get; set; }
    public string?   Password      { get; set; }
    public int?      RoleId        { get; set; }
    public int?      PostionId     { get; set; }
    public int?      Status        { get; set; }
    public int?      Sex           { get; set; }
    public string?   Address       { get; set; }
    public DateTime? BirthDate     { get; set; }
    public string?   Photo         { get; set; }
    public long?     RoleWinformId { get; set; }
}

public class UsersSearchRequest : SearchRequest
{
    public int? RoleId { get; set; }
}

public class ResetPasswordRequest
{
    public string Password { get; set; } = "";
}

public class BulkResetPasswordRequest
{
    public List<Guid> PublicIds { get; set; } = new();
    public string      Password  { get; set; } = "";
}

public class LockReaderRequest
{
    public string? Reason { get; set; }
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class SavePermissionRequest
{
    public Guid UserId { get; set; }
    public List<PermissionItemSaveRequest> Permissions { get; set; } = [];
}

public class PermissionItemSaveRequest
{
    public long ModuleId  { get; set; }
    public bool CanView   { get; set; }
    public bool CanAdd    { get; set; }
    public bool CanEdit   { get; set; }
    public bool CanDelete { get; set; }
}

// ==================== EBOOK: COLLECTION ====================
public class EbookCollectionRequest
{
    public string? Name          { get; set; }
    public long?   ParentId      { get; set; }
    public int?    Level         { get; set; }
    public int?    Status        { get; set; }
    public int?    Order         { get; set; }
    public string? PortalId      { get; set; }
    public string? Language      { get; set; }
    public int?    Allowdownload { get; set; }
    public string? Images        { get; set; }
    public int?    Share         { get; set; }
}
public class EbookCollectionSearchRequest : SearchRequest { public long? ParentId { get; set; } }

// ==================== EBOOK: COLLECTION PERMISTION USER ====================
public class CollectionPermistionUserRequest
{
    public long?  CollectionId { get; set; }
    public long?  GroupUserId  { get; set; }
    public byte?  CanAdd       { get; set; }
    public byte?  CanEdit      { get; set; }
    public byte?  CanDelete    { get; set; }
    public byte?  CanView      { get; set; }
}
public class CollectionPermistionUserSearchRequest : SearchRequest
{
    public long? CollectionId { get; set; }
    public long? GroupUserId  { get; set; }
}

// ==================== EBOOK: DIG TYPE ====================
public class DigTypeRequest
{
    public string? Code          { get; set; }
    public string? DescriptionVn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? PortalId      { get; set; }
    public int?    SortOrder     { get; set; }
    public string? Language      { get; set; }
}
public class DigTypeSearchRequest : SearchRequest { }

// ==================== EBOOK: EBOOK ACCESS ====================
public class EbookAccessRequest
{
    public long?     ReaderId   { get; set; }
    public string?   Cardnumber { get; set; }
    public DateTime? Submited   { get; set; }
    public long?     Bookid     { get; set; }
    public int?      Page       { get; set; }
    public long?     Size       { get; set; }
    public int?      Type       { get; set; }
    public string?   Ip         { get; set; }
}
public class EbookAccessSearchRequest : SearchRequest
{
    public long? ReaderId { get; set; }
    public long? Bookid   { get; set; }
}

// ==================== EBOOK: EBOOK FILE ====================
public class EbookFileRequest
{
    public string?   Url               { get; set; }
    public string?      Type              { get; set; }
    public int?      IsConvert         { get; set; }
    public long?     EbookId           { get; set; }
    public DateTime? CreatedDate       { get; set; }
    public string?   FileType          { get; set; }
    public double?   FileSize          { get; set; }
    public string?   FileExt           { get; set; }
    public string?   Description       { get; set; }
    public string?   Source            { get; set; }
    public int?      FormatId          { get; set; }
    public string?   CheckSumAlgorithm { get; set; }
    public int?      SortOrder         { get; set; }
}
public class EbookFileSearchRequest : SearchRequest
{
    public long? EbookId       { get; set; }
    public Guid? EbookPublicId { get; set; }
}
public class EbookFileGetTokenRequest { public Guid PublicId { get; set; } }

// ==================== EBOOK: EBOOK LOG ====================
public class EbookLogRequest
{
    public long?     ReaderId   { get; set; }
    public string?   Cardnumber { get; set; }
    public DateTime? Submited   { get; set; }
    public long?     Bookid     { get; set; }
    public int?      Page       { get; set; }
    public long?   Size       { get; set; }
    public int?      Type       { get; set; }
    public string?   Ip         { get; set; }
}
public class EbookLogSearchRequest : SearchRequest
{
    public long? ReaderId { get; set; }
    public long? Bookid   { get; set; }
}

// ==================== EBOOK: REVIEW ====================
public class EbookReviewRequest
{
    public long?   ItemId      { get; set; }
    public int?    Rating      { get; set; }
    public string? DisplayName { get; set; }
    public string? Content     { get; set; }
    public string? Email       { get; set; }
    public int?    Status      { get; set; }
}

public class EbookReviewSearchRequest : SearchRequest
{
    public Guid? ItemId { get; set; }
    public int?  Rating { get; set; }
}

// ==================== EBOOK: INTRO BOOK CATEGORY ====================
public class IntroBookCategoryRequest
{
    public string? Name            { get; set; }
    public long?   ParentId        { get; set; }
    public int?    Level           { get; set; }
    public int?    Status          { get; set; }
    public int?    Order           { get; set; }
    public string? PortalId        { get; set; }
    public string? Language        { get; set; }
    public int?    IsLogin         { get; set; }
    public string? Description     { get; set; }
    public string? Keyword         { get; set; }
    public string? PageTitle       { get; set; }
    public string? MetaDescription { get; set; }
    public string? Link            { get; set; }
}
public class IntroBookCategorySearchRequest : SearchRequest { public long? ParentId { get; set; } }

// ==================== EBOOK: INTRO BOOKS ====================
public class IntroBooksRequest
{
    public string    Title               { get; set; } = string.Empty;
    public string?   Brief               { get; set; }
    public string?   Noidung             { get; set; }
    public string?   Image               { get; set; }
    public DateTime? Submited            { get; set; }
    public int?      Order               { get; set; }
    public long?     IntroBookCategoryId { get; set; }
    public long?     BibId               { get; set; }
}
public class IntroBooksSearchRequest : SearchRequest { public long? IntroBookCategoryId { get; set; } }

// ==================== EBOOK: ITEM (EbookItem) ====================
public class MetaDataEntryRequest
{
    public long    Id              { get; set; }   // 0 = insert, >0 = update
    public int?    MetaDataFieldId { get; set; }
    public string? Value           { get; set; }
    public int     SortOrder       { get; set; } = 1;
}

public class EbookItemRequest
{
    public long?     CollectionId  { get; set; }
    public string?   Images        { get; set; }
    public byte?     Status        { get; set; }
    public bool?     IsPublished   { get; set; }
    public long?     SubjectId     { get; set; }
    public long?     TypeId        { get; set; }
    public bool?     AllowDownload { get; set; }
    public bool?     IsFree        { get; set; }
    public long?     TopicId       { get; set; }
    public string?   PortalId      { get; set; }
    public string?   Language      { get; set; }
    public int?      Show          { get; set; }
    public int?      IndexContent  { get; set; }
    public int?      Share         { get; set; }
    public int?      PrintCopies   { get; set; }
    public int?      OfflineDays   { get; set; }
    public List<MetaDataEntryRequest> MetaDataEntries { get; set; } = [];
}
public class EbookItemSearchRequest : SearchRequest
{
    public long?     CollectionId    { get; set; }
    public long?     SubjectId       { get; set; }
    public long?     TypeId          { get; set; }
    public long?     TopicId         { get; set; }
    public string?   Title           { get; set; }
    public string?   Author          { get; set; }
    public string?   Publisher       { get; set; }
    public DateTime? SubmitedFrom    { get; set; }
    public DateTime? SubmitedTo      { get; set; }
    public string?   PublishDateFrom { get; set; }
    public string?   PublishDateTo   { get; set; }
}

public class EbookItemExportRequest : EbookItemSearchRequest
{
    public List<string>? Fields { get; set; }
}

public class EbookItemBulkMoveCollectionRequest
{
    public List<Guid> PublicIds   { get; set; } = [];
    public long        CollectionId { get; set; }
}

public class EbookItemAddRequest
{
    // EbookItem fields
    public long?   CollectionId  { get; set; }
    public string? Images        { get; set; }
    public byte?   Status        { get; set; }
    public bool?   IsPublished   { get; set; }
    public long?   SubjectId     { get; set; }
    public long?   TypeId        { get; set; }
    public bool?   AllowDownload { get; set; }
    public bool?   IsFree        { get; set; }
    public long?   TopicId       { get; set; }
    public string? PortalId      { get; set; }
    public string? Language      { get; set; }
    public int?    Show          { get; set; }
    public int?    IndexContent  { get; set; }
    public int?    Share         { get; set; }
    public int?    PrintCopies   { get; set; }
    public int?    OfflineDays   { get; set; }
    // Metadata fields
    public string?       Title       { get; set; }
    public List<string>? OtherTitles { get; set; }
    public List<string>? Authors     { get; set; }
    public List<string>? Advisors    { get; set; }
    public string?       Publisher   { get; set; }
    public string?       PublishDate { get; set; }
    public List<string>? Keywords    { get; set; }
    public string?       Citation    { get; set; }
    public List<string>? Series      { get; set; }
    public string?       Isbn        { get; set; }
    public string?       Issn        { get; set; }
    public string?       DocLanguage { get; set; }
    public string?       Abstract    { get; set; }
    public string?       Sponsor     { get; set; }
    public string?       Description { get; set; }
    public string?       Uri         { get; set; }
    public List<MetaDataValueInput>? ExtraMetadata { get; set; }
}

public class MetaDataValueInput
{
    public int    MetaDataFieldId { get; set; }
    public string Value           { get; set; } = "";
    public int    SortOrder       { get; set; } = 1;
}

// ==================== EBOOK: ITEM LOAN (EbookItemLoan) — mượn tài liệu số ====================
public class EbookItemLoanRequest
{
    // Loan không tạo/sửa tay qua Add/Update — chỉ khai báo cho khớp IGenericRepository<>.
}
public class EbookItemLoanSearchRequest : SearchRequest
{
    public long? EbookItemId { get; set; }
    public long? ReaderId    { get; set; }
    public int?  LoanStatus  { get; set; }
}
public class RecallLoanRequest
{
    public string? Reason { get; set; }
}
public class EbookItemReservationRequest
{
    // Reservation không tạo/sửa tay qua Add/Update — chỉ khai báo cho khớp IGenericRepository<>.
}
public class EbookItemReservationSearchRequest : SearchRequest
{
    public long? EbookItemId       { get; set; }
    public long? ReaderId          { get; set; }
    public int?  ReservationStatus { get; set; }
}

// ==================== DBO: SCHEDULED REPORT ====================
public class ScheduledReportRequest
{
    public string? Name             { get; set; }
    public string? ReportType       { get; set; }
    public string? ReportParamsJson { get; set; }
    public int     FrequencyType    { get; set; }
    public int?    DayOfWeek        { get; set; }
    public int?    DayOfMonth       { get; set; }
    public string? TimeOfDay        { get; set; }
    public string? RecipientEmails  { get; set; }
    public int?    Status           { get; set; }
}
public class ScheduledReportSearchRequest : SearchRequest { }

// ==================== EBOOK: ITEM XML (EbookItemXml) ====================
public class EbookItemXmlRequest
{
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishDate { get; set; }
    public string? Keyword     { get; set; }
    public string? Xml         { get; set; }
    public string? OtherTitle  { get; set; }
    public string? Page        { get; set; }
    public string? OldAuthor   { get; set; }
}
public class EbookItemXmlSearchRequest : SearchRequest { }

// ==================== EBOOK: META DATA FIELD REGISTERY ====================
public class MetaDataFieldRegisteryRequest
{
    public long?   MetaDataSchemaId { get; set; }
    public string? Field            { get; set; }
    public string? Subfield         { get; set; }
    public string? DescriptionVn    { get; set; }
    public string? DescriptionEn    { get; set; }
    public int?    SortOrder        { get; set; }
    public int?    Status           { get; set; }
    public string? Input            { get; set; }
    public int?    ExportField      { get; set; }
}
public class MetaDataFieldRegisterySearchRequest : SearchRequest { public long? MetaDataSchemaId { get; set; } }

// ==================== EBOOK: METADATA SCHEMA REGISTRY ====================
public class MetadataSchemaRegistryRequest
{
    public string? NameSpace { get; set; }
    public string? ShortId   { get; set; }
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
}
public class MetadataSchemaRegistrySearchRequest : SearchRequest { }

// ==================== EBOOK: META DATA VALUE ====================
public class MetaDataValueRequest
{
    public int?    MetaDataFieldId { get; set; }
    public string? Value           { get; set; }
    public string? Language        { get; set; }
    public long?   ItemId          { get; set; }
    public string? Value_UnSign    { get; set; }
    public int?    SortOrder       { get; set; }
}
public class MetaDataValueSearchRequest : SearchRequest
{
    public long? ItemId         { get; set; }
    public int?  MetaDataFieldId{ get; set; }
}

// ==================== EBOOK: POLICY DIGITAL ====================
public class PolicyDigitalRequest
{
    public int?    ReaderTypeid { get; set; }
    public int?    Maxpage      { get; set; }
    public double? Maxsize      { get; set; }
    public int?    Maxdocument  { get; set; }
}
public class PolicyDigitalSearchRequest : SearchRequest { }

// ==================== EBOOK: POLICY DIGITAL BY COLLECTION ====================
public class PolicyDigitalByCollectionRequest
{
    public int?    ReaderTypeid { get; set; }
    public int?    CollectionId { get; set; }
    public int?    Maxpage      { get; set; }
    public double? Maxsize      { get; set; }
    public int?    Maxdocument  { get; set; }
    public int?    Read         { get; set; }
    public int?    Comment      { get; set; }
    public int?    Download     { get; set; }
    public int?    OfflineDays  { get; set; }
}
public class PolicyDigitalByCollectionSearchRequest : SearchRequest { public int? CollectionId { get; set; } }

public class SavePermissionsRequest
{
    public Guid CollectionPublicId { get; set; }
    public List<PermissionItemRequest> Permissions { get; set; } = new();
}
public class PermissionItemRequest
{
    public int  ReaderTypeId { get; set; }
    public int  Read         { get; set; }
    public int  Download     { get; set; }
    public int  Maxdocument  { get; set; }
    public int? OfflineDays  { get; set; }
}

// ==================== EBOOK: SUBJECT (EbookSubject) ====================
public class EbookSubjectRequest
{
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public int?    Level    { get; set; }
    public int?    Status   { get; set; }
    public int?    Order    { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? DDC      { get; set; }
}
public class EbookSubjectSearchRequest : SearchRequest { public long? ParentId { get; set; } }

// ==================== EBOOK: THEODOI BIENMUC EBOOK ====================
public class TheodoiBienmucEbookRequest
{
    public int?    UserId   { get; set; }
    public DateTime? Submited { get; set; }
    public long?   DigId    { get; set; }
    public string? Status   { get; set; }
}
public class TheodoiBienmucEbookSearchRequest : SearchRequest
{
    public long? DigId  { get; set; }
    public int?  UserId { get; set; }
}

// ==================== EBOOK: TOPIC (EbookTopic) ====================
public class EbookTopicRequest
{
    public string? Name            { get; set; }
    public long?   ParentId        { get; set; }
    public int?    Level           { get; set; }
    public int?    Status          { get; set; }
    public int?    Order           { get; set; }
    public string? PortalId        { get; set; }
    public string? Language        { get; set; }
    public int?    IsLogin         { get; set; }
    public string? Description     { get; set; }
    public string? Keyword         { get; set; }
    public string? PageTitle       { get; set; }
    public string? MetaDescription { get; set; }
    public string? DDC             { get; set; }
}
public class EbookTopicSearchRequest : SearchRequest { public long? ParentId { get; set; } }

// ==================== EOFFICE: AGENCY ====================
public class AgencyRequest
{
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public int?    Level    { get; set; }
    public int?    Status   { get; set; }
    public int?    Order    { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public string? Link     { get; set; }
}
public class AgencySearchRequest : SearchRequest { public long? ParentId { get; set; } }

// ==================== EOFFICE: DOCUMENT ====================
public class DocumentRequest
{
    public string?   Name           { get; set; }
    public string?   Brief          { get; set; }
    public long?     DocumentTypeId { get; set; }
    public long?     AgencyId       { get; set; }
    public DateTime? IssueDate      { get; set; }
    public DateTime? ExpireDate     { get; set; }
    public DateTime? CreatedDate    { get; set; }
    public int?      TypeId         { get; set; }
    public int?      Status         { get; set; }
    public string?   Sign           { get; set; }
    public long?     TopicId        { get; set; }
    public string?   GovDocNumber   { get; set; }
    public string?   PortalId       { get; set; }
    public string?   Language       { get; set; }
}
public class DocumentSearchRequest : SearchRequest
{
    public long? DocumentTypeId { get; set; }
    public long? AgencyId       { get; set; }
    public long? TopicId        { get; set; }
}

// ==================== EOFFICE: DOCUMENT FILE ====================
public class DocumentFileRequest
{
    public string? Name       { get; set; }
    public string? Url        { get; set; }
    public double? FileSize   { get; set; }
    public long?   DocumentId { get; set; }
}
public class DocumentFileSearchRequest : SearchRequest { public long? DocumentId { get; set; } }

// ==================== EOFFICE: DOCUMENT TYPE ====================
public class DocumentTypeRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class DocumentTypeSearchRequest : SearchRequest { }

// ==================== EOFFICE: TOPIC (EofficeTopic) ====================
public class EofficeTopicRequest
{
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public int?    Level    { get; set; }
    public int?    Status   { get; set; }
    public int?    Order    { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class EofficeTopicSearchRequest : SearchRequest { public long? ParentId { get; set; } }

// ==================== EVALUATE: COURSE (EvaluateCourse) ====================
public class EvaluateCourseRequest
{
    public string? Code          { get; set; }
    public string? Name          { get; set; }
    public int?    Credit        { get; set; }
    public int?    Status        { get; set; }
    public long?   DegreeId      { get; set; }
    public long?   OptionCourseId{ get; set; }
    public string? KnowledgeId   { get; set; }
    public string? FileUrl       { get; set; }
}
public class EvaluateCourseSearchRequest : SearchRequest { public long? DegreeId { get; set; } }

// ==================== EVALUATE: COURSE OPTION ====================
public class CourseOptionRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class CourseOptionSearchRequest : SearchRequest { }

// ==================== EVALUATE: DEGREE (EvaluateDegree) ====================
public class EvaluateDegreeRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class EvaluateDegreeSearchRequest : SearchRequest { }

// ==================== EVALUATE: KNOWLEDGE ====================
public class KnowledgeRequest
{
    public string? Name     { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class KnowledgeSearchRequest : SearchRequest { }

// ==================== EVALUATE: MON HOC ====================
public class MonHocRequest
{
    public string? MaMon         { get; set; }
    public string? TenMon        { get; set; }
    public int?    SoTinChi      { get; set; }
    public long?   DegreeId      { get; set; }
    public long?   KnowledgeId   { get; set; }
    public int?    OptionId      { get; set; }
    public string? NguoiBienSoan { get; set; }
    public int?    Active        { get; set; }
    public string? Attachment    { get; set; }
    public string? Note          { get; set; }
}
public class MonHocSearchRequest : SearchRequest
{
    public long? DegreeId    { get; set; }
    public long? KnowledgeId { get; set; }
}
public class SubjectTreeSearchRequest
{
    public Guid? TenantId { get; set; }
}

// ==================== EVALUATE: NGANH HOC ====================
public class NganhHocRequest
{
    public string? MajorsName    { get; set; }
    public long?   ParentId      { get; set; }
    public long?   ProgramId     { get; set; }
    public long?   AmountStudent { get; set; }
    public int?    SortOrder     { get; set; }
    public string? PortalId      { get; set; }
    public string? Language      { get; set; }
    public string? MajorsCode    { get; set; }
    public int?    Status        { get; set; }
}
public class NganhHocSearchRequest : SearchRequest
{
    public long? ProgramId { get; set; }
    public long? ParentId  { get; set; }
}

// ==================== EVALUATE: NGANH MON HOC ====================
public class NganhMonHocRequest
{
    public long? MajorId  { get; set; }
    public long? MonHocId { get; set; }
}
public class NganhMonHocSearchRequest : SearchRequest
{
    public long? MajorId  { get; set; }
    public long? MonHocId { get; set; }
}

// ==================== EVALUATE: PROGRAM (EvaluateProgram) ====================
public class EvaluateProgramRequest
{
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public long?   DonViId     { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
}
public class EvaluateProgramSearchRequest : SearchRequest { public long? DonViId { get; set; } }

// ==================== EVALUATE: DON VI (Đơn vị đào tạo — Khoa/Viện/Trường, KHÁC Org phòng ban) ====================
public class DonViRequest
{
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public int?    Level    { get; set; }
    public int?    Status   { get; set; }
    public int?    Order    { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
}
public class DonViSearchRequest : SearchRequest { public long? ParentId { get; set; } }

// ==================== EVALUATE: TAI LIEU ====================
public class TaiLieuRequest
{
    public long?   BibId        { get; set; }
    public string? Title        { get; set; }
    public string? Author       { get; set; }
    public string? Publisher    { get; set; }
    public string? Url          { get; set; }
    public string? PublishDate  { get; set; }
    public int?    LoaiTaiLieu  { get; set; }
    public string? LanXuatBan   { get; set; }
    public string? Note         { get; set; }
    public long?   EBookId      { get; set; }
    public long?   MonHocId     { get; set; }
}
public class TaiLieuSearchRequest : SearchRequest { public long? MonHocId { get; set; } }

public class BulkSyncChunksRequest
{
    public bool ForceReindex { get; set; }
    public long? IdFrom { get; set; }
    public long? IdTo   { get; set; }
}

public class EbookChunkSearchRequest
{
    public string? Q            { get; set; }
    public int     Page         { get; set; } = 1;
    public int     PageSize     { get; set; } = 10;
    public string? CollectionId { get; set; }
    public string? TopicId      { get; set; }
    public string? SubjectId    { get; set; }
    public string? Language     { get; set; }
    public bool?   Free         { get; set; }
    public int?    FromYear     { get; set; }
    public int?    ToYear       { get; set; }
    public long?   TenantId     { get; set; }
}

public class PublicEbookElasticSearchRequest
{
    public string? Q               { get; set; }
    public int     Page            { get; set; } = 1;
    public int     PageSize        { get; set; } = 10;
    public string? Title           { get; set; }
    public string? Author          { get; set; }
    public string? Publisher       { get; set; }
    public string? Keyword         { get; set; }
    public string? CollectionId    { get; set; }
    public string? TopicId         { get; set; }
    public string? SubjectId       { get; set; }
    public string? Language        { get; set; }
    public bool?   Free            { get; set; }
    public bool?   Share           { get; set; }
    public string? PublishDateFrom { get; set; }
    public string? PublishDateTo   { get; set; }
    public Guid?   TenantId        { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public long?   ResolvedTenantId { get; set; }
    public string? DcType          { get; set; }
    public string? DcSubject       { get; set; }
}

// ==================== PRINTBOOK: CIRCULATION HISTORY ====================
public class LoanHistorySearchRequest
{
    public string?   CardNo         { get; set; }
    public string?   Title          { get; set; }
    public string?   Barcode        { get; set; }
    public long?     BibId          { get; set; }
    public DateTime? BorrowDateFrom { get; set; }
    public DateTime? BorrowDateTo   { get; set; }
    public int?      Status         { get; set; }
    public int?      CircPlaceId    { get; set; }
    public long?     ReaderTypeId   { get; set; }
    public long?     OrgId          { get; set; }
    /// <summary>Kho diễn ra giao dịch (BookOut.Store) — port ELIB-LRC 09-23.</summary>
    public long?     StoreId        { get; set; }
    public long?     ClassId        { get; set; }
    public long?     CourseId       { get; set; }
    /// <summary>Lọc theo người xử lý giao dịch (Đợt 14) — khớp BookOut.CreatedRowBy.</summary>
    public long?     OperatorId     { get; set; }
    public bool?     IsOverdue      { get; set; }
    public DateTime? DueDateFrom    { get; set; }
    public DateTime? DueDateTo      { get; set; }
    public DateTime? ReturnDateFrom { get; set; }
    public DateTime? ReturnDateTo   { get; set; }
    public int       PageIndex      { get; set; } = 1;
    public int       PageSize       { get; set; } = 20;
}

// ==================== PRINTBOOK: CIRCULATION REPORT ====================
public class CirculationReportRequest
{
    public int       ReportType   { get; set; }
    public DateTime? DateFrom     { get; set; }
    public DateTime? DateTo       { get; set; }
    public long?     ClassId      { get; set; }
    public long?     CourseId     { get; set; }
    public long?     OrgId        { get; set; }
    public long?     ReaderTypeId { get; set; }
    public int?      CircPlaceId  { get; set; }
    public int       PageIndex    { get; set; } = 1;
    public int       PageSize     { get; set; } = 20;
}

// ==================== PRINTBOOK: STORE BOOK REPORT ====================
public class StoreBookReportRequest
{
    public DateTime? DateFrom  { get; set; }
    public DateTime? DateTo    { get; set; }
    public int?       StoreId  { get; set; }
    public int        PageIndex { get; set; } = 1;
    public int        PageSize  { get; set; } = 20;
}