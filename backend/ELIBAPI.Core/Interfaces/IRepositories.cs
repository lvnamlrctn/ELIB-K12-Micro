using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Entities.PrintBook;


namespace ELIBAPI.Core.Interfaces;

public interface IGenericRepository<TEntity, TSearch, TRequest>
    where TEntity : class
    where TSearch : SearchRequest
    where TRequest : class
{
    Task<TEntity?> GetByIdAsync(long id);
    Task<TEntity?> GetByPublicIdAsync(Guid publicId);
    Task<PagedResult<TEntity>> SearchAsync(TSearch request);
    Task<List<TEntity>> SearchAllAsync(TSearch request);
    Task<TEntity> AddAsync(TRequest request);
    Task AddRangeAsync(IEnumerable<TRequest> requests);
    Task<TEntity> UpdateAsync(Guid publicId, TRequest request);
    Task DeleteAsync(Guid publicId);
    Task ChangeStatusAsync(ChangeStatusRequest request);
}

public interface IPhotoAlbumRepository : IGenericRepository<PhotoAlbum, PhotoAlbumSearchRequest, PhotoAlbumRequest>
{
    Task ChangeIsSpecialAsync(ChangeIsSpecialRequest request);
}

public interface IMenuTypeRepository : IGenericRepository<MenuType, MenuTypeSearchRequest, MenuTypeRequest>
{
    Task<bool> CheckCodeExistsAsync(string code, Guid? excludePublicId = null);
}

public interface IOrgRepository : IGenericRepository<Org, OrgSearchRequest, OrgRequest>
{
    Task<List<OrgTreeResponse>> GetTreeAsync(OrgSearchRequest request);
    Task UpdateOrderAsync(Guid publicId, int newOrder);
    Task<Org> MoveAsync(Guid publicId, long? newParentId, int newOrder);
    Task DeleteWithChildrenAsync(Guid publicId);
}

public interface IDonViRepository : IGenericRepository<DonVi, DonViSearchRequest, DonViRequest>
{
    Task<List<DonViTreeResponse>> GetTreeAsync(DonViSearchRequest request);
    Task UpdateOrderAsync(Guid publicId, int newOrder);
    Task<DonVi> MoveAsync(Guid publicId, long? newParentId, int newOrder);
    Task DeleteWithChildrenAsync(Guid publicId);
}

public interface IModuleRepository : IGenericRepository<Module, ModuleSearchRequest, ModuleRequest>
{
    Task<List<ModuleTreeResponse>> GetTreeAsync(ModuleSearchRequest request);
    Task UpdateOrderAsync(Guid publicId, int newOrder);
    Task<Module> MoveAsync(Guid publicId, long? newParentId, int newOrder);
    Task DeleteWithChildrenAsync(Guid publicId);
}

public interface IMenuRepository : IGenericRepository<Menu, MenuSearchRequest, MenuRequest>
{
    Task<List<MenuTreeResponse>> GetTreeAsync(MenuSearchRequest request);
}

public interface ICategoryRepository : IGenericRepository<Category, CategorySearchRequest, CategoryRequest>
{
    Task<List<CategoryTreeResponse>> GetTreeAsync(CategorySearchRequest request);
    Task UpdateOrderAsync(Guid publicId, int newOrder);
    Task<Category> MoveAsync(Guid publicId, long? newParentId, int newOrder);
    Task DeleteWithChildrenAsync(Guid publicId);
}

public interface IEbookCollectionRepository : IGenericRepository<EbookCollection, EbookCollectionSearchRequest, EbookCollectionRequest>
{
    Task<List<EbookCollectionTreeResponse>> GetTreeAsync(EbookCollectionSearchRequest request);
    Task UpdateOrderAsync(Guid publicId, int newOrder);
    Task<EbookCollection> MoveAsync(Guid publicId, long? newParentId, int newOrder);
    Task DeleteWithChildrenAsync(Guid publicId);
}

public interface IEbookReviewRepository
    : IGenericRepository<EbookReview, EbookReviewSearchRequest, EbookReviewRequest>
{
    Task<PagedResult<EbookReviewResponse>> SearchWithTitleAsync(EbookReviewSearchRequest request);
    Task<List<EbookReviewResponse>> SearchAllWithTitleAsync(EbookReviewSearchRequest request);
}

