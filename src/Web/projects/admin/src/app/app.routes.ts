import { Routes } from '@angular/router';
import { authGuard, permissionGuard, systemGuard, tenantGuard } from './core/guards';
import { Shell } from './layout/shell';
import { Callback } from './pages/callback';
import { Home } from './pages/home';
import { ACADEMIC_TITLES, CURRENCIES, DEGREES, ETHNICITIES, NATIONALITIES, POSITIONS, SYSTEM_PARAMETERS } from './pages/tenant/catalogs';
import { EMAIL_TEMPLATES } from './pages/notification/templates';
import { CLASSES, COURSES, READER_GROUPS, READER_TYPES } from './pages/patron/catalogs';
import { BIB_TYPES } from './pages/catalog/catalogs';
import { STORES, STORE_TYPES } from './pages/holdings/catalogs';
import { CIRC_PLACES, FINE_REASONS, LOAN_POLICIES } from './pages/circulation/catalogs';
import type { CrudConfig } from './shared/crud-page';

/** Màn danh mục chuẩn của host đơn vị — đường dẫn như admin cũ (/admin/nationalities, /admin/chuc-vus…). */
const catalog = (path: string, config: CrudConfig) => ({
  path,
  loadComponent: () => import('./shared/crud-page').then((m) => m.CrudPage),
  canActivate: [tenantGuard, permissionGuard(config.perm)],
  data: { config },
  title: config.title,
});

// Các màn tải theo trang (lazy) — khung admin + bảng điều khiển nằm trong bundle đầu.
export const routes: Routes = [
  { path: 'callback', component: Callback },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', component: Home, title: 'Bảng điều khiển' },
      { path: 'change-password', loadComponent: () => import('./pages/change-password').then((m) => m.ChangePassword), title: 'Đổi mật khẩu' },
      { path: 'profile', loadComponent: () => import('./pages/profile').then((m) => m.Profile), title: 'Thông tin cá nhân' },
      // Host hệ thống
      { path: 'tenant', loadComponent: () => import('./pages/system/tenants-list').then((m) => m.TenantsList), canActivate: [systemGuard], title: 'Đơn vị' },
      { path: 'tenant/create', loadComponent: () => import('./pages/system/tenant-create').then((m) => m.TenantCreate), canActivate: [systemGuard], title: 'Tạo đơn vị' },
      { path: 'tenant/:id', loadComponent: () => import('./pages/system/tenant-detail').then((m) => m.TenantDetail), canActivate: [systemGuard], title: 'Đơn vị' },
      {
        path: 'platform-logs', loadComponent: () => import('./pages/audit-logs').then((m) => m.AuditLogs), canActivate: [systemGuard],
        data: { scope: 'platform' }, title: 'Nhật ký nền tảng',
      },
      // Host đơn vị
      { path: 'users', loadComponent: () => import('./pages/tenant/users').then((m) => m.Users), canActivate: [tenantGuard, permissionGuard('USER')], title: 'Cán bộ quản lý thư viện' },
      { path: 'roles', loadComponent: () => import('./pages/tenant/roles').then((m) => m.Roles), canActivate: [tenantGuard, permissionGuard('ROLE')], title: 'Phân quyền' },
      { path: 'orgs', loadComponent: () => import('./pages/tenant/orgs').then((m) => m.Orgs), canActivate: [tenantGuard, permissionGuard('ORGS')], title: 'Phòng ban' },
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
      { path: 'readers', loadComponent: () => import('./pages/patron/readers').then((m) => m.Readers), canActivate: [tenantGuard, permissionGuard('READERS')], title: 'Bạn đọc' },
      catalog('bib-types', BIB_TYPES),
      { path: 'worksheets', loadComponent: () => import('./pages/catalog/worksheets').then((m) => m.Worksheets), canActivate: [tenantGuard, permissionGuard('WORKSHEETS')], title: 'Biểu mẫu biên mục' },
      { path: 'catalog-bibs', loadComponent: () => import('./pages/catalog/bibs').then((m) => m.Bibs), canActivate: [tenantGuard, permissionGuard('CATALOG_BIBS')], title: 'Biên mục biểu ghi' },
      // ":mfn" = "new" khi biên mục mới (monolith: /catalog-bibs/new và /catalog-bibs/edit/:mfn).
      { path: 'catalog-bibs/:mfn', loadComponent: () => import('./pages/catalog/bib-edit').then((m) => m.BibEdit), canActivate: [tenantGuard, permissionGuard('CATALOG_BIBS')], title: 'Biên mục' },
      catalog('store-types', STORE_TYPES),
      catalog('stores', STORES),
      { path: 'books', loadComponent: () => import('./pages/holdings/items').then((m) => m.Items), canActivate: [tenantGuard, permissionGuard('DOC_SEARCH')], title: 'Tìm kiếm tài liệu' },
      { path: 'shelving', loadComponent: () => import('./pages/holdings/shelving').then((m) => m.Shelving), canActivate: [tenantGuard, permissionGuard('MAP_SHELVING')], title: 'Xếp giá' },
      catalog('circ-places', CIRC_PLACES),
      catalog('circ-policies', LOAN_POLICIES),
      catalog('cfine-types', FINE_REASONS),
      { path: 'request-books', loadComponent: () => import('./pages/circulation/holds').then((m) => m.Holds), canActivate: [tenantGuard, permissionGuard('REQUEST_BOOKS')], title: 'Yêu cầu mượn' },
      { path: 'fines', loadComponent: () => import('./pages/circulation/fines').then((m) => m.Fines), canActivate: [tenantGuard, permissionGuard('FINES')], title: 'Quản lý phạt' },
      { path: 'fine-ticket/:publicId', loadComponent: () => import('./pages/circulation/fine-ticket').then((m) => m.FineTicketPage), canActivate: [tenantGuard, permissionGuard('FINES')], title: 'Phiếu phạt' },
      { path: 'borrow', loadComponent: () => import('./pages/circulation/borrow').then((m) => m.Borrow), canActivate: [tenantGuard, permissionGuard('BORROW')], title: 'Mượn / Trả' },
      { path: 'loan-history', loadComponent: () => import('./pages/circulation/loan-history').then((m) => m.LoanHistory), canActivate: [tenantGuard, permissionGuard('LOAN_HISTORY')], title: 'Lịch sử lưu thông' },
      {
        path: 'system-logs', loadComponent: () => import('./pages/audit-logs').then((m) => m.AuditLogs),
        canActivate: [tenantGuard, permissionGuard('SYSTEM_LOG')], data: { scope: 'tenant' }, title: 'Nhật ký hệ thống',
      },
      {
        path: 'email-settings',
        loadComponent: () => import('./pages/notification/email-settings').then((m) => m.EmailSettingsPage),
        canActivate: [tenantGuard, permissionGuard('NOTIFICATION_CONFIG')],
        title: 'Cấu hình email',
      },
      {
        path: 'notification-logs',
        loadComponent: () => import('./pages/notification/notification-logs').then((m) => m.NotificationLogs),
        canActivate: [tenantGuard, permissionGuard('NOTIFICATION_LOGS')],
        title: 'Nhật ký gửi tin',
      },
      { path: 'phan-he/:code', loadComponent: () => import('./pages/module-placeholder').then((m) => m.ModulePlaceholder), canActivate: [tenantGuard] },
    ],
  },
  { path: '**', redirectTo: '' },
];
