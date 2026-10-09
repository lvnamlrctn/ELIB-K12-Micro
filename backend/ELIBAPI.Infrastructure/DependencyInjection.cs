using ELIBAPI.Core.Common;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.SqlServer;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Core.Entities.Gamification;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Repositories;
using ELIBAPI.Infrastructure.Services;
using ELIBAPI.Infrastructure.Mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AutoMapper;

namespace ELIBAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── DbContext ─────────────────────────────────────────────────────────
        var dbProvider = configuration["DatabaseProvider"] ?? "SqlServer";
        void ConfigureDbOptions(DbContextOptionsBuilder options)
        {
            if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                var pgConnStr = AesEncryptionHelper.DecryptConnectionString(
                    configuration.GetConnectionString("PostgreSQL") ?? "");
                options.UseNpgsql(pgConnStr);
            }
            else
            {
                var sqlConnStr = AesEncryptionHelper.DecryptConnectionString(
                    configuration.GetConnectionString("SqlServer") ?? "");
                if (!sqlConnStr.Contains("Encrypt", StringComparison.OrdinalIgnoreCase))
                    sqlConnStr = sqlConnStr.TrimEnd(';') + ";Encrypt=False;";
                options.UseSqlServer(sqlConnStr);
            }
        }
        services.AddDbContext<ELIBAPIDbContext>(ConfigureDbOptions);
        // Cho phép tự tạo DbContext riêng trong các Task chạy song song (vd Dashboard) — 1 DbContext
        // không thread-safe nếu dùng chung giữa nhiều Task.WhenAll cùng lúc.
        services.AddDbContextFactory<ELIBAPIDbContext>(ConfigureDbOptions, ServiceLifetime.Scoped);

        // ── AutoMapper ────────────────────────────────────────────────────────
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<EbookMappingProfile>();
        });

        // ── HttpContext (cho BaseRepository đọc JWT claims) ───────────────────
        services.AddHttpContextAccessor();
        services.AddHttpClient();

        // ── Services ──────────────────────────────────────────────────────────
        services.AddScoped<IAuthService,       AuthService>();
        services.AddScoped<IReaderAuthService, ReaderAuthService>();
        // Singleton: không giữ state riêng ngoài IMemoryCache (đã thread-safe) — không cần Scoped.
        services.AddSingleton<ICaptchaService, CaptchaService>();
        // OTP dùng chung cho đăng nhập bạn đọc + admin — cũng không giữ state riêng ngoài IMemoryCache.
        services.AddSingleton<IOtpService,     OtpService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ISystemParameterService, SystemParameterService>();
        services.AddScoped<IMinioService,         MinioService>();
        services.AddScoped<IFineTicketService,    FineTicketService>();
        services.AddScoped<ILostBookService,      LostBookService>();
        services.AddScoped<ILiquidateService,     LiquidateService>();
        services.AddScoped<IStoreMoveService,     StoreMoveService>();
        services.AddScoped<ISerialIssueService,   SerialIssueService>();
        services.AddScoped<ISerialBindingService, SerialBindingService>();
        services.AddScoped<ISerialReportService,  SerialReportService>();
        services.AddScoped<IElasticsearchService, ElasticsearchService>();
        services.AddScoped<IOaiPmhService,        OaiPmhService>();
        services.Configure<OaiPmhOptions>(configuration.GetSection("Oai"));
        services.AddScoped<IPdfExtractorService,      PdfExtractorService>();
        services.AddSingleton<IBookImageAnalyzerService, BookImageAnalyzerService>();
        services.AddSingleton<IOcrService, OcrService>();
        services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
        // Gemini (port ELIB-LRC 09-29): 1 named client dùng chung → tái sử dụng kết nối TLS giữa các lượt chat (trước đây mỗi
        // lượt tạo HttpClient mới và TẮT kiểm tra chứng chỉ). Chỉ bỏ kiểm tra khi LLMSettings:SkipCertificateValidation=true
        // (máy chủ đứng sau proxy chặn TLS).
        services.AddHttpClient(GeminiClient.HttpClientName, c =>
        {
            c.BaseAddress = new Uri(configuration["LLMSettings:BaseUrl"] ?? "https://generativelanguage.googleapis.com");
            c.Timeout = TimeSpan.FromSeconds(int.TryParse(configuration["LLMSettings:TimeoutSeconds"], out var t) ? t : 60);
        }).ConfigurePrimaryHttpMessageHandler(() => configuration.GetValue<bool>("LLMSettings:SkipCertificateValidation")
            ? new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }
            : new HttpClientHandler());
        services.AddSingleton<IGeminiClient, GeminiClient>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IDocumentFinderService, DocumentFinderService>();
        services.AddScoped<IAdminStatChatService, AdminStatChatService>();
        services.AddScoped<CirculationReportBuilder>();
        services.AddScoped<StoreBookReportBuilder>();
        services.AddScoped<IGenericRepository<ScheduledReport, ScheduledReportSearchRequest, ScheduledReportRequest>, ScheduledReportRepository>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.EbookIndexingJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.PrintBookIndexingJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.DigitalEnrichmentJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.PurgeDeletedRecordsJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.EmbeddingReindexJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.ScheduledReportEmailJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.EbookLoanExpiryJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.BookRequestExpiryJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.DueSoonReminderJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.DigitalUnifiedIndexingJob>();
        // Đặt phòng nâng cao (port ELIB-LRC 10-04): thông báo, danh sách chặn, cấu hình, kiểm soát cửa, báo cáo, trang OPAC.
        services.AddScoped<RoomBookingNotifier>();
        // Xác thực bạn đọc qua LDAP / API ngoài theo từng đơn vị (port ELIB-LRC 10-04).
        services.AddScoped<ExternalReaderAuth>();
        services.AddScoped<RoomBookingBanService>();
        services.AddScoped<RoomBookingAdminService>();
        services.AddScoped<AccessControlService>();
        services.AddScoped<RoomBookingReportService>();
        services.AddScoped<IRoomBookingPortalService, RoomBookingPortalService>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.RoomBookingExpiryJob>();
        // Thanh toán QR VietQR/VNPAY (port ELIB-LRC 09-25) — appsettings là mặc định, đơn vị ghi đè bằng SystemParameter PAYMENT_*.
        services.Configure<ELIBAPI.Core.Common.VnPayOptions>(configuration.GetSection("VnPay"));
        services.Configure<ELIBAPI.Core.Common.VietQrOptions>(configuration.GetSection("VietQr"));
        services.Configure<ELIBAPI.Core.Common.SepayOptions>(configuration.GetSection("Sepay"));
        services.Configure<ELIBAPI.Core.Common.PaymentOptions>(configuration.GetSection("Payment"));
        services.AddScoped<IFineSettlementService, FineSettlementService>();
        services.AddScoped<ELIBAPI.Infrastructure.Services.Payment.PaymentConfig>();
        services.AddScoped<IPaymentGatewayService, ELIBAPI.Infrastructure.Services.Payment.VietQrGatewayService>();
        services.AddScoped<IPaymentGatewayService, ELIBAPI.Infrastructure.Services.Payment.VnPayGatewayService>();
        services.AddScoped<IPaymentGatewayResolver, ELIBAPI.Infrastructure.Services.Payment.PaymentGatewayResolver>();
        services.AddScoped<IPaymentService, ELIBAPI.Infrastructure.Services.Payment.PaymentService>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.PaymentExpiryJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.BadgeEvaluationJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.SavedSearchAlertJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.SearchObservationPurgeJob>();
        // Đợt 26 — 3 job dưới đây bị bỏ sót khi port ở các đợt trước: Hangfire activate job qua
        // GetRequiredService(<type>) nên job KHÔNG đăng ký ở đây sẽ ném lỗi lúc chạy, dù recurring job
        // vẫn hiện trong dashboard. Cả 3 đều có constructor cần DI nên chắc chắn lỗi, không phải rủi ro
        // lý thuyết — kiểm chứng bằng cách chạy thật ZebraExportJob sau khi đăng ký.
        //   - ZebraExportJob        (Đợt 22.3): recurring "zebra-export-nightly-rebuild" + enqueue theo
        //                                       từng biểu ghi ở CatalogueBookController
        //   - DigitalStorageAuditJob(Đợt 22.4): recurring "digital-storage-audit"
        //   - AdminTaskRetentionJob (Đợt 13)  : recurring "admin-task-retention-sweep"
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.ZebraExportJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.DigitalStorageAuditJob>();
        services.AddScoped<ELIBAPI.Infrastructure.Jobs.AdminTaskRetentionJob>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEbookReservationReadyNotifier, EbookReservationReadyNotifier>();
        // SMS + Zalo ZNS — hạ tầng thông báo dùng chung cho hold sách, đến hạn/quá hạn, ebook sắp hết
        // hạn/có sẵn để mượn, đạt huy hiệu, duyệt/từ chối đặt phòng học nhóm (đợt 6).
        services.AddScoped<ISmsService,               SmsService>();
        services.AddScoped<IZaloZnsService,           ZaloZnsService>();
        services.AddScoped<INotificationDispatcher,   NotificationDispatcher>();
        services.AddScoped<IGenericRepository<NotificationChannelConfig, NotificationChannelConfigSearchRequest, NotificationChannelConfigRequest>, NotificationChannelConfigRepository>();
        services.AddScoped<IGenericRepository<NotificationLog,           NotificationLogSearchRequest,           NotificationLogRequest>,           NotificationLogRepository>();
        services.AddScoped<IFaceRecognitionService, FaceRecognitionService>();
        services.AddScoped<IBookCoverLookupService, BookCoverLookupService>();
        // Đợt 9 — circuit-breaker ES→DB cho tìm kiếm OPAC + thống kê chất lượng tìm kiếm. Breaker phải
        // Singleton để state (số lần lỗi liên tiếp, thời điểm mở) sống xuyên suốt tiến trình API, không
        // reset theo từng scope request.
        services.AddSingleton<SearchCircuitBreaker>();
        services.AddScoped<OpacSearchFailoverService>();
        services.AddScoped<SearchQualityService>();
        services.AddScoped<ReaderWorkspaceService>();
        // Đợt 10 — nền tảng tác vụ nền (AdminTask v2). Crypto Singleton (khoá dẫn xuất 1 lần, không đổi
        // trong vòng đời app); Service/Health Scoped (dùng ELIBAPIDbContext); Worker là BackgroundService
        // riêng (không qua Hangfire — xem AdminTaskWorker), poll liên tục nên đăng ký AddHostedService.
        services.AddSingleton<AdminTaskCrypto>();
        services.AddScoped<AdminTaskService>();
        services.AddScoped<AdminTaskHealth>();
        // Đợt 22.5 — dùng chung giữa thao tác đơn lẻ (controller) và tác vụ nền hàng loạt (AdminTaskService).
        services.AddScoped<BarcodeReRegisterService>();
        services.AddScoped<InventoryImportService>();
        services.AddHostedService<ELIBAPI.Infrastructure.Jobs.AdminTaskWorker>();
        // Đợt 13 — giám sát tác vụ người khác + dọn payload/result cũ, Scoped như trên (dùng ELIBAPIDbContext).
        services.AddScoped<AdminTaskMonitoringService>();
        services.AddScoped<AdminTaskControlService>();
        services.AddScoped<AdminTaskRetentionService>();
        // Huy hiệu đọc (Gamification) — CRUD danh mục thường, giống BibType.
        services.AddScoped<IGenericRepository<Badge, BadgeSearchRequest, BadgeRequest>, BadgeRepository>();

        // ── Internal Repositories: cms (existing) ─────────────────────────────
        services.AddScoped<INewsRepository, NewsRepository>();
        services.AddScoped<IGenericRepository<News, NewsSearchRequest, NewsRequest>>(
            sp => sp.GetRequiredService<INewsRepository>());
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IMenuTypeRepository, MenuTypeRepository>();
        services.AddScoped<IModuleRepository, ModuleRepository>();
        services.AddScoped<IGenericRepository<Module,      ModuleSearchRequest,      ModuleRequest>>(sp => sp.GetRequiredService<IModuleRepository>());
        services.AddScoped<IGenericRepository<ModuleRoles, ModuleRolesSearchRequest, ModuleRolesRequest>, ModuleRolesRepository>();
        services.AddScoped<IGenericRepository<Permission,  PermissionSearchRequest,  PermissionRequest>,  PermissionRepository>();
        services.AddScoped<IGenericRepository<Photo,       PhotoSearchRequest,       PhotoRequest>,       PhotoRepository>();
        services.AddScoped<IPhotoAlbumRepository, PhotoAlbumRepository>();
        services.AddScoped<IGenericRepository<PhotoAlbum,  PhotoAlbumSearchRequest,  PhotoAlbumRequest>>(sp => sp.GetRequiredService<IPhotoAlbumRepository>());
        services.AddScoped<IAttachFileRepository, AttachFileRepository>();
        services.AddScoped<IGenericRepository<AttachFile,  AttachFileSearchRequest,  AttachFileRequest>>(sp => sp.GetRequiredService<IAttachFileRepository>());
        services.AddScoped<IAttachFileService, AttachFileService>();
        services.AddScoped<IGenericRepository<Roles,       RolesSearchRequest,       RolesRequest>,       RolesRepository>();

        // ── Internal Repositories: cms (new) ──────────────────────────────────
        services.AddScoped<IGenericRepository<ADS,          AdsSearchRequest,          AdsRequest>,          AdsRepository>();
        services.AddScoped<IGenericRepository<ADSGroup,      AdsGroupSearchRequest,      AdsGroupRequest>,      AdsGroupRepository>();
        services.AddScoped<IGenericRepository<Banner,        BannerSearchRequest,        BannerRequest>,        BannerRepository>();
        services.AddScoped<IGenericRepository<Contact,       ContactSearchRequest,       ContactRequest>,       ContactRepository>();
        services.AddScoped<IGenericRepository<ContactGroup,  ContactGroupSearchRequest,  ContactGroupRequest>,  ContactGroupRepository>();
        services.AddScoped<IGenericRepository<Counter,       CounterSearchRequest,       CounterRequest>,       CounterRepository>();
        services.AddScoped<IGenericRepository<Customer,      CustomerSearchRequest,      CustomerRequest>,      CustomerRepository>();
        services.AddScoped<IGenericRepository<EventNews,     EventNewsSearchRequest,     EventNewsRequest>,     EventNewsRepository>();
        services.AddScoped<IGenericRepository<CmsItem,       CmsItemSearchRequest,       CmsItemRequest>,       CmsItemRepository>();
        services.AddScoped<IGenericRepository<ItemType,      ItemTypeSearchRequest,      ItemTypeRequest>,      ItemTypeRepository>();
        services.AddScoped<IGenericRepository<Link,          LinkSearchRequest,          LinkRequest>,          LinkRepository>();
        services.AddScoped<IGenericRepository<LinkGroup,     LinkGroupSearchRequest,     LinkGroupRequest>,     LinkGroupRepository>();
        services.AddScoped<IGenericRepository<NewsComment,   NewsCommentSearchRequest,   NewsCommentRequest>,   NewsCommentRepository>();
        services.AddScoped<IGenericRepository<Page,          PageSearchRequest,          PageRequest>,          PageRepository>();
        services.AddScoped<IGenericRepository<Supportonline, SupportonlineSearchRequest, SupportonlineRequest>, SupportonlineRepository>();
        services.AddScoped<IGenericRepository<Video,         VideoSearchRequest,         VideoRequest>,         VideoRepository>();

        // ── Internal Repositories: dbo ────────────────────────────────────────
        services.AddScoped<IGenericRepository<ChucVu,              ChucVuSearchRequest,              ChucVuRequest>,              ChucVuRepository>();
        services.AddScoped<IGenericRepository<Class,               ClassSearchRequest,               ClassRequest>,               ClassRepository>();
        services.AddScoped<IGenericRepository<ConfigImportReader,  ConfigImportReaderSearchRequest,  ConfigImportReaderRequest>,  ConfigImportReaderRepository>();
        services.AddScoped<IGenericRepository<Course,              DboCoursSearchRequest,            DboCoursRequest>,            DboCoursRepository>();
        services.AddScoped<IGenericRepository<Currency,            CurrencySearchRequest,            CurrencyRequest>,            CurrencyRepository>();
        services.AddScoped<IGenericRepository<Degree,              DboDegreeSearchRequest,           DboDegreeRequest>,           DboDegreeRepository>();
        services.AddScoped<IGenericRepository<Tenant,              TenantSearchRequest,              TenantRequest>,              TenantRepository>();
        services.AddScoped<IGenericRepository<Ethenic,             EthenicSearchRequest,             EthenicRequest>,             EthenicRepository>();
        services.AddScoped<IGenericRepository<GroupReader,         GroupReaderSearchRequest,         GroupReaderRequest>,         GroupReaderRepository>();
        services.AddScoped<IGenericRepository<GroupUser,           GroupUserSearchRequest,           GroupUserRequest>,           GroupUserRepository>();
        services.AddScoped<IGenericRepository<Nation,              NationSearchRequest,              NationRequest>,              NationRepository>();
        services.AddScoped<IOrgRepository, OrgRepository>();
        services.AddScoped<IGenericRepository<Org,                 OrgSearchRequest,                 OrgRequest>>(sp => sp.GetRequiredService<IOrgRepository>());
        services.AddScoped<IGenericRepository<Prof,                ProfSearchRequest,                ProfRequest>,                ProfRepository>();
        services.AddScoped<IReaderRepository, ReaderRepository>();
        services.AddScoped<IGenericRepository<Reader, ReaderSearchRequest, ReaderRequest>>(sp => sp.GetRequiredService<IReaderRepository>());
        services.AddScoped<IGenericRepository<ReaderDelete,        ReaderDeleteSearchRequest,        ReaderDeleteRequest>,        ReaderDeleteRepository>();
        services.AddScoped<IGenericRepository<ReaderInGroup,       ReaderInGroupSearchRequest,       ReaderInGroupRequest>,       ReaderInGroupRepository>();
        services.AddScoped<IGenericRepository<ReaderTrackingLogin, ReaderTrackingLoginSearchRequest, ReaderTrackingLoginRequest>, ReaderTrackingLoginRepository>();
        services.AddScoped<IGenericRepository<ReaderType,          ReaderTypeSearchRequest,          ReaderTypeRequest>,          ReaderTypeRepository>();
        services.AddScoped<IGenericRepository<Sip2Log,             Sip2LogSearchRequest,             Sip2LogRequest>,             Sip2LogRepository>();
        services.AddScoped<IGenericRepository<SystemParameter, SystemParameterSearchRequest, SystemParameterRequest>, SystemParameterRepository>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IGenericRepository<Users, UsersSearchRequest, UsersRequest>>(
            sp => sp.GetRequiredService<IUsersRepository>());
        services.AddScoped<IUserLogRepository, UserLogRepository>();

        // ── Internal Repositories: Ebook ──────────────────────────────────────
        services.AddScoped<IEbookCollectionRepository, EbookCollectionRepository>();
        services.AddScoped<IGenericRepository<CollectionPermistionUser,  CollectionPermistionUserSearchRequest,  CollectionPermistionUserRequest>,  CollectionPermistionUserRepository>();
        services.AddScoped<IGenericRepository<DigType,                   DigTypeSearchRequest,                   DigTypeRequest>,                   DigTypeRepository>();
        services.AddScoped<IGenericRepository<EbookAccess,               EbookAccessSearchRequest,               EbookAccessRequest>,               EbookAccessRepository>();
        services.AddScoped<IEbookFileRepository, EbookFileRepository>();
        services.AddScoped<IGenericRepository<EbookFile,                 EbookFileSearchRequest,                 EbookFileRequest>>(sp => sp.GetRequiredService<IEbookFileRepository>());
        services.AddScoped<IGenericRepository<EbookLog,                  EbookLogSearchRequest,                  EbookLogRequest>,                  EbookLogRepository>();
        services.AddScoped<IGenericRepository<IntroBookCategory,         IntroBookCategorySearchRequest,         IntroBookCategoryRequest>,         IntroBookCategoryRepository>();
        services.AddScoped<IGenericRepository<IntroBooks,                IntroBooksSearchRequest,                IntroBooksRequest>,                IntroBooksRepository>();
        services.AddScoped<IEbookItemRepository, EbookItemRepository>();
        services.AddScoped<IGenericRepository<EbookItem,                 EbookItemSearchRequest,                 EbookItemRequest>>(sp => sp.GetRequiredService<IEbookItemRepository>());
        services.AddScoped<IGenericRepository<EbookItemXml,              EbookItemXmlSearchRequest,              EbookItemXmlRequest>,              EbookItemXmlRepository>();
        services.AddScoped<IEbookItemLoanRepository, EbookItemLoanRepository>();
        services.AddScoped<IGenericRepository<EbookItemLoan,             EbookItemLoanSearchRequest,             EbookItemLoanRequest>>(sp => sp.GetRequiredService<IEbookItemLoanRepository>());
        services.AddScoped<IEbookItemReservationRepository, EbookItemReservationRepository>();
        services.AddScoped<IGenericRepository<EbookItemReservation,      EbookItemReservationSearchRequest,      EbookItemReservationRequest>>(sp => sp.GetRequiredService<IEbookItemReservationRepository>());
        services.AddScoped<IGenericRepository<MetaDataFieldRegistery,    MetaDataFieldRegisterySearchRequest,    MetaDataFieldRegisteryRequest>,    MetaDataFieldRegisteryRepository>();
        services.AddScoped<IGenericRepository<MetadataSchemaRegistry,    MetadataSchemaRegistrySearchRequest,    MetadataSchemaRegistryRequest>,    MetadataSchemaRegistryRepository>();
        services.AddScoped<IGenericRepository<MetaDataValue,             MetaDataValueSearchRequest,             MetaDataValueRequest>,             MetaDataValueRepository>();
        services.AddScoped<IPolicyDigitalRepository, PolicyDigitalRepository>();
        services.AddScoped<IGenericRepository<PolicyDigital,             PolicyDigitalSearchRequest,             PolicyDigitalRequest>>(sp => sp.GetRequiredService<IPolicyDigitalRepository>());
        services.AddScoped<IPolicyDigitalByCollectionRepository, PolicyDigitalByCollectionRepository>();
        services.AddScoped<IGenericRepository<PolicyDigitalByCollection, PolicyDigitalByCollectionSearchRequest, PolicyDigitalByCollectionRequest>>(sp => sp.GetRequiredService<IPolicyDigitalByCollectionRepository>());
        services.AddScoped<IEbookSubjectRepository, EbookSubjectRepository>();
        services.AddScoped<IGenericRepository<TheodoiBienmucEbook,       TheodoiBienmucEbookSearchRequest,       TheodoiBienmucEbookRequest>,       TheodoiBienmucEbookRepository>();
        services.AddScoped<IEbookTopicRepository, EbookTopicRepository>();
        services.AddScoped<IEbookReviewRepository, EbookReviewRepository>();
        services.AddScoped<IGenericRepository<EbookReview, EbookReviewSearchRequest, EbookReviewRequest>>(sp => sp.GetRequiredService<IEbookReviewRepository>());

        // ── Internal Repositories: EOffice ────────────────────────────────────
        services.AddScoped<IGenericRepository<Agency,       AgencySearchRequest,       AgencyRequest>,       AgencyRepository>();
        services.AddScoped<IGenericRepository<Document,     DocumentSearchRequest,     DocumentRequest>,     DocumentRepository>();
        services.AddScoped<IGenericRepository<DocumentFile, DocumentFileSearchRequest, DocumentFileRequest>, DocumentFileRepository>();
        services.AddScoped<IGenericRepository<DocumentType, DocumentTypeSearchRequest, DocumentTypeRequest>, DocumentTypeRepository>();
        services.AddScoped<IGenericRepository<EofficeTopic, EofficeTopicSearchRequest, EofficeTopicRequest>, EofficeTopicRepository>();

        // ── Internal Repositories: Evaluate ───────────────────────────────────
        services.AddScoped<IGenericRepository<EvaluateCourse,  EvaluateCourseSearchRequest,  EvaluateCourseRequest>,  EvaluateCourseRepository>();
        services.AddScoped<IGenericRepository<CourseOption,    CourseOptionSearchRequest,    CourseOptionRequest>,    CourseOptionRepository>();
        services.AddScoped<IGenericRepository<EvaluateDegree,  EvaluateDegreeSearchRequest,  EvaluateDegreeRequest>,  EvaluateDegreeRepository>();
        services.AddScoped<IGenericRepository<Knowledge,       KnowledgeSearchRequest,       KnowledgeRequest>,       KnowledgeRepository>();
        services.AddScoped<IGenericRepository<MonHoc,          MonHocSearchRequest,          MonHocRequest>,          MonHocRepository>();
        services.AddScoped<IGenericRepository<NganhHoc,        NganhHocSearchRequest,        NganhHocRequest>,        NganhHocRepository>();
        services.AddScoped<IGenericRepository<NganhMonHoc,     NganhMonHocSearchRequest,     NganhMonHocRequest>,     NganhMonHocRepository>();
        services.AddScoped<IGenericRepository<EvaluateProgram, EvaluateProgramSearchRequest, EvaluateProgramRequest>, EvaluateProgramRepository>();
        services.AddScoped<IGenericRepository<TaiLieu,         TaiLieuSearchRequest,         TaiLieuRequest>,         TaiLieuRepository>();
        services.AddScoped<IDonViRepository, DonViRepository>();
        services.AddScoped<IGenericRepository<DonVi,           DonViSearchRequest,           DonViRequest>>(sp => sp.GetRequiredService<IDonViRepository>());

        // ── Internal Repositories: PrintBook ──────────────────────────────────
        services.AddScoped<IGenericRepository<Aacr2Field,           Aacr2FieldSearchRequest,           Aacr2FieldRequest>,           Aacr2FieldRepository>();
        services.AddScoped<IGenericRepository<Aacr2Subfield,        Aacr2SubfieldSearchRequest,        Aacr2SubfieldRequest>,        Aacr2SubfieldRepository>();
        services.AddScoped<IGenericRepository<AbDeliverer,          AbDelivererSearchRequest,          AbDelivererRequest>,          AbDelivererRepository>();
        services.AddScoped<IGenericRepository<AbDelivererDetail,    AbDelivererDetailSearchRequest,    AbDelivererDetailRequest>,    AbDelivererDetailRepository>();
        services.AddScoped<IGenericRepository<AbDelivererStatus,    AbDelivererStatusSearchRequest,    AbDelivererStatusRequest>,    AbDelivererStatusRepository>();
        services.AddScoped<IGenericRepository<AbMove,               AbMoveSearchRequest,               AbMoveRequest>,               AbMoveRepository>();
        services.AddScoped<IGenericRepository<AbMoveDetail,         AbMoveDetailSearchRequest,         AbMoveDetailRequest>,         AbMoveDetailRepository>();
        services.AddScoped<IGenericRepository<AbOrder,              AbOrderSearchRequest,              AbOrderRequest>,              AbOrderRepository>();
        services.AddScoped<IGenericRepository<AbOrderDetail,        AbOrderDetailSearchRequest,        AbOrderDetailRequest>,        AbOrderDetailRepository>();
        services.AddScoped<IGenericRepository<AbReceipt,            AbReceiptSearchRequest,            AbReceiptRequest>,            AbReceiptRepository>();
        services.AddScoped<IGenericRepository<AbReceiptDetail,      AbReceiptDetailSearchRequest,      AbReceiptDetailRequest>,      AbReceiptDetailRepository>();
        services.AddScoped<IGenericRepository<AbSource,             AbSourceSearchRequest,             AbSourceRequest>,             AbSourceRepository>();
        services.AddScoped<IGenericRepository<AhReceipt,            AhReceiptSearchRequest,            AhReceiptRequest>,            AhReceiptRepository>();
        services.AddScoped<IGenericRepository<Barcode,              BarcodeSearchRequest,              BarcodeRequest>,              BarcodeRepository>();
        services.AddScoped<IGenericRepository<BarcodeStatus,        BarcodeStatusSearchRequest,        BarcodeStatusRequest>,        BarcodeStatusRepository>();
        services.AddScoped<IGenericRepository<Bib,                  BibSearchRequest,                  BibRequest>,                  BibRepository>();
        services.AddScoped<IGenericRepository<BibType,              BibTypeSearchRequest,              BibTypeRequest>,              BibTypeRepository>();
        services.AddScoped<IGenericRepository<BibWorksheet,         BibWorksheetSearchRequest,         BibWorksheetRequest>,         BibWorksheetRepository>();
        services.AddScoped<IGenericRepository<BibData,              BibDataSearchRequest,              BibDataRequest>,              BibDataRepository>();
        services.AddScoped<IGenericRepository<BibDataOrder,         BibDataOrderSearchRequest,         BibDataOrderRequest>,         BibDataOrderRepository>();
        services.AddScoped<IGenericRepository<BibOrder,             BibOrderSearchRequest,             BibOrderRequest>,             BibOrderRepository>();
        services.AddScoped<IGenericRepository<BibXml,               BibXmlSearchRequest,               BibXmlRequest>,               BibXmlRepository>();
        services.AddScoped<IGenericRepository<BibXmlOrder,          BibXmlOrderSearchRequest,          BibXmlOrderRequest>,          BibXmlOrderRepository>();
        services.AddScoped<IGenericRepository<BookGroup,            BookGroupSearchRequest,            BookGroupRequest>,            BookGroupRepository>();
        services.AddScoped<IGenericRepository<BookGroupDetail,      BookGroupDetailSearchRequest,      BookGroupDetailRequest>,      BookGroupDetailRepository>();
        services.AddScoped<IGenericRepository<BookIn,               BookInSearchRequest,               BookInRequest>,               BookInRepository>();
        services.AddScoped<IGenericRepository<BookOut,              BookOutSearchRequest,              BookOutRequest>,              BookOutRepository>();
        services.AddScoped<IGenericRepository<BookRequest,          BookRequestSearchRequest,          BookRequestRequest>,          BookRequestRepository>();
        services.AddScoped<IGenericRepository<Budget,               BudgetSearchRequest,               BudgetRequest>,               BudgetRepository>();
        services.AddScoped<IGenericRepository<CFine,                CFineSearchRequest,                CFineRequest>,                CFineRepository>();
        services.AddScoped<IGenericRepository<CFineMethod,          CFineMethodSearchRequest,          CFineMethodRequest>,          CFineMethodRepository>();
        services.AddScoped<IGenericRepository<CFineType,            CFineTypeSearchRequest,            CFineTypeRequest>,            CFineTypeRepository>();
        services.AddScoped<IGenericRepository<CFineTicket,          CFineTicketSearchRequest,          CFineTicketRequest>,          CFineTicketRepository>();
        services.AddScoped<IGenericRepository<CPhoto,               CPhotoSearchRequest,               CPhotoRequest>,               CPhotoRepository>();
        services.AddScoped<IGenericRepository<CQueueStatus,         CQueueStatusSearchRequest,         CQueueStatusRequest>,         CQueueStatusRepository>();
        services.AddScoped<IGenericRepository<CRenew,               CRenewSearchRequest,               CRenewRequest>,               CRenewRepository>();
        services.AddScoped<IGenericRepository<CRenewData,           CRenewDataSearchRequest,           CRenewDataRequest>,           CRenewDataRepository>();
        services.AddScoped<IGenericRepository<Cabinet,              CabinetSearchRequest,              CabinetRequest>,              CabinetRepository>();
        services.AddScoped<ICabinetCompartmentRepository, CabinetCompartmentRepository>();
        services.AddScoped<IGenericRepository<CabinetCompartment,   CabinetCompartmentSearchRequest,   CabinetCompartmentRequest>>(sp => sp.GetRequiredService<ICabinetCompartmentRepository>());
        services.AddScoped<IGenericRepository<CheckIn,              CheckInSearchRequest,              CheckInRequest>,              CheckInRepository>();
        services.AddScoped<IGenericRepository<CheckOut,             CheckOutSearchRequest,             CheckOutRequest>,             CheckOutRepository>();
        services.AddScoped<ICircPlaceRepository, CircPlaceRepository>();
        services.AddScoped<IGenericRepository<CircPlace,            CircPlaceSearchRequest,            CircPlaceRequest>>(sp => sp.GetRequiredService<ICircPlaceRepository>());
        services.AddScoped<IGenericRepository<CircPlaceStore,       CircPlaceStoreSearchRequest,       CircPlaceStoreRequest>,       CircPlaceStoreRepository>();
        services.AddScoped<IGenericRepository<CircPlaceReaderType,  CircPlaceReaderTypeSearchRequest,  CircPlaceReaderTypeRequest>,  CircPlaceReaderTypeRepository>();
        services.AddScoped<IGenericRepository<DocGroup,             DocGroupSearchRequest,             DocGroupRequest>,             DocGroupRepository>();
        services.AddScoped<IGenericRepository<PolicyCircDocGroup,   PolicyCircDocGroupSearchRequest,   PolicyCircDocGroupRequest>,   PolicyCircDocGroupRepository>();
        services.AddScoped<IGenericRepository<PolicyCircFine,       PolicyCircFineSearchRequest,       PolicyCircFineRequest>,       PolicyCircFineRepository>();
        services.AddScoped<IGenericRepository<ConfigReceiption,     ConfigReceiptionSearchRequest,     ConfigReceiptionRequest>,     ConfigReceiptionRepository>();
        services.AddScoped<IGenericRepository<ConfigAacr2,          ConfigAacr2SearchRequest,          ConfigAacr2Request>,          ConfigAacr2Repository>();
        services.AddScoped<IGenericRepository<ConfigIsbd,           ConfigIsbdSearchRequest,           ConfigIsbdRequest>,           ConfigIsbdRepository>();
        services.AddScoped<IGenericRepository<Countries,            CountriesSearchRequest,            CountriesRequest>,            CountriesRepository>();
        services.AddScoped<IGenericRepository<DBibStatus,           DBibStatusSearchRequest,           DBibStatusRequest>,           DBibStatusRepository>();
        services.AddScoped<IGenericRepository<DKey,                 DKeySearchRequest,                 DKeyRequest>,                 DKeyRepository>();
        services.AddScoped<IGenericRepository<DExportReason,        DExportReasonSearchRequest,        DExportReasonRequest>,        DExportReasonRepository>();
        services.AddScoped<IGenericRepository<DBookOutUnit,         DBookOutUnitSearchRequest,         DBookOutUnitRequest>,         DBookOutUnitRepository>();
        services.AddScoped<IGenericRepository<DExhibitionLocation,  DExhibitionLocationSearchRequest,  DExhibitionLocationRequest>,  DExhibitionLocationRepository>();
        services.AddScoped<IGenericRepository<BookOutStore,         BookOutStoreSearchRequest,         BookOutStoreRequest>,         BookOutStoreRepository>();
        services.AddScoped<IGenericRepository<DPublisher,           DPublisherSearchRequest,           DPublisherRequest>,           DPublisherRepository>();
        services.AddScoped<IGenericRepository<DFixField,            DFixFieldSearchRequest,            DFixFieldRequest>,            DFixFieldRepository>();
        services.AddScoped<IGenericRepository<DFixFieldPost,        DFixFieldPostSearchRequest,        DFixFieldPostRequest>,        DFixFieldPostRepository>();
        services.AddScoped<IGenericRepository<DFixFieldValue,       DFixFieldValueSearchRequest,       DFixFieldValueRequest>,       DFixFieldValueRepository>();
        services.AddScoped<IGenericRepository<DicAuthor,            DicAuthorSearchRequest,            DicAuthorRequest>,            DicAuthorRepository>();
        services.AddScoped<IGenericRepository<DicClass,             DicClassSearchRequest,             DicClassRequest>,             DicClassRepository>();
        services.AddScoped<IGenericRepository<DicKeyword,           DicKeywordSearchRequest,           DicKeywordRequest>,           DicKeywordRepository>();
        services.AddScoped<IGenericRepository<DicPublisher,         DicPublisherSearchRequest,         DicPublisherRequest>,         DicPublisherRepository>();
        services.AddScoped<IGenericRepository<FixedFieldValue,      FixedFieldValueSearchRequest,      FixedFieldValueRequest>,      FixedFieldValueRepository>();
        services.AddScoped<IGenericRepository<FrequencyMagazine,    FrequencyMagazineSearchRequest,    FrequencyMagazineRequest>,    FrequencyMagazineRepository>();
        services.AddScoped<IGenericRepository<Fund,                 FundSearchRequest,                 FundRequest>,                 FundRepository>();
        services.AddScoped<IGenericRepository<GeographicAreas,      GeographicAreasSearchRequest,      GeographicAreasRequest>,      GeographicAreasRepository>();
        services.AddScoped<IGenericRepository<Inventory,            InventorySearchRequest,            InventoryRequest>,            InventoryRepository>();
        services.AddScoped<IGenericRepository<InventoryBarcode,     InventoryBarcodeSearchRequest,     InventoryBarcodeRequest>,     InventoryBarcodeRepository>();
        services.AddScoped<IGenericRepository<IsbdField,            IsbdFieldSearchRequest,            IsbdFieldRequest>,            IsbdFieldRepository>();
        services.AddScoped<IGenericRepository<IsbdSubfield,         IsbdSubfieldSearchRequest,         IsbdSubfieldRequest>,         IsbdSubfieldRepository>();
        services.AddScoped<IGenericRepository<PbKey,                PbKeySearchRequest,                PbKeyRequest>,                PbKeyRepository>();
        services.AddScoped<IGenericRepository<KeyIn,                KeyInSearchRequest,                KeyInRequest>,                KeyInRepository>();
        services.AddScoped<IGenericRepository<KeyOut,               KeyOutSearchRequest,               KeyOutRequest>,               KeyOutRepository>();
        services.AddScoped<IGenericRepository<KiemKe,               KiemKeSearchRequest,               KiemKeRequest>,               KiemKeRepository>();
        services.AddScoped<IGenericRepository<Language,           LanguageSearchRequest,           LanguageRequest>,           LanguageRepository>();
        services.AddScoped<IGenericRepository<LinhVucNghienCuu,     LinhVucNghienCuuSearchRequest,     LinhVucNghienCuuRequest>,     LinhVucNghienCuuRepository>();
        services.AddScoped<IGenericRepository<LogBienMuc,           LogBienMucSearchRequest,           LogBienMucRequest>,           LogBienMucRepository>();
        services.AddScoped<IGenericRepository<LostBook,             LostBookSearchRequest,             LostBookRequest>,             LostBookRepository>();
        services.AddScoped<IGenericRepository<Lydophat,             LydophatSearchRequest,             LydophatRequest>,             LydophatRepository>();
        services.AddScoped<IGenericRepository<MagazineType,         MagazineTypeSearchRequest,         MagazineTypeRequest>,         MagazineTypeRepository>();
        services.AddScoped<IGenericRepository<MarcCodeList,         MarcCodeListSearchRequest,         MarcCodeListRequest>,         MarcCodeListRepository>();
        services.AddScoped<IGenericRepository<MarcField,            MarcFieldSearchRequest,            MarcFieldRequest>,            MarcFieldRepository>();
        services.AddScoped<IGenericRepository<MarcIndicator,        MarcIndicatorSearchRequest,        MarcIndicatorRequest>,        MarcIndicatorRepository>();
        services.AddScoped<IGenericRepository<MarcSubField,         MarcSubFieldSearchRequest,         MarcSubFieldRequest>,         MarcSubFieldRepository>();
        services.AddScoped<IGenericRepository<MarcBibLevel,         MarcBibLevelSearchRequest,         MarcBibLevelRequest>,         MarcBibLevelRepository>();
        services.AddScoped<IGenericRepository<MarcRecordType,       MarcRecordTypeSearchRequest,       MarcRecordTypeRequest>,       MarcRecordTypeRepository>();
        services.AddScoped<IGenericRepository<MarcType,             MarcTypeSearchRequest,             MarcTypeRequest>,             MarcTypeRepository>();
        services.AddScoped<IGenericRepository<MaterialsType,        MaterialsTypeSearchRequest,        MaterialsTypeRequest>,        MaterialsTypeRepository>();
        services.AddScoped<IGenericRepository<OrderStatus,          OrderStatusSearchRequest,          OrderStatusRequest>,          OrderStatusRepository>();
        services.AddScoped<IGenericRepository<PartemMagazineDetail, PartemMagazineDetailSearchRequest, PartemMagazineDetailRequest>, PartemMagazineDetailRepository>();
        services.AddScoped<IGenericRepository<PatternMagazine,      PatternMagazineSearchRequest,      PatternMagazineRequest>,      PatternMagazineRepository>();
        services.AddScoped<IGenericRepository<PhongBan,             PhongBanSearchRequest,             PhongBanRequest>,             PhongBanRepository>();
        services.AddScoped<IPolicyCircRepository, PolicyCircRepository>();
        services.AddScoped<IGenericRepository<PolicyCirc,           PolicyCircSearchRequest,           PolicyCircRequest>>(sp => sp.GetRequiredService<IPolicyCircRepository>());
        services.AddScoped<IGenericRepository<PrintBookAndDigital,  PrintBookAndDigitalSearchRequest,  PrintBookAndDigitalRequest>,  PrintBookAndDigitalRepository>();
        services.AddScoped<IGenericRepository<PbPrivate,            PbPrivateSearchRequest,            PbPrivateRequest>,            PbPrivateRepository>();
        services.AddScoped<IGenericRepository<ReceiptStatus,        ReceiptStatusSearchRequest,        ReceiptStatusRequest>,        ReceiptStatusRepository>();
        services.AddScoped<IGenericRepository<RecordType,           RecordTypeSearchRequest,           RecordTypeRequest>,           RecordTypeRepository>();
        services.AddScoped<IGenericRepository<PbRoles,              PbRolesSearchRequest,              PbRolesRequest>,              PbRolesRepository>();
        services.AddScoped<IGenericRepository<Serial,               SerialSearchRequest,               SerialRequest>,               SerialRepository>();
        services.AddScoped<IGenericRepository<SerialItem,           SerialItemSearchRequest,           SerialItemRequest>,           SerialItemRepository>();
        services.AddScoped<IGenericRepository<Store,                StoreSearchRequest,                StoreRequest>,                StoreRepository>();
        services.AddScoped<IStoreTypeRepository, StoreTypeRepository>();
        services.AddScoped<IGenericRepository<StoreType,            StoreTypeSearchRequest,            StoreTypeRequest>>(sp => sp.GetRequiredService<IStoreTypeRepository>());
        services.AddScoped<IGenericRepository<SubcriptionStatus,    SubcriptionStatusSearchRequest,    SubcriptionStatusRequest>,    SubcriptionStatusRepository>();
        services.AddScoped<IGenericRepository<Supplier,             SupplierSearchRequest,             SupplierRequest>,             SupplierRepository>();
        services.AddScoped<IGenericRepository<SystemDescription,    SystemDescriptionSearchRequest,    SystemDescriptionRequest>,    SystemDescriptionRepository>();
        services.AddScoped<IGenericRepository<SystemInfo,           SystemInfoSearchRequest,           SystemInfoRequest>,           SystemInfoRepository>();
        services.AddScoped<IGenericRepository<SystemPara,           SystemParaSearchRequest,           SystemParaRequest>,           SystemParaRepository>();
        services.AddScoped<IGenericRepository<Thanhly,              ThanhlySearchRequest,              ThanhlyRequest>,              ThanhlyRepository>();
        services.AddScoped<IGenericRepository<TrackingToLibrary,    TrackingToLibrarySearchRequest,    TrackingToLibraryRequest>,    TrackingToLibraryRepository>();
        services.AddScoped<IGenericRepository<WorksheetField,       WorksheetFieldSearchRequest,       WorksheetFieldRequest>,       WorksheetFieldRepository>();
        services.AddScoped<IGenericRepository<WorksheetSubfield,    WorksheetSubfieldSearchRequest,    WorksheetSubfieldRequest>,    WorksheetSubfieldRepository>();
        services.AddScoped<IGenericRepository<Z3950Group,           Z3950GroupSearchRequest,           Z3950GroupRequest>,           Z3950GroupRepository>();
        services.AddScoped<IGenericRepository<Z3950Config,         Z3950ConfigSearchRequest,         Z3950ConfigRequest>,         Z3950ConfigRepository>();
        services.AddSingleton<Z3950BerClient>();
        services.AddScoped<IZ3950SearchService, Z3950SearchService>();

        // ── Schema map ────────────────────────────────────────────────────────
        services.AddScoped<IGenericRepository<MapBuilding,     MapBuildingSearchRequest,     MapBuildingRequest>,     MapBuildingRepository>();
        services.AddScoped<IGenericRepository<MapFloor,        MapFloorSearchRequest,        MapFloorRequest>,        MapFloorRepository>();
        services.AddScoped<IGenericRepository<MapFloorUtility, MapFloorUtilitySearchRequest, MapFloorUtilityRequest>, MapFloorUtilityRepository>();
        services.AddScoped<IGenericRepository<MapObject,       MapObjectSearchRequest,       MapObjectRequest>,       MapObjectRepository>();
        services.AddScoped<IGenericRepository<MapShelfDetail,  MapShelfDetailSearchRequest,  MapShelfDetailRequest>,  MapShelfDetailRepository>();
        services.AddScoped<IGenericRepository<MapShelfRow,     MapShelfRowSearchRequest,     MapShelfRowRequest>,     MapShelfRowRepository>();
        services.AddScoped<IGenericRepository<MapEquipment,    MapEquipmentSearchRequest,    MapEquipmentRequest>,    MapEquipmentRepository>();
        services.AddScoped<IGenericRepository<RoomBookingConfig, RoomBookingConfigSearchRequest, RoomBookingConfigRequest>, RoomBookingConfigRepository>();
        services.AddScoped<IRoomBookingRepository, RoomBookingRepository>();
        services.AddScoped<IGenericRepository<RoomBooking, RoomBookingSearchRequest, RoomBookingRequest>>(sp => sp.GetRequiredService<IRoomBookingRepository>());

        // ── Public Repositories ───────────────────────────────────────────────
        services.AddMemoryCache();
        // Đợt 22 — cache versioning cho PublicBaseRepository (xem CacheInvalidator.cs).
        services.AddSingleton<ICacheInvalidator, CacheInvalidator>();
        services.AddScoped<IPublicNewsRepository,                                               PublicNewsRepository>();
        services.AddScoped<IPublicMenuRepository,                                               PublicMenuRepository>();
        services.AddScoped<IPublicCounterRepository,             PublicCounterRepository>();
        services.AddScoped<IPublicGenericRepository<Category,   PublicCategorySearchRequest>,   PublicCategoryRepository>();
        services.AddScoped<IPublicGenericRepository<DigType,    PublicDigTypeSearchRequest>,    PublicDigTypeRepository>();
        services.AddScoped<IPublicGenericRepository<EbookSubject, PublicEbookSubjectSearchRequest>, PublicEbookSubjectRepository>();
        services.AddScoped<IPublicGenericRepository<EbookTopic, PublicEbookTopicSearchRequest>, PublicEbookTopicRepository>();
        services.AddScoped<IPublicGenericRepository<Photo,      PublicPhotoSearchRequest>,      PublicPhotoRepository>();
        services.AddScoped<IPublicGenericRepository<PhotoAlbum, PublicPhotoAlbumSearchRequest>, PublicPhotoAlbumRepository>();
        services.AddScoped<IPublicEbookCollectionRepository, PublicEbookCollectionRepository>();
        services.AddScoped<IPublicEbookRepository,           PublicEbookRepository>();
        services.AddScoped<IPublicPrintBookRepository,       PublicPrintBookRepository>();
        services.AddScoped<IPublicLibraryMapRepository,      PublicLibraryMapRepository>();
        services.AddScoped<IPublicEbookReviewRepository,     PublicEbookReviewRepository>();
        services.AddScoped<IPublicEbookFavoriteRepository,   PublicEbookFavoriteRepository>();
        services.AddScoped<IPublicGenericRepository<Z3950Config, PublicZ3950ConfigSearchRequest>, PublicZ3950ConfigRepository>();
        services.AddScoped<IPublicHyperLinkRepository,       PublicHyperLinkRepository>();
        services.AddScoped<IPublicBannerRepository,          PublicBannerRepository>();
        services.AddScoped<IPublicSystemParameterRepository, PublicSystemParameterRepository>();
        services.AddScoped<IPublicTenantRepository, PublicTenantRepository>();
        services.AddScoped<IPublicGenericRepository<MapShelfRow, PublicMapShelfRowSearchRequest>, PublicMapShelfRowRepository>();

        // ── Hangfire ──────────────────────────────────────────────────────────
        var defaultConnKey = dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ? "PostgreSQL" : "SqlServer";
        var hangfireConn = AesEncryptionHelper.DecryptConnectionString(
            configuration.GetConnectionString("Hangfire")
            ?? configuration.GetConnectionString(defaultConnKey) ?? "");
        if (!string.IsNullOrEmpty(hangfireConn))
        {
            services.AddHangfire(cfg =>
            {
                cfg.SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
                   .UseSimpleAssemblyNameTypeSerializer()
                   .UseRecommendedSerializerSettings();

                if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    cfg.UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(hangfireConn));
                }
                else
                {
                    if (!hangfireConn.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
                        hangfireConn = hangfireConn.TrimEnd(';') + ";TrustServerCertificate=True;";
                    if (!hangfireConn.Contains("Encrypt", StringComparison.OrdinalIgnoreCase))
                        hangfireConn = hangfireConn.TrimEnd(';') + ";Encrypt=False;";
                    cfg.UseSqlServerStorage(hangfireConn, new Hangfire.SqlServer.SqlServerStorageOptions
                    {
                        PrepareSchemaIfNecessary = true,
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.FromSeconds(15),
                    });
                }
            });
            services.AddHangfireServer(opt =>
            {
                opt.WorkerCount = 4;
                opt.Queues      = ["indexing", "default"];
            });
        }

        return services;
    }
}