public interface IPolicyDigitalRepository
    : IGenericRepository<PolicyDigital, PolicyDigitalSearchRequest, PolicyDigitalRequest>
{
    Task<PagedResult<PolicyDigitalResponse>> SearchWithNameAsync(PolicyDigitalSearchRequest request);
    Task<List<PolicyDigitalResponse>> SearchAllWithNameAsync(PolicyDigitalSearchRequest request);
    Task<PolicyDigital> UpsertAsync(PolicyDigitalRequest request);
}

public interface IPolicyDigitalByCollectionRepository
    : IGenericRepository<PolicyDigitalByCollection, PolicyDigitalByCollectionSearchRequest, PolicyDigitalByCollectionRequest>
{
    Task<List<PolicyDigitalByCollection>> GetByCollectionPublicIdAsync(Guid collectionPublicId);
    Task SaveByCollectionPublicIdAsync(Guid collectionPublicId, List<PermissionItemRequest> permissions);
}

public interface IEbookSubjectRepository : IGenericRepository<EbookSubject, EbookSubjectSearchRequest, EbookSubjectRequest>
{
    Task<List<EbookSubjectTreeResponse>> GetTreeAsync(EbookSubjectSearchRequest request);
    Task UpdateOrderAsync(Guid publicId, int newOrder);
    Task<EbookSubject> MoveAsync(Guid publicId, long? newParentId, int newOrder);
    Task DeleteWithChildrenAsync(Guid publicId);
}

public interface IEbookTopicRepository : IGenericRepository<EbookTopic, EbookTopicSearchRequest, EbookTopicRequest>
{
    Task<List<EbookTopicTreeResponse>> GetTreeAsync(EbookTopicSearchRequest request);
    Task UpdateOrderAsync(Guid publicId, int newOrder);
    Task<EbookTopic> MoveAsync(Guid publicId, long? newParentId, int newOrder);
    Task DeleteWithChildrenAsync(Guid publicId);
}

public interface INewsRepository
    : IGenericRepository<News, NewsSearchRequest, NewsRequest>
{
    Task<List<News>> GetNewsWithOldImagesAsync(string minioPublicBaseUrl);
    Task<bool> UpdateNewsImagesAsync(long id, string? images, string? thumb);
}

public interface IEbookItemRepository
    : IGenericRepository<EbookItem, EbookItemSearchRequest, EbookItemRequest>
{
    Task<EbookItem> AddEbookAsync(EbookItemAddRequest request);
    Task<EbookItem> UpdateEbookAsync(Guid publicId, EbookItemRequest request);
    Task<List<EbookMetaDataExportRow>> ExportMetaDataAsync(EbookItemSearchRequest request);
    Task<List<DSpaceItemExportRow>> ExportDSpaceDataAsync(EbookItemSearchRequest request, bool includeFiles);
    Task SyncElasticAsync(Guid publicId);
    Task<int> BulkSyncElasticAsync();
    Task<BulkSyncChunksResult> BulkSyncChunksAsync(BulkSyncChunksRequest request);
    Task<EbookItemDetailResponse?> GetDetailAsync(Guid publicId);
    Task<List<EbookItem>> GetItemsWithOldImagesAsync(string minioPublicBaseUrl);
    Task<bool> UpdateItemImagesAsync(long id, string? images);
    Task<int> BulkMoveCollectionAsync(EbookItemBulkMoveCollectionRequest request);
}

public interface IStoreTypeRepository : IGenericRepository<StoreType, StoreTypeSearchRequest, StoreTypeRequest>
{
    Task<List<StoreTypeTreeResponse>> GetTreeAsync(StoreTypeSearchRequest request);
}

public interface IEbookFileRepository
    : IGenericRepository<EbookFile, EbookFileSearchRequest, EbookFileRequest>
{
    Task<List<EbookFile>> GetFilesWithOldUrlsAsync();
    Task<bool> UpdateFileUrlAsync(long id, string newObjectName);
}

