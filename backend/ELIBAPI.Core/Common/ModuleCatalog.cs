namespace ELIBAPI.Core.Common;

/// <summary>
/// Danh sách chuẩn cms.Module khớp 1:1 với menu quản trị hiện tại (frontend/src/app/services/menu.ts),
/// dùng cho nút "Đồng bộ Module" (ModuleController.SyncDiff / SyncApply).
///
/// Quy tắc xây dựng danh sách này (xem thêm ghi chú tại từng dòng khi có ngoại lệ):
///  - KHÔNG đưa vào các mục đang bị comment "TẠM ẨN" trong menu.ts (tính năng cố tình tắt).
///  - KHÔNG đưa vào các mục tái dùng quyền của module khác — qua field `permLink` của menu.ts,
///    qua override cứng `*appCan="'action'; url: '/admin/xxx'"` ngay trong .html của trang, hoặc
///    (phát hiện khi verify) vì controller thật của trang đó không có code [Permission] riêng mà
///    dùng chung code của module khác — các mục này không cần dòng catalog riêng.
///  - ModuleCode của từng mục đã được xác minh trực tiếp bằng cách tra [Permission("CODE", ...)]
///    ở đúng controller mà trang đó gọi (không đoán từ tên khoá i18n) — các trường hợp mã thật
///    khác với tên khoá i18n được ghi chú ngay tại dòng đó.
///  - Entry cha (nhóm menu không có link riêng) vẫn có 1 dòng catalog (Link = null) để entry con
///    tham chiếu qua ParentCode — Code của nhóm không nhất thiết ứng với 1 [Permission] cụ thể nào
///    (chỉ là node tổ chức cây), điều này là bình thường.
///
/// Danh sách liệt kê CHA TRƯỚC CON để SyncApply có thể gán ParentId đúng trong 1 lượt chạy.
/// </summary>
public static class ModuleCatalog
{
    public sealed record Entry(string Code, string Name, string Icon, string? Link, string? ParentCode);

    public static readonly IReadOnlyList<Entry> All = BuildFlat();

