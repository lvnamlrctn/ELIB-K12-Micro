/* =============================================================================
   SCRIPT: Xoá module/permission cũ, seed lại từ modules-seed.sql,
           cấp full quyền cho user lvnam và admin.

   CÁCH CHẠY: Mở SSMS → kết nối DB → chạy script này (F5 hoặc Execute).
   LƯU Ý: Script này xoá TOÀN BỘ dữ liệu cms.Permission và cms.Module.
           Đảm bảo đã backup DB trước khi chạy.
   ============================================================================= */

SET NOCOUNT ON;

-- ============================================================================
-- BƯỚC 1: Xoá permission và module cũ, reset identity
-- ============================================================================
DELETE FROM [cms].[Permission];
DELETE FROM [cms].[Module];

DBCC CHECKIDENT ('[cms].[Module]',     RESEED, 0);
DBCC CHECKIDENT ('[cms].[Permission]', RESEED, 0);

PRINT 'Đã xoá Permission và Module cũ. Bắt đầu seed...';

-- ============================================================================
-- BƯỚC 2: Insert module mới (nguồn: docs/modules-seed.sql, đổi schema dbo→cms)
-- ============================================================================

/* CẤP 0 — mục gốc (ParentId = NULL) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), NULL, N'Bảng điều khiển',    'DASHBOARD',     'dashboard',         '/admin/dashboard', 1,  1),
 (NEWID(), NULL, N'Kho tài liệu',       'DOC_STORE',     'library_books',     NULL,               2,  1),
 (NEWID(), NULL, N'Ebook',              'EBOOK',         'menu_book',         NULL,               3,  1),
 (NEWID(), NULL, N'Độc giả',            'READERS',       'people',            '/admin/readers',   4,  1),
 (NEWID(), NULL, N'Tin tức',            'NEWS',          'article',           NULL,               5,  1),
 (NEWID(), NULL, N'Bổ sung - Biên mục', 'ACQ_CATALOG',   'add_shopping_cart', NULL,               6,  1),
 (NEWID(), NULL, N'Biên mục',           'CATALOGING',    'edit_note',         NULL,               7,  1),
 (NEWID(), NULL, N'Nghiệp vụ lưu thông','CIRCULATION',   'import_export',     NULL,               8,  1),
 (NEWID(), NULL, N'Ấn phẩm định kỳ',    'SERIALS',       'auto_stories',      NULL,               9,  1),
 (NEWID(), NULL, N'Quản lý Kho',        'STORE_MGMT',    'store',             NULL,               10, 1),
 (NEWID(), NULL, N'Từ điển biên mục',   'CATALOG_DICT',  'menu_book',         NULL,               11, 1),
 (NEWID(), NULL, N'Tham số bạn đọc',    'READER_PARAMS', 'sync_alt',          NULL,               12, 1),
 (NEWID(), NULL, N'Quản lý vào ra',     'RECEIPTION',    'door_front',        NULL,               13, 1),
 (NEWID(), NULL, N'Hệ thống',           'SYSTEM',        'dns',               NULL,               14, 1),
 (NEWID(), NULL, N'Cài đặt',            'SETTINGS',      'settings',          '/admin/settings',  15, 1);

/* CẤP 1 — nhóm con Tham số (cha = ACQ_CATALOG) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='ACQ_CATALOG'), N'Tham số', 'PARAMETERS', 'tune', NULL, 1, 1);

/* LÁ — Kho tài liệu (DOC_STORE) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='DOC_STORE'), N'Tìm kiếm tài liệu',     'DOC_SEARCH',       'manage_search', '/admin/books',            1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='DOC_STORE'), N'Tài liệu số',           'DIGITAL_DOC',      'devices',       '/admin/ebooks',           2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='DOC_STORE'), N'Theo dõi đọc tài liệu', 'READING_TRACKING', 'auto_stories',  '/admin/reading-tracking', 3, 1);

/* LÁ — Ebook (EBOOK) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='EBOOK'), N'Bộ sưu tập',          'EBOOK_COLLECTION', 'collections_bookmark', '/admin/ebook-collections',       1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='EBOOK'), N'Chủ đề',              'EBOOK_SUBJECT',    'topic',                '/admin/ebook-subjects',          2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='EBOOK'), N'Ngành đào tạo',       'EBOOK_TOPIC',      'school',               '/admin/ebook-topics',            3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='EBOOK'), N'Loại tài liệu số',    'EBOOK_DIG_TYPE',   'description',          '/admin/ebook-dig-types',         4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='EBOOK'), N'Lược đồ siêu dữ liệu','METADATA_SCHEMA',  'schema',               '/admin/ebook-metadata-schemas',  5, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='EBOOK'), N'Chính sách truy cập', 'ACCESS_POLICY',    'policy',               '/admin/ebook-access-policies',   6, 1);

/* LÁ — Tin tức (NEWS) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Quản lý danh mục',        'NEWS_CATEGORIES',  'category',     '/admin/news-categories',   1,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Danh mục CMS',            'CMS_CATEGORIES',   'account_tree', '/admin/categories',        2,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Quản lý sự kiện',         'EVENTS',           'event',        '/admin/events',            3,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Quản lý tin tức',         'NEWS_MANAGE',      'article',      '/admin/news',              4,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Quản lý bộ sưu tập ảnh',  'IMAGE_COLLECTIONS','photo_album',  '/admin/image-collections', 5,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Quản lý hình ảnh',        'IMAGES',           'image',        '/admin/images',            6,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Quản lý Banner',          'BANNERS',          'view_carousel','/admin/banners',           7,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Nhóm liên kết',           'LINK_GROUP',       'folder_open',  '/admin/link-group',        8,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Liên kết',                'LINK',             'link',         '/admin/link',              9,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='NEWS'), N'Loại Menu',               'MENU_TYPES',       'list_alt',     '/admin/menu-types',        10, 1);

/* LÁ — Tham số (PARAMETERS, dưới ACQ_CATALOG) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='PARAMETERS'), N'Tiền tệ',        'CURRENCIES',   'currency_exchange', '/admin/currencies',   1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='PARAMETERS'), N'Nguồn bổ sung',  'AB_SOURCES',   'source',            '/admin/ab-sources',   2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='PARAMETERS'), N'Nhà cung cấp',   'SUPPLIERS',    'store',             '/admin/suppliers',    3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='PARAMETERS'), N'Kinh phí',       'BUDGETS',      'account_balance',   '/admin/budgets',      4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='PARAMETERS'), N'Quỹ',            'FUNDS',        'savings',           '/admin/funds',        5, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='PARAMETERS'), N'Nhóm Z3950',     'Z3950_GROUPS', 'hub',               '/admin/z3950-groups', 6, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='PARAMETERS'), N'Cấu hình Z3950', 'Z3950_CONFIGS','dns',               '/admin/z3950-configs',7, 1);

/* LÁ — Bổ sung - Biên mục (ACQ_CATALOG) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='ACQ_CATALOG'), N'Đơn đặt bổ sung', 'AB_ORDERS',     'shopping_cart',  '/admin/ab-orders',     2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='ACQ_CATALOG'), N'Phiếu nhập',      'AB_RECEIPTS',   'receipt_long',   '/admin/ab-receipts',   3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='ACQ_CATALOG'), N'Điều chuyển kho', 'AB_MOVES',      'swap_horiz',     '/admin/ab-moves',      4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='ACQ_CATALOG'), N'Người giao',      'AB_DELIVERERS', 'local_shipping', '/admin/ab-deliverers', 5, 1);

/* LÁ — Biên mục (CATALOGING) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOGING'), N'Biên mục biểu ghi', 'CATALOG_BIBS', 'menu_book',      '/admin/catalog-bibs',     1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOGING'), N'Phiếu nhập tin',    'WORKSHEETS',   'list_alt',       '/admin/worksheets',       2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOGING'), N'Loại biểu ghi',     'BIB_TYPES',    'category',       '/admin/bib-types',        3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOGING'), N'Khung phân loại',   'DIC_CLASSES',  'account_tree',   '/admin/dic-classes',      4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOGING'), N'In mã vạch',        'PRINT_BARCODE','qr_code',        '/admin/print-barcode',    5, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOGING'), N'In nhãn môn loại',  'PRINT_LABEL',  'label',          '/admin/print-spine-label',6, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOGING'), N'Tra cứu Z3950',     'Z3950_SEARCH', 'travel_explore', '/admin/z3950-search',     7, 1);

/* LÁ — Nghiệp vụ lưu thông (CIRCULATION) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CIRCULATION'), N'Mượn/Trả',            'BORROW',        'swap_vert',   '/admin/borrow',        1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CIRCULATION'), N'Yêu cầu mượn',        'REQUEST_BOOKS', 'bookmark_add','/admin/request-books', 2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CIRCULATION'), N'Lịch sử lưu thông',   'LOAN_HISTORY',  'history',     '/admin/loan-history',  3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CIRCULATION'), N'Quản lý phạt',        'FINES',         'gavel',       '/admin/fines',         4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CIRCULATION'), N'Chính sách lưu thông','CIRC_POLICIES', 'rule',        '/admin/circ-policies', 5, 1);

/* LÁ — Ấn phẩm định kỳ (SERIALS) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SERIALS'), N'Loại ấn phẩm', 'MAGAZINE_TYPES',  'auto_stories', '/admin/magazine-types',       1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SERIALS'), N'Tần suất',     'FREQUENCIES',     'schedule',     '/admin/magazine-frequencies', 2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SERIALS'), N'Mẫu đánh số',  'PATTERNS',        'tag',          '/admin/magazine-patterns',    3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SERIALS'), N'Đăng ký',      'SUBSCRIPTIONS',   'subscriptions','/admin/serial-subscriptions', 4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SERIALS'), N'Nhận kỳ',      'SERIAL_ISSUES',   'inventory_2',  '/admin/serial-issues',        5, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SERIALS'), N'Tìm phiếu nhận','SERIAL_RECEIPTS','search',       '/admin/serial-receipts',      6, 1);

/* LÁ — Quản lý Kho (STORE_MGMT) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='STORE_MGMT'), N'Loại kho',            'STORE_TYPES', 'category',               '/admin/store-types',         1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='STORE_MGMT'), N'Kho',                 'STORES',      'warehouse',              '/admin/stores',              2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='STORE_MGMT'), N'Kiểm kê',             'INVENTORY',   'inventory',              '/admin/inventory',           3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='STORE_MGMT'), N'Xử lý sách mất',      'LOST_BOOKS',  'report',                 '/admin/lost-books',          4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='STORE_MGMT'), N'Thanh lý',            'LIQUIDATES',  'delete_sweep',           '/admin/liquidates',          5, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='STORE_MGMT'), N'Đăng ký lại mã vạch', 'RE_REGISTER', 'published_with_changes', '/admin/re-register-barcode', 6, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='STORE_MGMT'), N'Thống kê tài liệu',   'STAT_DOC',    'query_stats',            '/admin/statistics-document', 7, 1);

/* LÁ — Từ điển biên mục (CATALOG_DICT) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOG_DICT'), N'Tác giả',     'DIC_AUTHORS',    'person',   '/admin/dic-authors',         1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOG_DICT'), N'Nhà xuất bản','DIC_PUBLISHERS', 'business', '/admin/dic-publishers',      2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOG_DICT'), N'Từ khóa',     'DIC_KEYWORDS',   'vpn_key',  '/admin/dic-keywords',        3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOG_DICT'), N'Ngôn ngữ',    'DIC_LANGUAGES',  'language', '/admin/dic-languages',       4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOG_DICT'), N'Quốc gia',    'DIC_COUNTRIES',  'public',   '/admin/dic-countries',       5, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='CATALOG_DICT'), N'Vùng địa lý', 'DIC_GEO',        'map',      '/admin/dic-geographic-areas',6, 1);

/* LÁ — Tham số bạn đọc (READER_PARAMS) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Quốc tịch',      'NATIONALITIES','flag',               '/admin/nationalities',   1,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Học hàm học vị', 'PROFS',        'workspace_premium',  '/admin/profs',           2,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Dân tộc',        'ETHNICS',      'diversity_3',        '/admin/ethnics',         3,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Khóa học',       'COURSES',      'school',             '/admin/courses',         4,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Lớp học',        'CLASSES',      'class',              '/admin/academic-classes',5,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Trình độ',       'DEGREES',      'school',             '/admin/degrees',         6,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Loại bạn đọc',   'READER_TYPES', 'local_library',      '/admin/reader-types',    7,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Chức vụ',        'POSITIONS',    'badge',              '/admin/chuc-vus',        8,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Phòng ban',      'ORGS',         'account_tree',       '/admin/orgs',            9,  1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Điểm lưu thông', 'CIRC_PLACES',  'place',              '/admin/circ-places',     10, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='READER_PARAMS'), N'Lý do phạt',     'FINE_REASONS', 'gavel',              '/admin/cfine-types',     11, 1);

/* LÁ — Quản lý vào ra (RECEIPTION) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='RECEIPTION'), N'Check-in / Check-out','CHECK_IN_OUT',      'login',                    '/admin/check-in-out',      1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='RECEIPTION'), N'Lịch sử vào ra',      'CHECKIN_HISTORY',   'history',                  '/admin/checkin-history',   2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='RECEIPTION'), N'Thống kê vào ra',     'CHECKIN_STATS',     'query_stats',              '/admin/checkin-statistics',3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='RECEIPTION'), N'Báo cáo vào ra',      'RECEIPTION_REPORT', 'summarize',                '/admin/receiption-report', 4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='RECEIPTION'), N'Mượn chìa khóa',      'BORROW_KEYS',       'vpn_key',                  '/admin/borrow-keys',       5, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='RECEIPTION'), N'Tra cứu mượn khóa',   'SEARCH_BORROW_KEYS','manage_search',            '/admin/search-borrow-keys',6, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='RECEIPTION'), N'Cấu hình vào ra',     'CONFIG_RECEIPTION', 'settings_input_component', '/admin/config-receiption', 7, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='RECEIPTION'), N'Quản lý tủ đựng đồ',  'CABINETS',          'door_back',                '/admin/cabinets',          8, 1);

/* LÁ — Hệ thống (SYSTEM) */
INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status) VALUES
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SYSTEM'), N'Người dùng',        'USERS',         'manage_accounts',       '/admin/users',             1, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SYSTEM'), N'Đơn vị',            'DEPARTMENTS',   'corporate_fare',        '/admin/departments',       2, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SYSTEM'), N'Phân hệ (Module)',  'MODULES',       'layers',                '/admin/modules',           3, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SYSTEM'), N'Tham số hệ thống',  'SYSTEM_PARAMS', 'settings_applications', '/admin/system-parameters', 4, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SYSTEM'), N'Phân quyền (Roles)','ROLES',         'admin_panel_settings',  '/admin/roles',             5, 1),
 (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode='SYSTEM'), N'Theo dõi hệ thống', 'SYSTEM_LOG',    'manage_history',        '/admin/system-log',        6, 1);

DECLARE @modCount INT = (SELECT COUNT(*) FROM [cms].[Module]);
PRINT CONCAT('Đã insert ', @modCount, ' module.');

-- ============================================================================
-- BƯỚC 3: Cấp full quyền cho user lvnam và admin
-- ============================================================================
INSERT INTO [cms].[Permission]
    (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT
    NEWID(),
    u.Id,
    m.Id,
    2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (
    SELECT Id FROM [dbo].[Users]
    WHERE LoginName IN ('lvnam', 'admin')
      AND IsDelete != 2
) u;

DECLARE @permCount INT = (SELECT COUNT(*) FROM [cms].[Permission]);
PRINT CONCAT('Đã cấp ', @permCount, ' quyền cho user lvnam và admin.');

-- ============================================================================
-- Kiểm tra kết quả
-- ============================================================================
SELECT 'Module count' AS [Check], COUNT(*) AS [Value] FROM [cms].[Module]
UNION ALL
SELECT 'Permission count',         COUNT(*)             FROM [cms].[Permission]
UNION ALL
SELECT 'Users được cấp quyền',    COUNT(DISTINCT UserId) FROM [cms].[Permission];
