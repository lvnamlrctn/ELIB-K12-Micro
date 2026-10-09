using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Entities.Gamification;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Data;

public class ELIBAPIDbContext : DbContext
{
    public ELIBAPIDbContext(DbContextOptions<ELIBAPIDbContext> options) : base(options) { }

    // ── Schema cms ────────────────────────────────────────────────────────────
    public DbSet<News>        News        { get; set; }
    public DbSet<Category>    Categories  { get; set; }
    public DbSet<Menu>        Menus       { get; set; }
    public DbSet<MenuType>    MenuTypes   { get; set; }
    public DbSet<Module>      Modules     { get; set; }
    public DbSet<ModuleRoles> ModuleRoles { get; set; }
    public DbSet<Permission>  Permissions { get; set; }
    public DbSet<Photo>       Photos      { get; set; }
    public DbSet<PhotoAlbum>  PhotoAlbums { get; set; }
    public DbSet<AttachFile>  AttachFiles { get; set; }
    public DbSet<Roles>       Roles       { get; set; }
    public DbSet<ADS>         ADS         { get; set; }
    public DbSet<ADSGroup>    ADSGroups   { get; set; }
    public DbSet<Banner>      Banners     { get; set; }
    public DbSet<Contact>     Contacts    { get; set; }
    public DbSet<ContactGroup>ContactGroups{ get; set; }
    public DbSet<Counter>     Counters    { get; set; }
    public DbSet<Customer>    Customers   { get; set; }
    public DbSet<EventNews>   EventNews   { get; set; }
    public DbSet<CmsItem>     CmsItems    { get; set; }
    public DbSet<ItemType>    ItemTypes   { get; set; }
    public DbSet<Link>        Links       { get; set; }
    public DbSet<LinkGroup>   LinkGroups  { get; set; }
    public DbSet<NewsComment> NewsComments{ get; set; }
    public DbSet<Page>        Pages       { get; set; }
    public DbSet<Supportonline> Supportonlines { get; set; }
    public DbSet<Video>       Videos      { get; set; }

