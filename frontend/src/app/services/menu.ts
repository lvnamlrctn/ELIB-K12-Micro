import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, Observable } from 'rxjs';

export interface MenuItem {
  title: string;
  icon?: string;
  link?: string;
  /** Tra quyền theo URL này thay cho `link` khi trang dùng chung permission với module khác. */
  permLink?: string;
  /** Mã quyền (ModuleCode) chỉ định thẳng; nếu có sẽ dùng thay cho việc suy từ link/permLink. */
  perm?: string;
  children?: MenuItem[];
  expanded?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class Menu {
  private http = inject(HttpClient);

  getMenu(): Observable<MenuItem[]> {
    // title = khóa i18n (MENU.*) — sidebar áp `| translate`. Bản dịch ở public/i18n/{vi,en}.json mục MENU.
    const mockMenu: MenuItem[] = [
      {
        title: 'MENU.DASHBOARD',
        icon: 'dashboard',
        link: '/admin/dashboard'
      },
      {
        title: 'MENU.EBOOK',
        icon: 'menu_book',
        expanded: false,
        children: [
          { title: 'MENU.DIGITAL_DOC',      icon: 'devices',              link: '/admin/ebooks' },
          { title: 'MENU.EBOOK_LOAN_MANAGE', icon: 'assignment_turned_in', link: '/admin/ebook-loan-manage' },
          { title: 'MENU.READING_TRACKING', icon: 'trending_up',          link: '/admin/reading-tracking' },
          { title: 'MENU.ACCESS_STATS',     icon: 'query_stats',          link: '/admin/reading-tracking' },
          /* TẠM ẨN — Quản lý bình luận
          { title: 'MENU.EBOOK_REVIEW',     icon: 'reviews',              link: '/admin/ebook-review' },
          */
          { title: 'MENU.EBOOK_COLLECTION', icon: 'collections_bookmark', link: '/admin/ebook-collections' },
          { title: 'MENU.EBOOK_SUBJECT',    icon: 'topic',                link: '/admin/ebook-subjects' }
          /* TẠM ẨN — Ngành đào tạo
          ,{ title: 'MENU.EBOOK_TOPIC',      icon: 'school',               link: '/admin/ebook-topics' },
          */
          /* TẠM ẨN — Loại tài liệu số
          ,{ title: 'MENU.EBOOK_DIG_TYPE',   icon: 'description',          link: '/admin/ebook-dig-types' }
          */
          /* TẠM ẨN — Lược đồ siêu dữ liệu, Chính sách truy cập
          ,{ title: 'MENU.METADATA_SCHEMA',  icon: 'schema',               link: '/admin/ebook-metadata-schemas' },
          { title: 'MENU.ACCESS_POLICY',    icon: 'policy',               link: '/admin/ebook-access-policies' }
          */
        ]
      },
      {
        title: 'MENU.NEWS',
        icon: 'article',
        expanded: false,
        children: [
          /* TẠM ẨN — Quản lý danh mục
          { title: 'MENU.NEWS_CATEGORIES', icon: 'category', link: '/admin/news-categories' },
          */
          { title: 'MENU.CMS_CATEGORIES', icon: 'account_tree', link: '/admin/categories' },
          { title: 'MENU.EVENTS', icon: 'event', link: '/admin/events' },
          { title: 'MENU.NEWS_MANAGE', icon: 'article', link: '/admin/news' },
          /* TẠM ẨN — Quản lý bộ sưu tập ảnh, Quản lý hình ảnh
          { title: 'MENU.IMAGE_COLLECTIONS', icon: 'photo_album', link: '/admin/image-collections' },
          { title: 'MENU.IMAGES', icon: 'image', link: '/admin/images' },
          */
          { title: 'MENU.BANNERS', icon: 'view_carousel', link: '/admin/banners' },
          { title: 'MENU.LINK_GROUP', icon: 'folder_open', link: '/admin/link-group' },
          { title: 'MENU.LINK', icon: 'link', link: '/admin/link' },
          { title: 'MENU.MENU_TYPES', icon: 'list_alt', link: '/admin/menu-types' },
          { title: 'MENU.PAGE', icon: 'pages', link: '/admin/page' },
          { title: 'MENU.VIDEO', icon: 'video_library', link: '/admin/video' },
          { title: 'MENU.CONTACT', icon: 'contact_mail', link: '/admin/contact' },
          { title: 'MENU.CONTACTGROUP', icon: 'contacts', link: '/admin/contactgroup' }
        ]
      },
      {
        title: 'MENU.ACQ_CATALOG',
        icon: 'add_shopping_cart',
        expanded: false,
        children: [
          {
            title: 'MENU.PARAMETERS',
            icon: 'tune',
            expanded: false,
            children: [
              { title: 'MENU.CURRENCIES',   icon: 'currency_exchange', link: '/admin/currencies' },
              { title: 'MENU.AB_SOURCES',   icon: 'source',            link: '/admin/ab-sources' },
              { title: 'MENU.SUPPLIERS',    icon: 'store',             link: '/admin/suppliers' },
              { title: 'MENU.BUDGETS',      icon: 'account_balance',   link: '/admin/budgets' },
              { title: 'MENU.FUNDS',        icon: 'savings',           link: '/admin/funds' },
              { title: 'MENU.Z3950_GROUPS', icon: 'hub',               link: '/admin/z3950-groups' },
              { title: 'MENU.Z3950_CONFIGS', icon: 'dns',              link: '/admin/z3950-configs' }
            ]
          },
          { title: 'MENU.AB_ORDERS',    icon: 'shopping_cart',  link: '/admin/ab-orders' },
          { title: 'MENU.AB_RECEIPTS',  icon: 'receipt_long',  link: '/admin/ab-receipts' },
          { title: 'MENU.AB_RECEIPTS_LOOKUP', icon: 'manage_search', link: '/admin/ab-receipts/lookup' },
          { title: 'MENU.AB_MOVES',     icon: 'swap_horiz',    link: '/admin/ab-moves' },
          { title: 'MENU.AB_DELIVERERS', icon: 'local_shipping', link: '/admin/ab-deliverers' },
          { title: 'MENU.AB_DELIVERERS_RECEIVE', icon: 'how_to_reg', link: '/admin/ab-deliverers-receive', permLink: '/admin/ab-deliverers' }
        ]
      },
      {
        title: 'MENU.CATALOGING',
        icon: 'edit_note',
        expanded: false,
        children: [
          { title: 'MENU.CATALOG_BIBS', icon: 'menu_book',   link: '/admin/catalog-bibs' },
          // Đợt 21 — không có module riêng: quyền theo từng quy tắc phía server; hiện menu theo quyền Biên mục.
          { title: 'MENU.DATA_QUALITY', icon: 'rule',       link: '/admin/data-quality', permLink: '/admin/catalog-bibs' },
          { title: 'MENU.WORKSHEETS',   icon: 'list_alt',     link: '/admin/worksheets' },
          { title: 'MENU.BIB_TYPES',    icon: 'category',     link: '/admin/bib-types' },
          { title: 'MENU.DIC_CLASSES',  icon: 'account_tree', link: '/admin/dic-classes' },
          { title: 'MENU.PRINT_BARCODE', icon: 'qr_code',     link: '/admin/print-barcode' },
          { title: 'MENU.PRINT_LABEL',  icon: 'label',        link: '/admin/print-spine-label' },
          { title: 'MENU.Z3950_SEARCH', icon: 'travel_explore', link: '/admin/z3950-search' },
          // Đợt 26 — không có module quyền riêng, dùng chung quyền Biên mục như data-quality.
          { title: 'MENU.Z3950_SERVER', icon: 'settings_ethernet', link: '/admin/z3950-server', permLink: '/admin/catalog-bibs' },
          { title: 'MENU.MARC_DICTIONARY', icon: 'account_tree', link: '/admin/marc-dictionary' },
          { title: 'MENU.CONFIG_ISBD', icon: 'format_quote', link: '/admin/config-isbd' }
        ]
      },
      {
        title: 'MENU.CIRCULATION',
        icon: 'people',
        expanded: false,
        children: [
          { title: 'MENU.BORROW',        icon: 'swap_vert',   link: '/admin/borrow' },
          { title: 'MENU.REQUEST_BOOKS', icon: 'bookmark_add', link: '/admin/request-books' },
          { title: 'MENU.LOAN_HISTORY',  icon: 'history',     link: '/admin/loan-history' },
          { title: 'MENU.CIRC_REPORT',   icon: 'bar_chart',   link: '/admin/circulation-report' },
          { title: 'MENU.FINES',         icon: 'gavel',       link: '/admin/fines' },
          { title: 'MENU.FINE_TICKETS',  icon: 'receipt_long', link: '/admin/fine-tickets', permLink: '/admin/fines' },
          { title: 'MENU.PHOTOCOPY',     icon: 'print',       link: '/admin/photocopy' },
          // Tạm ẩn 2026-09-24 theo ELIB-LRC (09-23) — bỏ comment để hiện lại.
          // { title: 'MENU.CIRC_POLICIES', icon: 'rule',        link: '/admin/circ-policies' },
          { title: 'MENU.READERS',       icon: 'people',      link: '/admin/readers' },
          { title: 'MENU.BADGES',        icon: 'military_tech', link: '/admin/badges' },
          { title: 'MENU.C_FINE_METHOD', icon: 'payments',    link: '/admin/c-fine-method', permLink: '/admin/cfine-types' },
          {
            title: 'MENU.READER_PARAMS',
            icon: 'tune',
            expanded: false,
            children: [
              /* TẠM ẨN — Quốc tịch, Học hàm học vị, Dân tộc
              { title: 'MENU.NATIONALITIES', icon: 'flag', link: '/admin/nationalities' },
              { title: 'MENU.PROFS', icon: 'workspace_premium', link: '/admin/profs' },
              { title: 'MENU.ETHNICS', icon: 'diversity_3', link: '/admin/ethnics' },
              */
              { title: 'MENU.COURSES', icon: 'school', link: '/admin/courses' },
              { title: 'MENU.CLASSES', icon: 'class', link: '/admin/academic-classes' },
              { title: 'MENU.DEGREES', icon: 'school', link: '/admin/degrees' },
              { title: 'MENU.READER_TYPES', icon: 'local_library', link: '/admin/reader-types' },
              { title: 'MENU.POSITIONS',   icon: 'badge',          link: '/admin/chuc-vus' },
              { title: 'MENU.ORGS',         icon: 'account_tree',  link: '/admin/orgs' },
              { title: 'MENU.CIRC_PLACES', icon: 'place',          link: '/admin/circ-places' },
              { title: 'MENU.FINE_REASONS', icon: 'gavel',          link: '/admin/cfine-types' }
            ]
          }
        ]
      },
      {
        title: 'MENU.SERIALS',
        icon: 'auto_stories',
        expanded: false,
        children: [
          { title: 'MENU.MAGAZINE_TYPES', icon: 'auto_stories', link: '/admin/magazine-types' },
          { title: 'MENU.FREQUENCIES',    icon: 'schedule',     link: '/admin/magazine-frequencies' },
          { title: 'MENU.PATTERNS',       icon: 'tag',          link: '/admin/magazine-patterns' },
          { title: 'MENU.SUBSCRIPTIONS',  icon: 'subscriptions', link: '/admin/serial-subscriptions' },
          { title: 'MENU.SERIAL_ISSUES',  icon: 'inventory_2',  link: '/admin/serial-issues' },
          { title: 'MENU.SERIAL_RECEIPTS', icon: 'search',      link: '/admin/serial-receipts' }
        ]
      },
      {
        title: 'MENU.STORE_MGMT',
        icon: 'store',
        expanded: false,
        children: [
          { title: 'MENU.DOC_SEARCH',  icon: 'manage_search', link: '/admin/books' },
          { title: 'MENU.STORE_TYPES', icon: 'category',  link: '/admin/store-types' },
          { title: 'MENU.STORES',      icon: 'warehouse',  link: '/admin/stores' },
          { title: 'MENU.INVENTORY',       icon: 'inventory',     link: '/admin/inventory' },
          // /admin/shelving không có module quyền riêng — dùng chung MAP_SHELVING (như StoreShelvingController),
          // thiếu permLink thì tài khoản phân quyền thật không thấy menu (port ELIB-LRC 09-22).
          { title: 'MENU.SHELVING',        icon: 'move_to_inbox', link: '/admin/shelving', permLink: '/admin/map-shelving' },
          { title: 'MENU.MAP_SHELVING',    icon: 'grid_view',     link: '/admin/map-shelving' },
          { title: 'MENU.BOOK_OUT_STORE',  icon: 'qr_code_scanner', link: '/admin/book-out-store' },
          { title: 'MENU.EXPORT_REASON',   icon: 'list_alt',      link: '/admin/export-reason' },
          { title: 'MENU.BOOK_OUT_UNIT',   icon: 'apartment',     link: '/admin/book-out-unit' },
          { title: 'MENU.EXHIBITION_LOCATION', icon: 'location_on', link: '/admin/exhibition-location' },
          { title: 'MENU.LOST_BOOKS',      icon: 'report',        link: '/admin/lost-books' },
          { title: 'MENU.LIQUIDATES',      icon: 'delete_sweep',  link: '/admin/liquidates' },
          { title: 'MENU.RE_REGISTER',     icon: 'published_with_changes', link: '/admin/re-register-barcode' },
          { title: 'MENU.STAT_DOC',        icon: 'query_stats',   link: '/admin/statistics-document' },
          { title: 'MENU.STORE_BOOK_REPORT', icon: 'menu_book',   link: '/admin/store-book-report' },
          { title: 'MENU.MAP_BUILDING', icon: 'apartment', link: '/admin/map-buildings' },
          { title: 'MENU.MAP_FLOOR',    icon: 'layers',    link: '/admin/map-floors' },
          { title: 'MENU.MAP_OBJECT',   icon: 'category',  link: '/admin/map-objects' },
          { title: 'MENU.MAP_VISUAL',   icon: 'map',       link: '/admin/map-visual', permLink: '/admin/map-objects' },
          { title: 'MENU.ROOM_BOOKING_CONFIG', icon: 'meeting_room',    link: '/admin/room-booking-config' },
          { title: 'MENU.STUDY_ROOM_BOOKING',  icon: 'event_available', link: '/admin/study-room-bookings' }
        ]
      },
      {
        title: 'MENU.CATALOG_DICT',
        icon: 'menu_book',
        expanded: false,
        children: [
          { title: 'MENU.DIC_AUTHORS', icon: 'person', link: '/admin/dic-authors' },
          { title: 'MENU.DIC_PUBLISHERS', icon: 'business', link: '/admin/dic-publishers' },
          { title: 'MENU.DIC_KEYWORDS', icon: 'vpn_key', link: '/admin/dic-keywords' },
          { title: 'MENU.DIC_LANGUAGES', icon: 'language', link: '/admin/dic-languages' },
          { title: 'MENU.DIC_COUNTRIES', icon: 'public', link: '/admin/dic-countries' },
          { title: 'MENU.DIC_GEO', icon: 'map', link: '/admin/dic-geographic-areas' }
        ]
      },
      {
        title: 'MENU.RECEIPTION',
        icon: 'door_front',
        expanded: false,
        children: [
          { title: 'MENU.CHECK_IN_OUT',     icon: 'login',                   link: '/admin/check-in-out' },
          { title: 'MENU.CHECKIN_HISTORY',  icon: 'history',                 link: '/admin/checkin-history' },
          { title: 'MENU.CHECKIN_STATS',    icon: 'query_stats',             link: '/admin/checkin-statistics' },
          { title: 'MENU.RECEIPTION_REPORT', icon: 'summarize',              link: '/admin/receiption-report' },
          { title: 'MENU.BORROW_KEYS',      icon: 'vpn_key',                 link: '/admin/borrow-keys' },
          { title: 'MENU.SEARCH_BORROW_KEYS', icon: 'manage_search',         link: '/admin/search-borrow-keys' },
          { title: 'MENU.CONFIG_RECEIPTION', icon: 'settings_input_component', link: '/admin/config-receiption' },
          { title: 'MENU.CABINETS', icon: 'door_back',                link: '/admin/cabinets' }
        ]
      },
      {
        title: 'MENU.SUBJECT_DB',
        icon: 'school',
        expanded: false,
        children: [
          { title: 'MENU.SUBJECT_TREE',      icon: 'account_tree',      link: '/admin/subject-tree' },
          { title: 'MENU.DON_VI',           icon: 'corporate_fare',    link: '/admin/subject-units' },
          { title: 'MENU.KNOWLEDGE',        icon: 'psychology',        link: '/admin/subject-knowledge' },
          { title: 'MENU.NGANH',            icon: 'account_tree',      link: '/admin/subject-majors' },
          { title: 'MENU.EVALUATE_DEGREE',  icon: 'workspace_premium', link: '/admin/subject-degrees' },
          { title: 'MENU.EVALUATE_PROGRAM', icon: 'assignment',        link: '/admin/subject-programs' },
          { title: 'MENU.MONHOC',           icon: 'menu_book',         link: '/admin/subjects' }
        ]
      },
      {
        title: 'MENU.SYSTEM',
        icon: 'dns',
        expanded: false,
        children: [
          { title: 'MENU.USERS',          icon: 'manage_accounts',       link: '/admin/users' },
          /* TẠM ẨN — Đơn vị, Phân hệ (Module)
          { title: 'MENU.DEPARTMENTS',    icon: 'corporate_fare',        link: '/admin/departments' },
          { title: 'MENU.MODULES',        icon: 'layers',                link: '/admin/modules' },
          */
          { title: 'MENU.SYSTEM_PARAMS',  icon: 'settings_applications', link: '/admin/system-parameters' },
          /* TẠM ẨN — Phân quyền (Roles)
          { title: 'MENU.ROLES',          icon: 'admin_panel_settings', link: '/admin/roles' },
          */
          { title: 'MENU.DOC_UNIT_REPORT', icon: 'summarize',           link: '/admin/document-unit-report' },
          { title: 'MENU.SYSTEM_LOG',     icon: 'manage_history',        link: '/admin/system-log' },
          { title: 'MENU.SMS_ZALO_CONFIG', icon: 'sms',                  link: '/admin/notification-channel-config' },
          { title: 'MENU.NOTIFICATION_LOG', icon: 'history',             link: '/admin/notification-log' },
          { title: 'MENU.SCHEDULED_REPORT', icon: 'schedule_send',       link: '/admin/scheduled-report' },
          { title: 'MENU.SEARCH_QUALITY', icon: 'query_stats',           link: '/admin/search-quality', permLink: '/admin/dashboard' },
          { title: 'MENU.TENANTS',        icon: 'business',              link: '/admin/tenant' }
        ]
      }
      /* TẠM ẨN — Cài đặt
      ,{
        title: 'MENU.SETTINGS',
        icon: 'settings',
        link: '/admin/settings'
      }
      */
    ];

    return of(mockMenu);
  }
}