public interface IEbookItemLoanRepository
    : IGenericRepository<EbookItemLoan, EbookItemLoanSearchRequest, EbookItemLoanRequest>
{
    Task<(bool Ok, string? Error, int StatusCode, EbookItemLoan? Loan)> CheckoutOrResumeAsync(long ebookItemId, long readerId);
    Task<bool> ReturnAsync(Guid loanPublicId, long readerId);
    Task<(bool Valid, string? Error, int StatusCode)> ValidateAccessAsync(long ebookItemId, long readerId);
    Task<bool> RecallAsync(Guid loanPublicId, long recalledByUserId, string? reason);
    Task<int>  RecallAllActiveForItemAsync(long ebookItemId, long recalledByUserId, string? reason);
}

public interface IEbookItemReservationRepository
    : IGenericRepository<EbookItemReservation, EbookItemReservationSearchRequest, EbookItemReservationRequest>
{
    Task<(bool Ok, string? Error, int StatusCode, EbookItemReservation? Reservation)> ReserveAsync(long ebookItemId, long readerId);
    Task<bool> CancelReservationAsync(Guid reservationPublicId, long readerId);
    Task<int>  PromoteNextIfSlotAvailableAsync(long ebookItemId);
}

// Đặt phòng học nhóm — nghiệp vụ viết tay bổ sung (overlap check, duyệt/từ chối, check-in, hủy) ngoài
// CRUD chung IGenericRepository. Mọi tham số readerId/tenantId cho các thao tác phía OPAC (không có JWT
// tenant claim) đều nhận tường minh từ controller, đã resolve từ Reader.TenantId — xem
// Public/Map/RoomBookingController.
public interface IRoomBookingRepository
    : IGenericRepository<RoomBooking, RoomBookingSearchRequest, RoomBookingRequest>
{
    Task<(bool Ok, string? Error, int StatusCode, RoomBooking? Booking)> CreateBookingAsync(CreateRoomBookingRequest request, long readerId, long? tenantId);
    Task<(bool Ok, string? Error)> ApproveAsync(Guid publicId, long staffUserId);
    Task<(bool Ok, string? Error)> RejectAsync(Guid publicId, long staffUserId, string? reason);
    /// Bạn đọc (người đặt hoặc thành viên nhóm) tự check-in — trong khung từ 15 phút trước giờ bắt đầu tới hết ân hạn của phòng.
    Task<(bool Ok, string? Error)> CheckInAsync(Guid publicId, long readerId, long? tenantId);
    /// Thủ thư/kiosk quét mã QR (lọc đơn vị theo JWT); mapObjectId = phòng đặt kiosk (null = mọi phòng).
    Task<(bool Ok, string? Error, RoomBooking? Booking)> StaffCheckInAsync(Guid publicId, long? mapObjectId, long staffUserId);
    /// Kiosk khuôn mặt: bạn đọc (người đặt + thành viên) có lượt đã duyệt đang trong khung check-in, đúng phòng kiosk nếu có
    /// (lọc đơn vị theo JWT) — chỉ so ảnh camera với những người này.
    Task<List<long>> FaceCheckInCandidatesAsync(long? mapObjectId);
    /// Kiosk khuôn mặt: check-in lượt sớm nhất đang trong khung của bạn đọc đã nhận diện (ưu tiên đúng phòng kiosk).
    Task<(bool Ok, string? Error, RoomBooking? Booking)> StaffCheckInByReaderAsync(long readerId, long? mapObjectId, long staffUserId);
    /// Bạn đọc tự check-in bằng khuôn mặt: kiểm tra trạng thái/giờ TRƯỚC khi gọi AI. null = được check-in.
    Task<string?> CheckInPrecheckAsync(Guid publicId, long readerId, long? tenantId);
    /// null = huỷ được; ngược lại là lý do (chỉ người đặt, trước giờ bắt đầu).
    Task<string?> CancelAsync(Guid publicId, long readerId, long? tenantId);
    /// Trả phòng (kết thúc sớm) lượt đang sử dụng — bạn đọc (readerId + tenantId) hoặc thủ thư (staffUserId, lọc JWT).
    Task<(bool Ok, string? Error, RoomBooking? Booking)> CheckOutAsync(Guid publicId, long? readerId, long? staffUserId, long? tenantId = null);
    /// Dòng xuất Excel theo bộ lọc màn danh sách (đã lọc đơn vị như Search), tối đa max dòng.
    Task<(List<RoomBooking> Rows, int Total)> ExportRowsAsync(RoomBookingSearchRequest request, int max);
}

