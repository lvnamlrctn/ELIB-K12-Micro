import { Routes } from '@angular/router';
import { anyPermissionGuard, authGuard, permissionGuard, systemGuard, tenantGuard } from './core/guards';
import { APPROVAL_VIEW_PERMS } from './layout/menu';
import { Shell } from './layout/shell';
import { Callback } from './pages/callback';
import { Home } from './pages/home';
import { ACADEMIC_TITLES, CURRENCIES, DEGREES, ETHNICITIES, NATIONALITIES, POSITIONS, SYSTEM_PARAMETERS } from './pages/tenant/catalogs';
import { EMAIL_TEMPLATES } from './pages/notification/templates';
import { CLASSES, COURSES, READER_GROUPS, READER_TYPES } from './pages/patron/catalogs';
import { BIB_TYPES, DIC_AUTHORS, DIC_CLASSES, DIC_KEYWORDS, DIC_PUBLISHERS, ISBD_CONFIGS } from './pages/catalog/catalogs';
import { Z3950_SERVERS } from './pages/search/catalogs';
import { BOOK_OUT_UNITS, EXHIBITION_LOCATIONS, EXPORT_REASONS, STORES, STORE_TYPES } from './pages/holdings/catalogs';
import { FINE_REASONS, LOAN_POLICIES, PHOTOCOPIES, SIP2_STATIONS } from './pages/circulation/catalogs';
import { ACQ_SOURCES, BUDGETS, FUNDS, SUPPLIERS } from './pages/acquisition/catalogs';
import { DIGITAL_COLLECTIONS, DIGITAL_DIG_TYPES, DIGITAL_POLICIES, DIGITAL_POLICIES_BY_COLLECTION, DIGITAL_REVIEWS, DIGITAL_SUBJECTS, DIGITAL_TOPICS } from './pages/digital/catalogs';
import { SPACE_BUILDINGS, SPACE_FLOORS, SPACE_ROOMS } from './pages/space/catalogs';
import type { CrudConfig } from './shared/crud-page';
import type { TreeCatalogConfig } from './shared/tree-catalog-page';
import { unsavedChangesGuard } from './shared/unsaved';

/** Màn danh mục chuẩn của host đơn vị — đường dẫn như admin cũ (/admin/nationalities, /admin/chuc-vus…). */
const catalog = (path: string, config: CrudConfig) => ({
  path,
  loadComponent: () => import('./shared/crud-page').then((m) => m.CrudPage),
  canActivate: [tenantGuard, permissionGuard(config.perm)],
  data: { config },
  title: config.title,
});

/** Danh mục cây tài liệu số (bộ sưu tập/môn loại/chủ đề) — như catalog() nhưng dùng TreeCatalogPage (có quan hệ cha/con). */
const treeCatalog = (path: string, config: TreeCatalogConfig) => ({
  path,
  loadComponent: () => import('./shared/tree-catalog-page').then((m) => m.TreeCatalogPage),
  canActivate: [tenantGuard, permissionGuard(config.perm)],
  data: { config },
  title: config.title,
});