    // ── Schema dbo ────────────────────────────────────────────────────────────
    public DbSet<Users>   Users   { get; set; }
    public DbSet<UserLog> UserLogs { get; set; }
    public DbSet<ChucVu>  ChucVus  { get; set; }
    public DbSet<Class>   Classes  { get; set; }
    public DbSet<ConfigImportReader> ConfigImportReaders { get; set; }
    public DbSet<Course>  Courses  { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<Degree>  Degrees  { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Ethenic> Ethenics { get; set; }
    public DbSet<GroupReader> GroupReaders { get; set; }
    public DbSet<GroupUser>   GroupUsers   { get; set; }
    public DbSet<Nation>  Nations  { get; set; }
    public DbSet<Org>     Orgs     { get; set; }
    public DbSet<Prof>    Profs    { get; set; }
    public DbSet<Reader>  Readers  { get; set; }
    public DbSet<ReaderPhoto> ReaderPhotos { get; set; }
    public DbSet<ReaderDelete> ReaderDeletes { get; set; }
    public DbSet<ReaderInGroup> ReaderInGroups { get; set; }
    public DbSet<ReaderTrackingLogin> ReaderTrackingLogins { get; set; }
    public DbSet<ReaderType> ReaderTypes { get; set; }
    public DbSet<ReaderPreference> ReaderPreferences { get; set; }
    public DbSet<ScheduledReport> ScheduledReports { get; set; }
    public DbSet<Sip2Log> Sip2Logs { get; set; }
    public DbSet<SystemParameter> SystemParameters { get; set; }
    public DbSet<NotificationChannelConfig> NotificationChannelConfigs { get; set; }
    public DbSet<NotificationLog>           NotificationLogs           { get; set; }
    public DbSet<SearchObservation>         SearchObservations         { get; set; }
    public DbSet<ReaderSavedSearch>         ReaderSavedSearches        { get; set; }
    public DbSet<ReaderWorkspace>           ReaderWorkspaces           { get; set; }
    public DbSet<AdminTask>                 AdminTasks                 { get; set; }
    public DbSet<AdminTaskChunk>            AdminTaskChunks            { get; set; }
    public DbSet<AdminWorkerHeartbeat>      AdminWorkerHeartbeats      { get; set; }
    public DbSet<AdminTaskControlEvent>     AdminTaskControlEvents     { get; set; }
    public DbSet<AdminTaskRetentionRun>     AdminTaskRetentionRuns     { get; set; }
    public DbSet<AdminWorkAssignment>       AdminWorkAssignments       { get; set; }
    public DbSet<AdminUiPreference>         AdminUiPreferences         { get; set; }

    // ── Ngữ cảnh tác vụ nền (Đợt 10) ─────────────────────────────────────────────
    // Không map DB — chỉ giữ trạng thái trong bộ nhớ của instance DbContext (scoped theo từng lượt worker
    // xử lý 1 chunk). BaseRepository.GetCurrentUserId()/GetCurrentTenantId() đọc lại 2 property này khi
    // không có HttpContext (worker không chạy trong request HTTP) — PHẢI đặt trước khi gọi bất kỳ repo
    // nào từ trong AdminTaskService.RunNext, nếu không repo sẽ âm thầm coi là tài khoản đặc quyền
    // (xem BaseRepository.cs).
    public long? BackgroundActorId { get; set; }
    public long? BackgroundTenantId { get; set; }
    public bool  AcceptedAdminTask { get; set; }
    public Guid? AdminTaskId { get; set; }

    // ── Schema Ebook ──────────────────────────────────────────────────────────
    public DbSet<EbookCollection> EbookCollections { get; set; }
    public DbSet<CollectionPermistionUser> CollectionPermistionUsers { get; set; }
    public DbSet<DigType>  DigTypes  { get; set; }
    public DbSet<EbookAccess> EbookAccesses { get; set; }
    public DbSet<EbookFile> EbookFiles { get; set; }
    public DbSet<DigitalStorageAuditResult> DigitalStorageAuditResults { get; set; }
    public DbSet<EbookLog> EbookLogs { get; set; }
    public DbSet<IntroBookCategory> IntroBookCategories { get; set; }
    public DbSet<IntroBooks> IntroBooks { get; set; }
    public DbSet<EbookItem> EbookItems { get; set; }
    public DbSet<EbookItemXml> EbookItemXmls { get; set; }
    public DbSet<EbookItemLoan> EbookItemLoans { get; set; }
    public DbSet<EbookItemReservation> EbookItemReservations { get; set; }
    public DbSet<MetaDataFieldRegistery> MetaDataFieldRegisteries { get; set; }
    public DbSet<MetadataSchemaRegistry> MetadataSchemaRegistries { get; set; }
    public DbSet<MetaDataValue> MetaDataValues { get; set; }
    public DbSet<PolicyDigital> PolicyDigitals { get; set; }
    public DbSet<PolicyDigitalByCollection> PolicyDigitalByCollections { get; set; }
    public DbSet<EbookSubject> EbookSubjects { get; set; }
    public DbSet<TheodoiBienmucEbook> TheodoiBienmucEbooks { get; set; }
    public DbSet<EbookTopic> EbookTopics { get; set; }
    public DbSet<EbookReview> EbookReviews { get; set; }
    public DbSet<DocumentSubmission> DocumentSubmissions { get; set; }
    public DbSet<EbookFavorite> EbookFavorites { get; set; }

    // ── Schema EOffice ────────────────────────────────────────────────────────
    public DbSet<Agency>       Agencies      { get; set; }
    public DbSet<Document>     Documents     { get; set; }
    public DbSet<DocumentFile> DocumentFiles { get; set; }
    public DbSet<DocumentType> DocumentTypes { get; set; }
    public DbSet<EofficeTopic> EofficeTopics { get; set; }

    // ── Schema Evaluate ───────────────────────────────────────────────────────
    public DbSet<EvaluateCourse>  EvaluateCourses  { get; set; }
    public DbSet<CourseOption>    CourseOptions    { get; set; }
    public DbSet<EvaluateDegree>  EvaluateDegrees  { get; set; }
    public DbSet<Knowledge>       Knowledges       { get; set; }
    public DbSet<MonHoc>          MonHocs          { get; set; }
    public DbSet<NganhHoc>        NganhHocs        { get; set; }
    public DbSet<NganhMonHoc>     NganhMonHocs     { get; set; }
    public DbSet<EvaluateProgram> EvaluatePrograms { get; set; }
    public DbSet<TaiLieu>         TaiLieus         { get; set; }
    public DbSet<DonVi>           DonVis           { get; set; }

    // ── Schema PrintBook ──────────────────────────────────────────────────────
    public DbSet<Z3950Config>          Z3950Configs          { get; set; }
    public DbSet<Z3950Group>           Z3950Groups           { get; set; }
    public DbSet<Aacr2Field>           Aacr2Fields           { get; set; }
    public DbSet<Aacr2Subfield>        Aacr2Subfields        { get; set; }
    public DbSet<AbDeliverer>          AbDeliverers          { get; set; }
    public DbSet<AbDelivererDetail>    AbDelivererDetails    { get; set; }
    public DbSet<AbDelivererStatus>    AbDelivererStatuses   { get; set; }
    public DbSet<AbMove>               AbMoves               { get; set; }
    public DbSet<AbMoveDetail>         AbMoveDetails         { get; set; }
    public DbSet<AbOrder>              AbOrders              { get; set; }
    public DbSet<AbOrderDetail>        AbOrderDetails        { get; set; }
    public DbSet<AbReceipt>            AbReceipts            { get; set; }
    public DbSet<AbReceiptDetail>      AbReceiptDetails      { get; set; }
    public DbSet<AbSource>             AbSources             { get; set; }
    public DbSet<AhReceipt>            AhReceipts            { get; set; }
    public DbSet<Barcode>              Barcodes              { get; set; }
    public DbSet<BarcodeStatus>        BarcodeStatuses       { get; set; }
    public DbSet<Bib>                  Bibs                  { get; set; }
    public DbSet<BibType>              BibTypes              { get; set; }
    public DbSet<BibWorksheet>         BibWorksheets         { get; set; }
    public DbSet<BibData>              BibDatas              { get; set; }
    public DbSet<BibDataOrder>         BibDataOrders         { get; set; }
    public DbSet<BibOrder>             BibOrders             { get; set; }
    public DbSet<BibXml>               BibXmls               { get; set; }
    public DbSet<BibXmlOrder>          BibXmlOrders          { get; set; }
    public DbSet<BookGroup>            BookGroups            { get; set; }
    public DbSet<BookGroupDetail>      BookGroupDetails      { get; set; }
    public DbSet<BookIn>               BookIns               { get; set; }
    public DbSet<BookOut>              BookOuts              { get; set; }
    public DbSet<BookRequest>          BookRequests          { get; set; }
    public DbSet<Budget>               Budgets               { get; set; }
    public DbSet<CFine>                CFines                { get; set; }
    public DbSet<CFineMethod>          CFineMethods          { get; set; }
    public DbSet<CFineType>            CFineTypes            { get; set; }
    public DbSet<CFineTicket>          CFineTickets          { get; set; }
    public DbSet<CPhoto>               CPhotos               { get; set; }
    public DbSet<CQueueStatus>         CQueueStatuses        { get; set; }
    public DbSet<CRenew>               CRenews               { get; set; }
    public DbSet<CRenewData>           CRenewDatas           { get; set; }
    public DbSet<Cabinet>              Cabinets              { get; set; }
    public DbSet<CabinetCompartment>   CabinetCompartments   { get; set; }
    public DbSet<CheckIn>              CheckIns              { get; set; }
    public DbSet<CheckOut>             CheckOuts             { get; set; }
    public DbSet<CircPlace>            CircPlaces            { get; set; }
    public DbSet<CircPlaceStore>       CircPlaceStores       { get; set; }
    public DbSet<CircPlaceReaderType>  CircPlaceReaderTypes  { get; set; }
    public DbSet<DocGroup>             DocGroups             { get; set; }
    public DbSet<PolicyCircDocGroup>   PolicyCircDocGroups   { get; set; }
    public DbSet<PolicyCircFine>       PolicyCircFines       { get; set; }
    public DbSet<ConfigReceiption>     ConfigReceiptions     { get; set; }
    public DbSet<ConfigAacr2>          ConfigAacr2s          { get; set; }
    public DbSet<ConfigIsbd>           ConfigIsbds           { get; set; }
    public DbSet<Countries>            Countries             { get; set; }
    public DbSet<DBibStatus>           DBibStatuses          { get; set; }
    public DbSet<DKey>                 DKeys                 { get; set; }
    public DbSet<DExportReason>        DExportReasons        { get; set; }
    public DbSet<DBookOutUnit>         DBookOutUnits         { get; set; }
    public DbSet<DExhibitionLocation>  DExhibitionLocations  { get; set; }
    public DbSet<BookOutStore>         BookOutStores         { get; set; }
    public DbSet<DPublisher>           DPublishers           { get; set; }
    public DbSet<DFixField>            DFixFields            { get; set; }
    public DbSet<DFixFieldPost>        DFixFieldPosts        { get; set; }
    public DbSet<DFixFieldValue>       DFixFieldValues       { get; set; }
    public DbSet<DicAuthor>            DicAuthors            { get; set; }
    public DbSet<DicClass>             DicClasses            { get; set; }
    public DbSet<DicKeyword>           DicKeywords           { get; set; }
    public DbSet<DicPublisher>         DicPublishers         { get; set; }
    public DbSet<FixedFieldValue>      FixedFieldValues      { get; set; }
    public DbSet<FixedFieldValueOrder> FixedFieldValueOrders { get; set; }
    public DbSet<FrequencyMagazine>    FrequencyMagazines    { get; set; }
    public DbSet<Fund>                 Funds                 { get; set; }
    public DbSet<GeographicAreas>      GeographicAreas       { get; set; }
    public DbSet<Inventory>            Inventories           { get; set; }
    public DbSet<InventoryBarcode>     InventoryBarcodes     { get; set; }
    public DbSet<IsbdField>            IsbdFields            { get; set; }
    public DbSet<IsbdSubfield>         IsbdSubfields         { get; set; }
    public DbSet<PbKey>                PbKeys                { get; set; }
    public DbSet<KeyIn>                KeyIns                { get; set; }
    public DbSet<KeyOut>               KeyOuts               { get; set; }
    public DbSet<KiemKe>               KiemKes               { get; set; }
    public DbSet<Language>             Languages           { get; set; }
    public DbSet<LinhVucNghienCuu>     LinhVucNghienCuus     { get; set; }
    public DbSet<LogBienMuc>           LogBienMucs           { get; set; }
    public DbSet<LostBook>             LostBooks             { get; set; }
    public DbSet<Lydophat>             Lydophats             { get; set; }
    public DbSet<MagazineType>         MagazineTypes         { get; set; }
    public DbSet<MarcCodeList>         MarcCodeLists         { get; set; }
    public DbSet<MarcField>            MarcFields            { get; set; }
    public DbSet<MarcIndicator>        MarcIndicators        { get; set; }
    public DbSet<MarcSubField>         MarcSubFields         { get; set; }
    public DbSet<MarcBibLevel>         MarcBibLevels         { get; set; }
    public DbSet<MarcRecordType>       MarcRecordTypes       { get; set; }
    public DbSet<MarcType>             MarcTypes             { get; set; }
    public DbSet<MaterialsType>        MaterialsTypes        { get; set; }
    public DbSet<OrderStatus>          OrderStatuses         { get; set; }
    public DbSet<PartemMagazineDetail> PartemMagazineDetails { get; set; }
    public DbSet<PatternMagazine>      PatternMagazines      { get; set; }
    public DbSet<PhongBan>             PhongBans             { get; set; }
    public DbSet<PolicyCirc>           PolicyCircs           { get; set; }
    public DbSet<PrintBookAndDigital>  PrintBookAndDigitals  { get; set; }
    public DbSet<PbPrivate>            PbPrivates            { get; set; }
    public DbSet<ReceiptStatus>        ReceiptStatuses       { get; set; }
    public DbSet<RecordType>           RecordTypes           { get; set; }
    public DbSet<PbRoles>              PbRoles               { get; set; }
    public DbSet<Serial>               Serials               { get; set; }
    public DbSet<SerialItem>           SerialItems           { get; set; }
    public DbSet<SerialBinding>        SerialBindings        { get; set; }
    public DbSet<SerialBindingItem>    SerialBindingItems    { get; set; }
    public DbSet<Store>                Stores                { get; set; }
    public DbSet<StoreType>            StoreTypes            { get; set; }
    public DbSet<SubcriptionStatus>    SubcriptionStatuses   { get; set; }
    public DbSet<Supplier>             Suppliers             { get; set; }
    public DbSet<SystemDescription>    SystemDescriptions    { get; set; }
    public DbSet<SystemInfo>           SystemInfos           { get; set; }
    public DbSet<SystemPara>           SystemParas           { get; set; }
    public DbSet<Thanhly>              Thanhlys              { get; set; }
    public DbSet<TrackingToLibrary>    TrackingToLibraries   { get; set; }
    public DbSet<WorksheetField>       WorksheetFields       { get; set; }
    public DbSet<WorksheetSubfield>    WorksheetSubfields    { get; set; }

    // ── Schema map ────────────────────────────────────────────────────────────
    public DbSet<MapBuilding>     MapBuildings     { get; set; }
    public DbSet<MapFloor>        MapFloors        { get; set; }
    public DbSet<MapFloorUtility> MapFloorUtilities{ get; set; }
    public DbSet<MapObject>       MapObjects       { get; set; }
    public DbSet<MapShelfDetail>  MapShelfDetails  { get; set; }
    public DbSet<MapShelfRow>     MapShelfRows     { get; set; }
    public DbSet<MapEquipment>    MapEquipments    { get; set; }
    public DbSet<RoomBooking>       RoomBookings       { get; set; }
    public DbSet<RoomBookingConfig> RoomBookingConfigs { get; set; }
    // Đặt phòng nâng cao (port ELIB-LRC 10-04) — bảng tạo bằng RoomBookingSchema lúc khởi động (Postgres).
    public DbSet<RoomBookingBan>    RoomBookingBans    { get; set; }
    public DbSet<RoomOpeningHour>   RoomOpeningHours   { get; set; }
    public DbSet<RoomSpecialDay>    RoomSpecialDays    { get; set; }
    public DbSet<RoomBookingMember> RoomBookingMembers { get; set; }
    public DbSet<AccessDevice>      AccessDevices      { get; set; }
    public DbSet<AccessStaffCard>   AccessStaffCards   { get; set; }
    public DbSet<AccessScanLog>     AccessScanLogs     { get; set; }
    public DbSet<AccessCommand>     AccessCommands     { get; set; }

    // ── Thanh toán QR (port ELIB-LRC 09-25) — bảng tạo bằng PaymentSchema lúc khởi động (Postgres) ─────
    public DbSet<ELIBAPI.Core.Entities.Payment.PaymentTransaction> PaymentTransactions { get; set; }

    // ── Schema Gamification ──────────────────────────────────────────────────
    public DbSet<Badge>       Badges       { get; set; }
    public DbSet<ReaderBadge> ReaderBadges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var uuidSql   = Database.IsSqlServer() ? "newsequentialid()" : "gen_random_uuid()";
        var dboSchema = Database.IsSqlServer() ? "dbo" : "public";

        // ── Schema cms ────────────────────────────────────────────────────────
        modelBuilder.Entity<News>()       .ToTable("news",        "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Category>()   .ToTable("Category",    "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Menu>()       .ToTable("Menu",        "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MenuType>()   .ToTable("MenuType",    "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Module>()     .ToTable("Module",      "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ModuleRoles>().ToTable("ModuleRoles", "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Permission>() .ToTable("Permission",  "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Photo>()      .ToTable("Photo",       "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<PhotoAlbum>() .ToTable("PhotoAlbum",  "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<AttachFile>() .ToTable("AttachFile",  "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Roles>()      .ToTable("Roles",       "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ADS>()        .ToTable("ADS",         "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ADSGroup>()   .ToTable("ADSGroup",    "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Banner>()     .ToTable("Banner",      "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Contact>()    .ToTable("Contact",     "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ContactGroup>().ToTable("ContactGroup","cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Counter>()    .ToTable("Counter",     "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Customer>()   .ToTable("Customer",    "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EventNews>()  .ToTable("EventNews",   "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<CmsItem>()    .ToTable("Item",        "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ItemType>()   .ToTable("ItemType",    "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Link>()       .ToTable("Link",        "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<LinkGroup>()  .ToTable("LinkGroup",   "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<NewsComment>().ToTable("NewsComment",  "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Page>()       .ToTable("Page",        "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Supportonline>().ToTable("Supportonline","cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Video>()      .ToTable("Video",       "cms").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);

        // ── Schema dbo ────────────────────────────────────────────────────────
        modelBuilder.Entity<Users>()  .ToTable("Users",   dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<UserLog>().ToTable("UserLog", dboSchema);
        modelBuilder.Entity<ChucVu>() .ToTable("ChucVu",  dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Class>()  .ToTable("Class",   dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ConfigImportReader>().ToTable("ConfigImportReader", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Course>() .ToTable("Course",  dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Currency>().ToTable("Currency", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Degree>() .ToTable("Degree",  dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Tenant>().ToTable("Tenant", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Ethenic>().ToTable("Ethenic",  dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<GroupReader>().ToTable("GroupReader", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<GroupUser>().ToTable("GroupUser", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Nation>() .ToTable("Nation",  dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);

        // ── Schema PrintBook ──────────────────────────────────────────────────
        modelBuilder.Entity<Z3950Config>().ToTable("Z3950Config", "PrintBook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Language>() .ToTable("Language",    "PrintBook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Org>()    .ToTable("Org",     dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Prof>()   .ToTable("Prof",    dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Reader>() .ToTable("Reader",  dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ReaderDelete>().ToTable("ReaderDelete", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ReaderInGroup>().ToTable("ReaderInGroup", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ReaderTrackingLogin>().ToTable("ReaderTrackingLogin", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ReaderType>().ToTable("ReaderType", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ReaderPreference>().ToTable("ReaderPreference", dboSchema);
        modelBuilder.Entity<ScheduledReport>().ToTable("ScheduledReport", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Sip2Log>().ToTable("Sip2Log",  dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<SystemParameter>().ToTable("systemparameter", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<NotificationChannelConfig>().ToTable("NotificationChannelConfig", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<NotificationLog>().ToTable("NotificationLog", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<SearchObservation>().ToTable("SearchObservation", dboSchema);
        modelBuilder.Entity<ReaderSavedSearch>().ToTable("ReaderSavedSearch", dboSchema).Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<ReaderWorkspace>().ToTable("ReaderWorkspace", dboSchema);
        modelBuilder.Entity<AdminTask>().ToTable("AdminTask", dboSchema);
        modelBuilder.Entity<AdminTask>().HasIndex(x => x.State);
        modelBuilder.Entity<AdminTask>().HasIndex(x => x.ActorId);
        modelBuilder.Entity<AdminTask>().HasIndex(x => x.ReviewToken);
        modelBuilder.Entity<AdminTask>().HasIndex(x => new { x.State, x.FinishedAt });
        modelBuilder.Entity<AdminTaskChunk>().ToTable("AdminTaskChunk", dboSchema)
            .HasIndex(x => new { x.TaskId, x.Position }).IsUnique();
        modelBuilder.Entity<AdminWorkerHeartbeat>().ToTable("AdminWorkerHeartbeat", dboSchema);
        modelBuilder.Entity<AdminTaskControlEvent>().ToTable("AdminTaskControlEvent", dboSchema);
        modelBuilder.Entity<AdminTaskControlEvent>().HasIndex(x => x.TaskId);
        modelBuilder.Entity<AdminTaskControlEvent>().HasIndex(x => x.RequestId).IsUnique();
        modelBuilder.Entity<AdminTaskRetentionRun>().ToTable("AdminTaskRetentionRun", dboSchema);
        modelBuilder.Entity<AdminWorkAssignment>().ToTable("AdminWorkAssignment", dboSchema)
            .HasKey(x => new { x.SourceType, x.SourcePublicId });
        modelBuilder.Entity<AdminWorkAssignment>().HasIndex(x => new { x.SourceType, x.AssigneeId, x.DueAtUtc });
        modelBuilder.Entity<AdminWorkAssignment>().HasIndex(x => x.TenantId);
        // Đợt 21 — cấu hình bảng theo tài khoản; bảng tạo bằng backend/docs/add-admin-ui-preference.sql.
        modelBuilder.Entity<AdminUiPreference>().ToTable("AdminUiPreference", dboSchema).HasKey(x => new { x.ActorId, x.PageKey });

        // ── Schema Ebook ──────────────────────────────────────────────────────
        modelBuilder.Entity<EbookCollection>().ToTable("collection","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<CollectionPermistionUser>().ToTable("CollectionPermistionUser","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<DigType>().ToTable("DigType","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookAccess>().ToTable("Ebook","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookFile>().ToTable("EbookFile","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookLog>().ToTable("EbookLog","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<IntroBookCategory>().ToTable("IntroBookCategory","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<IntroBooks>().ToTable("IntroBooks","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookItem>().ToTable("Item","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookItemXml>().ToTable("itemXml","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookItem>().HasOne(x => x.ItemXml).WithOne().HasForeignKey<EbookItemXml>(x => x.Id);
        modelBuilder.Entity<EbookItemLoan>().ToTable("ItemLoan","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookItemReservation>().ToTable("ItemReservation","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MetaDataFieldRegistery>().ToTable("MetaDataFieldRegistery","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MetadataSchemaRegistry>().ToTable("MetadataSchemaRegistry","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MetaDataValue>().ToTable("MetaDataValue","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<PolicyDigital>().ToTable("PolicyDigital","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<PolicyDigitalByCollection>().ToTable("PolicyDigitalByCollection","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookSubject>().ToTable("Subject","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<TheodoiBienmucEbook>().ToTable("TheodoiBienmucEbook","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookTopic>().ToTable("Topic","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookReview>().ToTable("Review","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<DocumentSubmission>().ToTable("DocumentSubmission","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EbookFavorite>().ToTable("EbookFavorite","Ebook").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);

        // ── Schema EOffice ────────────────────────────────────────────────────
        modelBuilder.Entity<Agency>().ToTable("Agency","EOffice").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Document>().ToTable("Document","EOffice").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<DocumentFile>().ToTable("DocumentFile","EOffice").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<DocumentType>().ToTable("DocumentType","EOffice").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EofficeTopic>().ToTable("Topic","EOffice").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);

        // ── Schema Evaluate ───────────────────────────────────────────────────
        modelBuilder.Entity<EvaluateCourse>().ToTable("Course","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<CourseOption>().ToTable("CourseOption","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EvaluateDegree>().ToTable("Degree","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<Knowledge>().ToTable("Knowledge","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MonHoc>().ToTable("MonHoc","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<NganhHoc>().ToTable("NganhHoc","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<NganhMonHoc>().ToTable("NganhMonHoc","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<EvaluateProgram>().ToTable("Program","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<TaiLieu>().ToTable("TaiLieu","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<DonVi>().ToTable("DonVi","Evaluate").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);

        // ── Schema map ────────────────────────────────────────────────────────
        modelBuilder.Entity<MapBuilding>()    .ToTable("MapBuilding",     "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MapFloor>()       .ToTable("MapFloor",        "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MapFloorUtility>().ToTable("MapFloorUtility", "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MapObject>()      .ToTable("MapObject",       "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MapShelfDetail>() .ToTable("MapShelfDetail",  "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MapShelfRow>()    .ToTable("MapShelfRow",     "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<MapEquipment>()   .ToTable("MapEquipment",    "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<RoomBooking>()       .ToTable("RoomBooking",       "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);
        modelBuilder.Entity<RoomBookingConfig>() .ToTable("RoomBookingConfig", "map").Property(e => e.PublicId).HasDefaultValueSql(uuidSql);

        // PostgreSQL: existing DB uses lowercase schema names (ebook, printbook, eoffice, evaluate)
        if (!Database.IsSqlServer())
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var schema = entityType.GetSchema();
                if (schema != null)
                    entityType.SetSchema(schema.ToLower());
            }
        }

    }
}