    private static List<Entry> BuildFlat()
    {
        var list = new List<Entry>
        {
            new("DASHBOARD", "Bảng điều khiển", "dashboard", "/admin/dashboard", null),

            new("EBOOK", "Tài liệu số", "menu_book", null, null),
            new("DIGITAL_DOC", "Biên mục tài liệu số", "devices", "/admin/ebooks", "EBOOK"),
            new("READING_TRACKING", "Theo dõi đọc tài liệu", "trending_up", "/admin/reading-tracking", "EBOOK"),
            new("EBOOK_COLLECTION", "Bộ sưu tập", "collections_bookmark", "/admin/ebook-collections", "EBOOK"),
            new("EBOOK_SUBJECT", "Chủ đề", "topic", "/admin/ebook-subjects", "EBOOK"),
            // EBOOK_LOAN_MANAGE (/admin/ebook-loan-manage) KHÔNG có dòng riêng: nút duy nhất của trang
            // (thu hồi lượt mượn) override cứng url:'/admin/ebooks' — xem ebook-loan-manage.html:67.
            // ACCESS_STATS (/admin/reading-tracking) KHÔNG có dòng riêng: menu.ts khai literal cùng
            // Link '/admin/reading-tracking' như READING_TRACKING (menu.ts:36-37) — cùng 1 route.

            new("NEWS", "Tin tức", "article", null, null),
            // CMS_CATEGORIES (tiêu đề menu) thật ra dùng code NEWS_CATEGORIES ở CategoryController.cs
            // (route /admin/categories) — NEWS_CATEGORIES "gốc" (/admin/news-categories) đang TẠM ẨN.
            new("NEWS_CATEGORIES", "Danh mục CMS", "account_tree", "/admin/categories", "NEWS"),
            new("EVENTS", "Quản lý sự kiện", "event", "/admin/events", "NEWS"),
            new("NEWS_MANAGE", "Quản lý tin tức", "article", "/admin/news", "NEWS"),
            new("BANNERS", "Quản lý Banner", "view_carousel", "/admin/banners", "NEWS"),
            new("LINK_GROUP", "Nhóm liên kết", "folder_open", "/admin/link-group", "NEWS"),
            new("LINK", "Liên kết", "link", "/admin/link", "NEWS"),
            new("MENU_TYPES", "Loại Menu", "list_alt", "/admin/menu-types", "NEWS"),
            new("PAGE", "Trang tĩnh", "pages", "/admin/page", "NEWS"),
            new("VIDEO", "Video", "video_library", "/admin/video", "NEWS"),
            new("CONTACT", "Liên hệ", "contact_mail", "/admin/contact", "NEWS"),
            new("CONTACTGROUP", "Nhóm liên hệ", "contacts", "/admin/contactgroup", "NEWS"),

            new("ACQ_CATALOG", "Bổ sung - Biên mục", "add_shopping_cart", null, null),
            new("PARAMETERS", "Tham số", "tune", null, "ACQ_CATALOG"),
            new("CURRENCIES", "Tiền tệ", "currency_exchange", "/admin/currencies", "PARAMETERS"),
            new("AB_SOURCES", "Nguồn bổ sung", "source", "/admin/ab-sources", "PARAMETERS"),
            new("SUPPLIERS", "Nhà cung cấp", "store", "/admin/suppliers", "PARAMETERS"),
            new("BUDGETS", "Kinh phí", "account_balance", "/admin/budgets", "PARAMETERS"),
            new("FUNDS", "Quỹ", "savings", "/admin/funds", "PARAMETERS"),
            new("Z3950_GROUPS", "Nhóm Z3950", "hub", "/admin/z3950-groups", "PARAMETERS"),
            new("Z3950_CONFIGS", "Cấu hình Z3950", "dns", "/admin/z3950-configs", "PARAMETERS"),
            new("AB_ORDERS", "Đơn đặt bổ sung", "shopping_cart", "/admin/ab-orders", "ACQ_CATALOG"),
            new("AB_RECEIPTS", "Đơn nhận", "receipt_long", "/admin/ab-receipts", "ACQ_CATALOG"),
            // AB_RECEIPTS_LOOKUP (/admin/ab-receipts/lookup) KHÔNG có dòng riêng: cả 3 endpoint nó gọi
            // đều dùng chung [Permission("AB_RECEIPTS", ...)] — không có code riêng để xác minh.
            new("AB_MOVES", "Điều chuyển kho", "swap_horiz", "/admin/ab-moves", "ACQ_CATALOG"),
            new("AB_DELIVERERS", "Phân bổ tài liệu", "local_shipping", "/admin/ab-deliverers", "ACQ_CATALOG"),
            // AB_DELIVERERS_RECEIVE có permLink: '/admin/ab-deliverers' (menu.ts) — dùng chung AB_DELIVERERS.

            new("CATALOGING", "Biên mục", "edit_note", null, null),
            new("CATALOG_BIBS", "Biên mục biểu ghi", "menu_book", "/admin/catalog-bibs", "CATALOGING"),
            new("WORKSHEETS", "Biểu mẫu biên mục", "list_alt", "/admin/worksheets", "CATALOGING"),
            new("BIB_TYPES", "Loại biểu ghi", "category", "/admin/bib-types", "CATALOGING"),
            new("DIC_CLASSES", "Khung phân loại", "account_tree", "/admin/dic-classes", "CATALOGING"),
            // PRINT_BARCODE/PRINT_LABEL: trang 100% xử lý phía client (in mã vạch/nhãn), KHÔNG gọi API
            // backend nào — không có [Permission] nào để xác minh. Vẫn thêm dòng catalog (mã đoán theo
            // tên) để sidebar/nút vẫn có thể ẩn/hiện theo Link như các module khác; chưa có gate quyền
            // phía server tương ứng cho tới khi (nếu) backend bổ sung.
            new("PRINT_BARCODE", "In mã vạch", "qr_code", "/admin/print-barcode", "CATALOGING"),
            new("PRINT_LABEL", "In nhãn môn loại", "label", "/admin/print-spine-label", "CATALOGING"),
            // Z3950_SEARCH (/admin/z3950-search) KHÔNG có dòng riêng: OpacZ3950Controller.cs dùng
            // [Permission("AB_RECEIPTS", ...)] trên Search/Marc/Import — nhiều khả năng là lỗi copy-paste
            // ở backend (không liên quan Đơn nhận), NHƯNG nằm ngoài phạm vi sửa của task này.
            new("MARC_DICTIONARY", "Từ điển MARC", "account_tree", "/admin/marc-dictionary", "CATALOGING"),
            // MARC_DICTIONARY: MarcFieldController & MarcIndicatorController dùng đúng "MARC_DICTIONARY";
            // riêng MarcSubFieldController (cùng trang) lại dùng "AB_RECEIPTS" — lỗi backend, ngoài phạm vi sửa.
            new("CONFIG_ISBD", "Cấu hình ISBD", "format_quote", "/admin/config-isbd", "CATALOGING"),

            new("CIRCULATION", "Quản lý bạn đọc", "people", null, null),
            new("BORROW", "Mượn/Trả", "swap_vert", "/admin/borrow", "CIRCULATION"),
            new("REQUEST_BOOKS", "Yêu cầu mượn", "bookmark_add", "/admin/request-books", "CIRCULATION"),
            new("LOAN_HISTORY", "Lịch sử lưu thông", "history", "/admin/loan-history", "CIRCULATION"),
            new("CIRC_REPORT", "Báo cáo lưu thông", "bar_chart", "/admin/circulation-report", "CIRCULATION"),
            new("FINES", "Quản lý phạt", "gavel", "/admin/fines", "CIRCULATION"),
            // FINE_TICKETS có permLink: '/admin/fines' (menu.ts) — dùng chung FINES, không có dòng riêng.
            // PHOTOCOPY (tiêu đề menu) thật ra dùng code C_PHOTO ở CPhotoController.cs.
            new("C_PHOTO", "Photo copy tài liệu", "print", "/admin/photocopy", "CIRCULATION"),
            new("CIRC_POLICIES", "Chính sách lưu thông", "rule", "/admin/circ-policies", "CIRCULATION"),
            new("READERS", "Bạn đọc", "people", "/admin/readers", "CIRCULATION"),
            new("READER_PARAMS", "Tham số bạn đọc", "tune", null, "CIRCULATION"),
            new("COURSES", "Khóa học", "school", "/admin/courses", "READER_PARAMS"),
            new("CLASSES", "Lớp học", "class", "/admin/academic-classes", "READER_PARAMS"),
            new("DEGREES", "Trình độ", "school", "/admin/degrees", "READER_PARAMS"),
            new("READER_TYPES", "Loại bạn đọc", "local_library", "/admin/reader-types", "READER_PARAMS"),
            new("POSITIONS", "Chức vụ", "badge", "/admin/chuc-vus", "READER_PARAMS"),
            new("ORGS", "Phòng ban", "account_tree", "/admin/orgs", "READER_PARAMS"),
            new("CIRC_PLACES", "Điểm lưu thông", "place", "/admin/circ-places", "READER_PARAMS"),
            new("FINE_REASONS", "Lý do phạt", "gavel", "/admin/cfine-types", "READER_PARAMS"),
            // C_FINE_METHOD có permLink: '/admin/cfine-types' (menu.ts) — dùng chung FINE_REASONS.

            new("SERIALS", "Ấn phẩm định kỳ", "auto_stories", null, null),
            new("MAGAZINE_TYPES", "Loại ấn phẩm", "auto_stories", "/admin/magazine-types", "SERIALS"),
            new("FREQUENCIES", "Tần suất", "schedule", "/admin/magazine-frequencies", "SERIALS"),
            new("PATTERNS", "Mẫu đánh số", "tag", "/admin/magazine-patterns", "SERIALS"),
            new("SUBSCRIPTIONS", "Đăng ký", "subscriptions", "/admin/serial-subscriptions", "SERIALS"),
            // SERIAL_ISSUES (/admin/serial-issues) và SERIAL_RECEIPTS (/admin/serial-receipts) KHÔNG có
            // dòng riêng: cả hai đều gọi MagazineSerialController với [Permission("SUBSCRIPTIONS", ...)] —
            // không tồn tại code SERIAL_ISSUES / SERIAL_RECEIPTS nào ở backend.

            new("STORE_MGMT", "Quản lý Kho", "store", null, null),
            new("DOC_SEARCH", "Tìm kiếm tài liệu", "manage_search", "/admin/books", "STORE_MGMT"),
            new("STORE_TYPES", "Loại kho", "category", "/admin/store-types", "STORE_MGMT"),
            new("STORES", "Kho", "warehouse", "/admin/stores", "STORE_MGMT"),
            new("INVENTORY", "Kiểm kê", "inventory", "/admin/inventory", "STORE_MGMT"),
            // SHELVING (/admin/shelving) KHÔNG có dòng catalog riêng: StoreShelvingController.cs từng
            // rỗng — 0 byte, khiến ShelvingService gọi api/PrintBook/Store/SearchUnshelved và /Shelve
            // ra lỗi 404 ngầm (đã sửa, port lại từ ELIB-LRC + thêm lọc TenantId). Controller mới dùng
            // [Permission("MAP_SHELVING", ...)] — dùng chung quyền với StoreMapShelvingController
            // (2 màn hình cùng 1 nghiệp vụ "xếp giá", chỉ khác kiểu giao diện danh sách/sơ đồ), đúng
            // theo quy ước ELIB-LRC đã dùng — không cần dòng catalog SHELVING riêng.
            new("MAP_SHELVING", "Xếp giá theo sơ đồ", "grid_view", "/admin/map-shelving", "STORE_MGMT"),
            new("BOOK_OUT_STORE", "Sách ra vào kho", "qr_code_scanner", "/admin/book-out-store", "STORE_MGMT"),
            new("EXPORT_REASON", "Lý do xuất kho", "list_alt", "/admin/export-reason", "STORE_MGMT"),
            new("BOOK_OUT_UNIT", "Đơn vị nhận sách", "apartment", "/admin/book-out-unit", "STORE_MGMT"),
            new("EXHIBITION_LOCATION", "Địa điểm triển lãm", "location_on", "/admin/exhibition-location", "STORE_MGMT"),
            new("LOST_BOOKS", "Xử lý sách mất", "report", "/admin/lost-books", "STORE_MGMT"),
            new("LIQUIDATES", "Thanh lý", "delete_sweep", "/admin/liquidates", "STORE_MGMT"),
            new("RE_REGISTER", "Đăng ký lại mã vạch", "published_with_changes", "/admin/re-register-barcode", "STORE_MGMT"),
            new("STAT_DOC", "Thống kê tài liệu", "query_stats", "/admin/statistics-document", "STORE_MGMT"),
            new("STORE_BOOK_REPORT", "Báo cáo sách trong kho", "menu_book", "/admin/store-book-report", "STORE_MGMT"),
            new("MAP_BUILDING", "Toà nhà", "apartment", "/admin/map-buildings", "STORE_MGMT"),
            new("MAP_FLOOR", "Tầng", "layers", "/admin/map-floors", "STORE_MGMT"),
            new("MAP_OBJECT", "Đối tượng/Kệ sách", "category", "/admin/map-objects", "STORE_MGMT"),
            // MAP_VISUAL (/admin/map-visual) KHÔNG có dòng riêng: trang chỉ kéo-thả vị trí, thao tác ghi
            // duy nhất gọi MapObjectController.Update ([Permission("MAP_OBJECT", "edit")]) — dùng chung MAP_OBJECT.

            new("CATALOG_DICT", "Từ điển biên mục", "menu_book", null, null),
            new("DIC_AUTHORS", "Tác giả", "person", "/admin/dic-authors", "CATALOG_DICT"),
            new("DIC_PUBLISHERS", "Nhà xuất bản", "business", "/admin/dic-publishers", "CATALOG_DICT"),
            new("DIC_KEYWORDS", "Từ khóa", "vpn_key", "/admin/dic-keywords", "CATALOG_DICT"),
            new("DIC_LANGUAGES", "Ngôn ngữ", "language", "/admin/dic-languages", "CATALOG_DICT"),
            new("DIC_COUNTRIES", "Quốc gia", "public", "/admin/dic-countries", "CATALOG_DICT"),
            new("DIC_GEO", "Vùng địa lý", "map", "/admin/dic-geographic-areas", "CATALOG_DICT"),

            new("RECEIPTION", "Quản lý vào ra", "door_front", null, null),
            new("CHECK_IN_OUT", "Check-in / Check-out", "login", "/admin/check-in-out", "RECEIPTION"),
            // CHECKIN_HISTORY (/admin/checkin-history) KHÔNG có dòng riêng: CheckInService dùng chung
            // ReceiptionCheckController ([Permission("CHECK_IN_OUT", ...)]) — dùng chung CHECK_IN_OUT.
            new("CHECKIN_STATS", "Thống kê vào ra", "query_stats", "/admin/checkin-statistics", "RECEIPTION"),
            new("RECEIPTION_REPORT", "Báo cáo vào ra", "summarize", "/admin/receiption-report", "RECEIPTION"),
            new("BORROW_KEYS", "Mượn chìa khóa", "vpn_key", "/admin/borrow-keys", "RECEIPTION"),
            // SEARCH_BORROW_KEYS (/admin/search-borrow-keys) KHÔNG có dòng riêng: BorrowKeyService.search()
            // dùng chung ReceiptionBorrowKeyController ([Permission("BORROW_KEYS", "view")]).
            new("CONFIG_RECEIPTION", "Cấu hình vào ra", "settings_input_component", "/admin/config-receiption", "RECEIPTION"),
            new("CABINETS", "Quản lý tủ đựng đồ", "door_back", "/admin/cabinets", "RECEIPTION"),

            new("SUBJECT_DB", "Cơ sở dữ liệu môn học", "school", null, null),
            new("KNOWLEDGE", "Thiết lập khối kiến thức", "psychology", "/admin/subject-knowledge", "SUBJECT_DB"),
            // NGANH (tiêu đề menu) thật ra dùng code NGANHHOC (không dấu gạch dưới) ở NganhHocController.cs.
            new("NGANHHOC", "Thiết lập ngành học", "account_tree", "/admin/subject-majors", "SUBJECT_DB"),
            // EVALUATE_DEGREE (tiêu đề menu) thật ra dùng code EVALUATEDEGREE ở EvaluateDegreeController.cs.
            new("EVALUATEDEGREE", "Thiết lập Trình độ", "workspace_premium", "/admin/subject-degrees", "SUBJECT_DB"),
            // EVALUATE_PROGRAM (tiêu đề menu) thật ra dùng code EVALUATEPROGRAM ở EvaluateProgramController.cs.
            new("EVALUATEPROGRAM", "Quản lý chương trình đào tạo", "assignment", "/admin/subject-programs", "SUBJECT_DB"),
            new("MONHOC", "Thiết lập môn học", "menu_book", "/admin/subjects", "SUBJECT_DB"),

            new("SYSTEM", "Hệ thống", "dns", null, null),
            new("USERS", "Cán bộ quản lý thư viện", "manage_accounts", "/admin/users", "SYSTEM"),
            // TENANTS (/admin/tenant) KHÔNG có dòng riêng: tenant.html override cứng cả 3 nút
            // add/edit/delete sang url:'/admin/departments' (tenant.html:22,51,52) — dùng chung module
            // Departments đã có sẵn (DEPARTMENTS đang TẠM ẨN khỏi menu.ts nhưng module cũ vẫn tồn tại).
            new("SYSTEM_PARAMS", "Tham số hệ thống", "settings_applications", "/admin/system-parameters", "SYSTEM"),
            new("DOC_UNIT_REPORT", "Báo cáo tài liệu theo đơn vị", "summarize", "/admin/document-unit-report", "SYSTEM"),
            new("SYSTEM_LOG", "Theo dõi hệ thống", "manage_history", "/admin/system-log", "SYSTEM"),
            new("SCHEDULED_REPORT", "Báo cáo định kỳ", "schedule_send", "/admin/scheduled-report", "SYSTEM"),
        };
        return list;
    }
}