public interface IReaderRepository
    : IGenericRepository<Reader, ReaderSearchRequest, ReaderRequest>
{
    Task<int> BulkUpdateAsync(ReaderBulkUpdateRequest request);
    Task<Dictionary<long, string>> GetClassMapAsync(IEnumerable<long> ids);
    Task<Dictionary<long, string>> GetCourseMapAsync(IEnumerable<long> ids);
    Task<Dictionary<long, string>> GetOrgMapAsync(IEnumerable<long> ids);
    Task<ReaderImportResult> ImportAsync(List<ReaderImportRow> rows, string? portalId, string? language, bool overwrite = false, long? readerTypeId = null, bool classByCode = false, bool courseByCode = false, bool orgByCode = false, bool previewOnly = false, bool autoCreateRefs = false);
    /// UID thẻ chip đã gán cho bạn đọc khác cùng đơn vị (UID chỉ cần duy nhất trong 1 đơn vị).
    Task<bool> CardUidTakenAsync(string cardUid, long? tenantId, Guid? excludePublicId = null);
    Task<PagedResult<ReaderResponse>> SearchWithNamesAsync(ReaderSearchRequest request);
    Task<List<ReaderResponse>> SearchAllWithNamesAsync(ReaderSearchRequest request);
    Task<int> BatchUpdateAsync(ReaderBatchUpdateRequest request);
    Task<bool> CheckCardnoExistsAsync(string cardno, Guid? excludePublicId = null);
    Task ResetPasswordAsync(Guid publicId, string hashedPassword, long userId);
    Task<int> BulkResetPasswordAsync(List<Guid> publicIds, string hashedPassword, long userId);
    Task LockAsync(Guid publicId, string? reason, long? userId);
}

public interface IUsersRepository
    : IGenericRepository<Users, UsersSearchRequest, UsersRequest>
{
    Task ResetPasswordAsync(Guid publicId, string hashedPassword, long userId);
    Task ChangePasswordAsync(long userId, string oldPlainPassword, string newPlainPassword);
    Task<List<UserPermissionResponse>> GetPermissionsAsync(Guid userPublicId);
    Task SavePermissionsAsync(SavePermissionRequest request, long editorUserId);
    Task<bool> CheckLoginNameExistsAsync(string loginName, Guid? excludePublicId = null);
}

public interface IUserLogRepository
{
    Task<PagedResult<UserLogResponse>> SearchAsync(UserLogSearchRequest request, long? jwtTenantId, bool isPrivileged);
    Task<List<UserLogResponse>> SearchAllAsync(UserLogSearchRequest request, long? jwtTenantId, bool isPrivileged);
}

public interface IPublicGenericRepository<TEntity, TSearch>
    where TEntity : class
    where TSearch : PublicSearchRequest
{
    /// <summary>Đợt 22.7 — vá lỗ hổng đọc chéo đơn vị: <paramref name="tenantId"/> (PublicId của Tenant,
    /// Guid.Empty = không lọc) được controller truyền vào từ tham số route/query cùng tên "TenantId" mà
    /// <see cref="ELIBAPI.API.Filters.PublicHostTenantFilter"/> đã tự gán theo Host thật. Entity không có
    /// cột TenantId (vd Tenant) thì tham số này không có tác dụng gì — an toàn để giữ mặc định.</summary>
    Task<TEntity?> GetByPublicIdAsync(Guid publicId, Guid tenantId = default);
    Task<PagedResult<TEntity>> SearchAsync(TSearch request);
    Task<List<TEntity>> SearchAllAsync(TSearch request);
}

public record LoginResult<T>(bool Success, T? Data, string? ErrorMessage, int StatusCode = 200);

public interface IAuthService
{
    Task<LoginResult<LoginResponse>> LoginAsync(LoginRequest request);

    /// Xác thực OTP (lớp bảo mật thứ 2, sau khi mật khẩu đã đúng) và phát hành token đăng nhập thật —
    /// mirror IReaderAuthService.VerifyOtpAsync cho luồng đăng nhập admin.
    Task<LoginResult<LoginResponse>> VerifyOtpAsync(VerifyOtpRequest request);
}

public interface IReaderAuthService
{
    Task<LoginResult<ReaderLoginResponse>> LoginAsync(ReaderLoginRequest request);