// Các màn tải theo trang (lazy) — khung admin + bảng điều khiển nằm trong bundle đầu.
export const routes: Routes = [
  { path: 'callback', component: Callback },
  // Trang in (ngoài khung admin — bản in chỉ có nội dung). Guard đăng nhập ở route cha chạy xong rồi mới đến guard quyền.
  {
    path: 'print',
    canActivate: [authGuard],
    children: [
      { path: 'reader-cards', loadComponent: () => import('./pages/patron/reader-print').then((m) => m.ReaderPrint), canActivate: [tenantGuard, permissionGuard('READERS')], title: 'READER.PRINT_CARD' },
      { path: 'loan-slip', loadComponent: () => import('./pages/circulation/loan-slip').then((m) => m.LoanSlipPage), canActivate: [tenantGuard, permissionGuard('BORROW')], title: 'BORROW_PAGE.TITLE' },
      { path: 'acq-report', loadComponent: () => import('./pages/acquisition/report-print').then((m) => m.AcquisitionReportPrint), canActivate: [tenantGuard, permissionGuard('AB_RECEIPTS')], title: 'ACQ_REPORT.TITLE' },
      // Bản in báo cáo dùng chung (lưu thông, đặt phòng, kho…): nội dung do màn báo cáo ghi sẵn vào sessionStorage, quyền kiểm ở màn đó.
      { path: 'report', loadComponent: () => import('./pages/acquisition/report-print').then((m) => m.AcquisitionReportPrint), canActivate: [tenantGuard], title: 'REPORT_PRINT.TITLE' },
      { path: 'labels', loadComponent: () => import('./pages/holdings/labels').then((m) => m.LabelPrint), canActivate: [tenantGuard, anyPermissionGuard('PRINT_LABEL:view', 'CATALOG_BIBS:view')], title: 'LABELS_PAGE.TITLE' },
    ],
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', component: Home, title: 'PAGE_TITLE.HOME' },
      { path: 'change-password', loadComponent: () => import('./pages/change-password').then((m) => m.ChangePassword), title: 'CHANGE_PASSWORD.TITLE' },
      { path: 'profile', loadComponent: () => import('./pages/profile').then((m) => m.Profile), title: 'PAGE_TITLE.PROFILE' },
      // Host hệ thống
      { path: 'tenant', loadComponent: () => import('./pages/system/tenants-list').then((m) => m.TenantsList), canActivate: [systemGuard], title: 'TENANT_ADMIN.TITLE' },
      { path: 'tenant/create', loadComponent: () => import('./pages/system/tenant-create').then((m) => m.TenantCreate), canActivate: [systemGuard], title: 'TENANT_ADMIN.CREATE' },
      { path: 'tenant/:id', loadComponent: () => import('./pages/system/tenant-detail').then((m) => m.TenantDetail), canActivate: [systemGuard], title: 'TENANT_ADMIN.TITLE' },
      { path: 'platform-stats', loadComponent: () => import('./pages/system/platform-stats').then((m) => m.PlatformStats), canActivate: [systemGuard], title: 'PLATFORM_STATS.TITLE' },
      {
        path: 'platform-logs', loadComponent: () => import('./pages/audit-logs').then((m) => m.AuditLogs), canActivate: [systemGuard],
        data: { scope: 'platform' }, title: 'AUDIT.TITLE_PLATFORM',
      },
      // Host đơn vị
      { path: 'users', loadComponent: () => import('./pages/tenant/users').then((m) => m.Users), canActivate: [tenantGuard, permissionGuard('USER')], title: 'USER_MGT.PAGE_TITLE' },
      { path: 'roles', loadComponent: () => import('./pages/tenant/roles').then((m) => m.Roles), canActivate: [tenantGuard, permissionGuard('ROLE')], title: 'ROLE.PAGE_TITLE' },
      { path: 'orgs', loadComponent: () => import('./pages/tenant/orgs').then((m) => m.Orgs), canActivate: [tenantGuard, permissionGuard('ORGS')], title: 'ORG.PAGE_TITLE' },
      catalog('nationalities', NATIONALITIES),
      catalog('profs', ACADEMIC_TITLES),
      catalog('ethnics', ETHNICITIES),
      catalog('degrees', DEGREES),
      catalog('chuc-vus', POSITIONS),
      catalog('currencies', CURRENCIES),
      catalog('system-parameters', SYSTEM_PARAMETERS),
      catalog('email-templates', EMAIL_TEMPLATES),
      catalog('reader-types', READER_TYPES),
      catalog('academic-classes', CLASSES),
      catalog('courses', COURSES),
      catalog('reader-groups', READER_GROUPS),
      { path: 'readers', loadComponent: () => import('./pages/patron/readers').then((m) => m.Readers), canActivate: [tenantGuard, permissionGuard('READERS')], title: 'MENU.READERS' },
      catalog('bib-types', BIB_TYPES),
      { path: 'worksheets', loadComponent: () => import('./pages/catalog/worksheets').then((m) => m.Worksheets), canActivate: [tenantGuard, permissionGuard('WORKSHEETS')], title: 'WORKSHEET_PAGE.TITLE' },
      { path: 'catalog-bibs', loadComponent: () => import('./pages/catalog/bibs').then((m) => m.Bibs), canActivate: [tenantGuard, permissionGuard('CATALOG_BIBS')], title: 'BIBS_PAGE.TITLE' },
      // ":mfn" = "new" khi biên mục mới (monolith: /catalog-bibs/new và /catalog-bibs/edit/:mfn).
      { path: 'catalog-bibs/:mfn', loadComponent: () => import('./pages/catalog/bib-edit').then((m) => m.BibEdit), canActivate: [tenantGuard, permissionGuard('CATALOG_BIBS')], canDeactivate: [unsavedChangesGuard], title: 'PAGE_TITLE.CATALOGING' },
      catalog('store-types', STORE_TYPES),
      catalog('stores', STORES),
      { path: 'books', loadComponent: () => import('./pages/holdings/items').then((m) => m.Items), canActivate: [tenantGuard, permissionGuard('DOC_SEARCH')], title: 'ITEMS_PAGE.TITLE' },
      { path: 'print-labels', loadComponent: () => import('./pages/holdings/labels').then((m) => m.Labels), canActivate: [tenantGuard, anyPermissionGuard('PRINT_LABEL:view', 'CATALOG_BIBS:view')], title: 'LABELS_PAGE.TITLE' },
      { path: 'shelving', loadComponent: () => import('./pages/holdings/shelving').then((m) => m.Shelving), canActivate: [tenantGuard, permissionGuard('MAP_SHELVING')], title: 'SHELVING_PAGE.TITLE' },
      { path: 'inventory', loadComponent: () => import('./pages/holdings/inventories').then((m) => m.Inventories), canActivate: [tenantGuard, permissionGuard('INVENTORY')], title: 'INVENTORY_PAGE.TITLE' },
      { path: 'inventory/:id', loadComponent: () => import('./pages/holdings/inventories').then((m) => m.InventoryDetail), canActivate: [tenantGuard, permissionGuard('INVENTORY')], title: 'INVENTORY_PAGE.TITLE' },
      {
        path: 'lost-books', loadComponent: () => import('./pages/holdings/store-records').then((m) => m.StoreRecords),
        canActivate: [tenantGuard, permissionGuard('LOST_BOOKS')], data: { kind: 'lost' }, title: 'STORE_RECORD.LOST_TITLE',
      },
      {
        path: 'liquidates', loadComponent: () => import('./pages/holdings/store-records').then((m) => m.StoreRecords),
        canActivate: [tenantGuard, permissionGuard('LIQUIDATES')], data: { kind: 'liquidation' }, title: 'STORE_RECORD.LIQUIDATE_TITLE',
      },
      { path: 'book-out-store', loadComponent: () => import('./pages/holdings/store-outs').then((m) => m.StoreOuts), canActivate: [tenantGuard, permissionGuard('BOOK_OUT_STORE')], title: 'STORE_OUT.TITLE' },
      catalog('export-reason', EXPORT_REASONS),
      catalog('book-out-unit', BOOK_OUT_UNITS),
      catalog('exhibition-location', EXHIBITION_LOCATIONS),
      ...(['ab-moves', 'ab-deliverers'] as const).flatMap((path) => {
        const kind = path === 'ab-moves' ? 'store-moves' : 'deliveries';
        const perm = path === 'ab-moves' ? 'AB_MOVES' : 'AB_DELIVERERS';
        const title = path === 'ab-moves' ? 'TRANSFER.MOVE_TITLE' : 'TRANSFER.DELIVERY_TITLE';
        return [
          { path, loadComponent: () => import('./pages/holdings/transfers').then((m) => m.Transfers), canActivate: [tenantGuard, permissionGuard(perm)], data: { kind }, title },
          { path: path + '/:id', loadComponent: () => import('./pages/holdings/transfers').then((m) => m.TransferDetail), canActivate: [tenantGuard, permissionGuard(perm)], data: { kind }, title },
        ];
      }),
      { path: 'map-shelving', loadComponent: () => import('./pages/holdings/map-shelving').then((m) => m.MapShelving), canActivate: [tenantGuard, permissionGuard('MAP_SHELVING')], data: { mode: 'shelve' }, title: 'MAP_SHELVING_PAGE.TITLE' },
      { path: 'reshelving', loadComponent: () => import('./pages/holdings/map-shelving').then((m) => m.MapShelving), canActivate: [tenantGuard, permissionGuard('MAP_SHELVING')], data: { mode: 'reshelve' }, title: 'MAP_SHELVING_PAGE.RESHELVE_TITLE' },
      { path: 'ab-deliverers-receive', loadComponent: () => import('./pages/holdings/delivery-receive').then((m) => m.DeliveryReceive), canActivate: [tenantGuard, permissionGuard('AB_DELIVERERS')], title: 'TRANSFER.RECEIVE_TITLE' },
      // Bổ sung (acquisition)
      { path: 'ab-orders', loadComponent: () => import('./pages/acquisition/orders').then((m) => m.Orders), canActivate: [tenantGuard, permissionGuard('AB_ORDERS')], title: 'ACQ_ORDER.TITLE' },
      { path: 'ab-orders/:id', loadComponent: () => import('./pages/acquisition/orders').then((m) => m.OrderDetail), canActivate: [tenantGuard, permissionGuard('AB_ORDERS')], title: 'ACQ_ORDER.TITLE' },
      { path: 'ab-receipts', loadComponent: () => import('./pages/acquisition/receipts').then((m) => m.Receipts), canActivate: [tenantGuard, permissionGuard('AB_RECEIPTS')], title: 'ACQ_RECEIPT.TITLE' },
      { path: 'ab-receipts/lookup', loadComponent: () => import('./pages/acquisition/receipt-lookup').then((m) => m.ReceiptLookup), canActivate: [tenantGuard, permissionGuard('AB_RECEIPTS')], title: 'ACQ_LOOKUP.TITLE' },
      {
        path: 'ab-receipts/:id', loadComponent: () => import('./pages/acquisition/receipts').then((m) => m.ReceiptDetail), canActivate: [tenantGuard, permissionGuard('AB_RECEIPTS')],
        canDeactivate: [unsavedChangesGuard], title: 'ACQ_RECEIPT.TITLE',
      },
      { path: 'ab-reports', loadComponent: () => import('./pages/acquisition/reports').then((m) => m.AcquisitionReports), canActivate: [tenantGuard, permissionGuard('AB_RECEIPTS')], title: 'ACQ_REPORT.TITLE' },
      catalog('ab-sources', ACQ_SOURCES),
      catalog('suppliers', SUPPLIERS),
      catalog('budgets', BUDGETS),
      catalog('funds', FUNDS),
      { path: 're-register-barcode', loadComponent: () => import('./pages/holdings/re-register').then((m) => m.ReRegister), canActivate: [tenantGuard, permissionGuard('RE_REGISTER')], title: 'RE_REGISTER_PAGE.TITLE' },
      { path: 'statistics-document', loadComponent: () => import('./pages/holdings/store-reports').then((m) => m.StoreStats), canActivate: [tenantGuard, permissionGuard('STAT_DOC')], title: 'STORE_STATS.TITLE' },
      { path: 'store-book-report', loadComponent: () => import('./pages/holdings/store-reports').then((m) => m.StoreBookReportPage), canActivate: [tenantGuard, permissionGuard('STORE_BOOK_REPORT')], title: 'STORE_BOOK_REPORT.TITLE' },
      { path: 'circ-places', loadComponent: () => import('./pages/circulation/circ-places').then((m) => m.CircPlaces), canActivate: [tenantGuard, permissionGuard('CIRC_PLACES')], title: 'CIRC_PLACE_PAGE.TITLE' },
      catalog('circ-policies', LOAN_POLICIES),
      catalog('cfine-types', FINE_REASONS),
      catalog('sip2-stations', SIP2_STATIONS),
      catalog('dic-authors', DIC_AUTHORS),
      catalog('dic-publishers', DIC_PUBLISHERS),
      catalog('dic-keywords', DIC_KEYWORDS),
      catalog('dic-classes', DIC_CLASSES),
      catalog('isbd-configs', ISBD_CONFIGS),
      {
        path: 'catalog-logs', loadComponent: () => import('./pages/audit-logs').then((m) => m.AuditLogs),
        canActivate: [tenantGuard, permissionGuard('CATALOG_BIBS')], data: { scope: 'catalog' }, title: 'AUDIT.TITLE_CATALOG',
      },
      { path: 'sip2-logs', loadComponent: () => import('./pages/circulation/sip2-logs').then((m) => m.Sip2Logs), canActivate: [tenantGuard, permissionGuard('SIP2LOG')], title: 'SIP2.LOG_TITLE' },
      catalog('photocopy', PHOTOCOPIES),
      { path: 'circulation-report', loadComponent: () => import('./pages/circulation/reports').then((m) => m.CirculationReports), canActivate: [tenantGuard, permissionGuard('CIRC_REPORT')], title: 'CIRC_REPORT_PAGE.TITLE' },
      { path: 'request-books', loadComponent: () => import('./pages/circulation/holds').then((m) => m.Holds), canActivate: [tenantGuard, permissionGuard('REQUEST_BOOKS')], title: 'HOLDS_PAGE.TITLE' },
      { path: 'search-index', loadComponent: () => import('./pages/search/search-index').then((m) => m.SearchIndex), canActivate: [tenantGuard, permissionGuard('SEARCH_INDEX')], title: 'SEARCH_INDEX_PAGE.ROUTE_TITLE' },
      catalog('z3950-configs', Z3950_SERVERS),
      // Tra Z39.50: cán bộ biên mục (thêm biểu ghi) hoặc người quản lý máy chủ Z39.50.
      { path: 'z3950-search', loadComponent: () => import('./pages/search/z3950-search').then((m) => m.Z3950Search),
        canActivate: [tenantGuard, anyPermissionGuard('CATALOG_BIBS:add', 'Z3950_CONFIGS:view')],
        title: 'Z3950_PAGE.TITLE' },
      { path: 'search-stats', loadComponent: () => import('./pages/search/search-stats').then((m) => m.SearchStats), canActivate: [tenantGuard, permissionGuard('SEARCH_STATS')], title: 'SEARCH_STATS_PAGE.ROUTE_TITLE' },
      { path: 'approvals', loadComponent: () => import('./pages/circulation/approvals').then((m) => m.Approvals), canActivate: [tenantGuard, anyPermissionGuard(...APPROVAL_VIEW_PERMS)], title: 'APPROVALS_PAGE.TITLE' },
      { path: 'approval-rules', loadComponent: () => import('./pages/tenant/approval-rules').then((m) => m.ApprovalRules), canActivate: [tenantGuard, permissionGuard('APPROVAL_RULES')], title: 'APPROVAL_RULES_PAGE.TITLE' },
      { path: 'fines', loadComponent: () => import('./pages/circulation/fines').then((m) => m.Fines), canActivate: [tenantGuard, permissionGuard('FINES')], title: 'FINES_PAGE.TITLE' },
      { path: 'fine-ticket/:publicId', loadComponent: () => import('./pages/circulation/fine-ticket').then((m) => m.FineTicketPage), canActivate: [tenantGuard, permissionGuard('FINES')], title: 'PAGE_TITLE.FINE_TICKET' },
      { path: 'borrow', loadComponent: () => import('./pages/circulation/borrow').then((m) => m.Borrow), canActivate: [tenantGuard, permissionGuard('BORROW')], title: 'BORROW_PAGE.TITLE' },
      { path: 'loan-history', loadComponent: () => import('./pages/circulation/loan-history').then((m) => m.LoanHistory), canActivate: [tenantGuard, permissionGuard('LOAN_HISTORY')], title: 'LOAN_HISTORY_PAGE.TITLE' },
      { path: 'check-in-out', loadComponent: () => import('./pages/circulation/check-in-out').then((m) => m.CheckInOut), canActivate: [tenantGuard, permissionGuard('CHECK_IN_OUT')], title: 'CHECK_IN_OUT_PAGE.TITLE' },
      { path: 'checkin-history', loadComponent: () => import('./pages/circulation/checkin-history').then((m) => m.CheckinHistory), canActivate: [tenantGuard, permissionGuard('CHECK_IN_OUT')], title: 'CHECKIN_HISTORY_PAGE.TITLE' },
      {
        path: 'system-logs', loadComponent: () => import('./pages/audit-logs').then((m) => m.AuditLogs),
        canActivate: [tenantGuard, permissionGuard('SYSTEM_LOG')], data: { scope: 'tenant' }, title: 'AUDIT.TITLE_TENANT',
      },
      {
        path: 'email-settings',
        loadComponent: () => import('./pages/notification/email-settings').then((m) => m.EmailSettingsPage),
        canActivate: [tenantGuard, permissionGuard('NOTIFICATION_CONFIG')],
        title: 'EMAIL_SETTINGS.TITLE',
      },
      {
        path: 'notification-logs',
        loadComponent: () => import('./pages/notification/notification-logs').then((m) => m.NotificationLogs),
        canActivate: [tenantGuard, permissionGuard('NOTIFICATION_LOGS')],
        title: 'NOTIFICATION_LOG.TITLE',
      },
      // Tài liệu số (digital)
      treeCatalog('digital-collections', DIGITAL_COLLECTIONS),
      treeCatalog('digital-subjects', DIGITAL_SUBJECTS),
      treeCatalog('digital-topics', DIGITAL_TOPICS),
      catalog('digital-dig-types', DIGITAL_DIG_TYPES),
      catalog('digital-policies', DIGITAL_POLICIES),
      catalog('digital-policies-by-collection', DIGITAL_POLICIES_BY_COLLECTION),
      catalog('digital-reviews', DIGITAL_REVIEWS),
      { path: 'digital-items', loadComponent: () => import('./pages/digital/items').then((m) => m.DigitalItems), canActivate: [tenantGuard, permissionGuard('DIGITAL_ITEMS')], title: 'DIGITAL_ITEMS_PAGE.TITLE' },
      { path: 'digital-loans', loadComponent: () => import('./pages/digital/loans').then((m) => m.DigitalLoans), canActivate: [tenantGuard, permissionGuard('DIGITAL_LOANS')], title: 'DIGITAL_LOANS_PAGE.TITLE' },
      { path: 'digital-submissions', loadComponent: () => import('./pages/digital/submissions').then((m) => m.DigitalSubmissions), canActivate: [tenantGuard, permissionGuard('DIGITAL_SUBMISSIONS')], title: 'DIGITAL_SUBMISSIONS_PAGE.TITLE' },
      // Không gian & đặt phòng (space)
      catalog('space-buildings', SPACE_BUILDINGS),
      catalog('space-floors', SPACE_FLOORS),
      catalog('space-rooms', SPACE_ROOMS),
      { path: 'space-floor-map', loadComponent: () => import('./pages/space/floor-map-editor').then((m) => m.SpaceFloorMapEditor), canActivate: [tenantGuard, permissionGuard('SPACE_ROOMS')], title: 'SPACE_FLOOR_MAP_PAGE.TITLE' },
      { path: 'space-locker-desk', loadComponent: () => import('./pages/space/locker-desk').then((m) => m.SpaceLockerDesk), canActivate: [tenantGuard, permissionGuard('SPACE_LOCKER_LOANS')], title: 'SPACE_LOCKER_DESK_PAGE.TITLE' },
      { path: 'space-bookings', loadComponent: () => import('./pages/space/bookings').then((m) => m.SpaceBookings), canActivate: [tenantGuard, permissionGuard('SPACE_BOOKINGS')], title: 'SPACE_BOOKINGS_PAGE.TITLE' },
      { path: 'space-bans', loadComponent: () => import('./pages/space/bans').then((m) => m.SpaceBans), canActivate: [tenantGuard, permissionGuard('SPACE_BANS')], title: 'SPACE_BANS_PAGE.TITLE' },
      { path: 'space-hours', loadComponent: () => import('./pages/space/hours').then((m) => m.SpaceHours), canActivate: [tenantGuard, permissionGuard('SPACE_HOURS')], title: 'SPACE_HOURS_PAGE.TITLE' },
      { path: 'payment-transactions', loadComponent: () => import('./pages/payment/transactions').then((m) => m.PaymentTransactions), canActivate: [tenantGuard, permissionGuard('PAYMENT_TRANSACTIONS')], title: 'PAYMENT_TX.TITLE' },
      { path: 'payment-account', loadComponent: () => import('./pages/payment/account').then((m) => m.PaymentAccountPage), canActivate: [tenantGuard, permissionGuard('PAYMENT_ACCOUNTS')], title: 'PAYMENT_ACCOUNT.TITLE' },
      { path: 'card-fees', loadComponent: () => import('./pages/patron/card-fees').then((m) => m.CardFees), canActivate: [tenantGuard, permissionGuard('READER_CARD_FEES')], title: 'CARD_FEES.TITLE' },
      { path: 'space-settings', loadComponent: () => import('./pages/space/settings').then((m) => m.SpaceSettingsPage), canActivate: [tenantGuard, permissionGuard('SPACE_SETTINGS')], title: 'SPACE_SETTINGS_PAGE.TITLE' },
      { path: 'space-report', loadComponent: () => import('./pages/space/report').then((m) => m.SpaceReportPage), canActivate: [tenantGuard, permissionGuard('SPACE_REPORT')], title: 'SPACE_REPORT_PAGE.TITLE' },
      { path: 'space-access', loadComponent: () => import('./pages/space/access').then((m) => m.SpaceAccess), canActivate: [tenantGuard, permissionGuard('SPACE_ACCESS')], title: 'SPACE_ACCESS_PAGE.TITLE' },
      { path: 'phan-he/:code', loadComponent: () => import('./pages/module-placeholder').then((m) => m.ModulePlaceholder), canActivate: [tenantGuard] },
    ],
  },
  { path: '**', redirectTo: '' },
];