    /// Xác thực OTP (lớp bảo mật thứ 2, sau khi mật khẩu đã đúng) và phát hành token đăng nhập thật.
    Task<LoginResult<ReaderLoginResponse>> VerifyOtpAsync(VerifyOtpRequest request);
}

public interface IAttachFileRepository
    : IGenericRepository<ELIBAPI.Core.Entities.Cms.AttachFile, AttachFileSearchRequest, AttachFileRequest>
{
    /// <summary>Dòng chưa xoá còn trỏ đường dẫn của hệ thống cũ (Url bắt đầu "Upload"); <paramref name="tenantId"/> null = mọi đơn vị.</summary>
    Task<List<ELIBAPI.Core.Entities.Cms.AttachFile>> GetLegacyAsync(long? tenantId);
    Task<bool> UpdateUrlAsync(long id, string newObjectName);
}

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(long userId, string moduleCode, string action);

    /// <summary>Mã quyền → các action được phép (view/add/edit/delete/access) của user — dựng claim JWT "perm" lúc đăng
    /// nhập. Bỏ qua mã có nhiều dòng module hoặc nhiều dòng quyền (kết quả HasPermissionAsync không xác định) để các mã
    /// đó luôn đi đường DB.</summary>
    Task<Dictionary<string, List<string>>> GetUserPermissionsAsync(long userId);
    /// <summary>Có thuộc nhóm role toàn quyền (ReadOnlyPolicy:AdminRoleCodes) không.</summary>
    Task<bool> IsAdminRoleAsync(long userId);
    /// <summary>Có bị chặn ghi (tài khoản hệ thống không đơn vị, đơn vị/role chỉ-xem) không.</summary>
    Task<bool> IsReadOnlyUserAsync(long userId);
    /// <summary>PermissionStamp hiện tại của user (1 query theo khoá chính, không join) — phát hiện claim JWT đã cũ.</summary>
    Task<string?> GetPermissionStampAsync(long userId);
}

public class LoginRequest
{
    public string LoginName { get; set; } = "";
    public string Password  { get; set; } = "";
    public Guid?  TenantId  { get; set; }
    // CaptchaId/CaptchaAnswer chỉ bắt buộc khi tenant bật ADMIN_LOGIN_CAPTCHA_ENABLED — optional để không
    // phá vỡ client cũ / tenant không bật CAPTCHA. Mirror ReaderLoginRequest.
    public string? CaptchaId     { get; set; }
    public string? CaptchaAnswer { get; set; }
}
// CaptchaId/CaptchaAnswer chỉ bắt buộc khi tenant bật READER_LOGIN_CAPTCHA_ENABLED — optional để không
// phá vỡ client cũ / tenant không bật CAPTCHA.
public record ReaderLoginRequest(string LoginName, string Password, Guid TenantId, string? CaptchaId = null, string? CaptchaAnswer = null);

// OtpSessionToken lấy từ ReaderLoginResponse.OtpSessionToken (bước 1), Code là mã OTP 6 số nhận qua email.
public record VerifyOtpRequest(string OtpSessionToken, string Code);
// Khi OtpRequired=true: Token rỗng (chưa đăng nhập thật), FE phải gọi AuthController.VerifyOtp với
// OtpSessionToken để lấy token thật. Khi OtpRequired=false (mặc định — flag tắt hoặc không cần lớp 2):
// hành vi y hệt trước khi có tính năng CAPTCHA/OTP — Token có giá trị ngay. Mirror ReaderLoginResponse.
public record LoginResponse(
    string Token,
    string FullName,
    string LoginName,
    long Id,
    Guid PublicId,
    long? TenantId,
    int? RoleId,
    bool IsPrivileged,
    DateTime Expiration,
    bool OtpRequired = false,
    string? OtpSessionToken = null);
// Khi OtpRequired=true: Token rỗng (chưa đăng nhập thật), FE phải gọi VerifyOtp với OtpSessionToken để
// lấy token thật. Khi OtpRequired=false (mặc định — flag tắt hoặc không cần lớp 2): hành vi y hệt trước
// khi có tính năng CAPTCHA/OTP — Token có giá trị ngay.
public record ReaderLoginResponse(
    string Token,
    string FullName,
    string Cardno,
    Guid UserId,
    Guid PublicId,
    Guid? TenantId,
    DateTime Expiration,
    bool OtpRequired = false,
    string? OtpSessionToken = null);
