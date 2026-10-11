import { Injectable, computed, inject } from '@angular/core';
import { MODULE_NAMES, Session } from '../core/session';

export interface MenuItem {
  title: string;
  icon?: string;
  link?: string;
  /** Mã module quyền (USER, ORGS…) — hiện khi có quyền MODULE:view. */
  perm?: string;
  /** Phân hệ license (CATALOG…) — nhóm chỉ hiện khi đơn vị đã mua. */
  module?: string;
  children?: MenuItem[];
}

const MODULE_ICONS: Record<string, string> = {
  CATALOG: 'edit_note',
  HOLDINGS: 'store',
  CIRCULATION: 'swap_horiz',
  ACQUISITION: 'add_shopping_cart',
  SERIALS: 'auto_stories',
  DIGITAL: 'menu_book',
  SEARCH: 'travel_explore',
  PAYMENT: 'payments',
  SPACE: 'meeting_room',
  AI: 'smart_toy',
  PORTAL: 'article',
  REPORTING: 'bar_chart',
  SCHOOL: 'school',
};

/**
 * Menu cùng cấu trúc và tên với menu admin của frontend monolith (services/menu.ts, nhãn MENU.* tiếng Việt).
 * Host hệ thống chỉ có quản trị nền tảng; host đơn vị lọc theo quyền và license (docs 08 §6) — chỉ là giao diện,
 * gateway/service vẫn tự chặn.
 */
@Injectable({ providedIn: 'root' })
export class Menu {
  private readonly session = inject(Session);

  readonly items = computed<MenuItem[]>(() => {
    if (!this.session.me()) return [];
    if (this.session.isSystem()) {
      return [
        { title: 'Bảng điều khiển', icon: 'dashboard', link: '/' },
        {
          title: 'Hệ thống',
          icon: 'dns',
          children: [
            { title: 'Đơn vị', icon: 'business', link: '/tenant' },
            { title: 'Nhật ký nền tảng', icon: 'receipt_long', link: '/platform-logs' },
          ],
        },
      ];
    }

    const modules = this.session.features()?.modules ?? [];
    return this.filter([
      { title: 'Bảng điều khiển', icon: 'dashboard', link: '/' },
      {
        title: 'Quản lý bạn đọc',
        icon: 'people',
        children: [
          { title: 'Bạn đọc', icon: 'people', link: '/readers', perm: 'READERS' },
          { title: 'Nhóm bạn đọc', icon: 'groups', link: '/reader-groups', perm: 'GROUPREADER' },
          {
            title: 'Tham số bạn đọc',
            icon: 'tune',
            children: [
              { title: 'Khoá', icon: 'school', link: '/courses', perm: 'COURSES' },
              { title: 'Lớp', icon: 'class', link: '/academic-classes', perm: 'CLASSES' },
              { title: 'Loại bạn đọc', icon: 'local_library', link: '/reader-types', perm: 'READER_TYPES' },
              { title: 'Quốc tịch', icon: 'flag', link: '/nationalities', perm: 'NATIONALITIES' },
              { title: 'Học hàm học vị', icon: 'workspace_premium', link: '/profs', perm: 'PROFS' },
              { title: 'Dân tộc', icon: 'diversity_3', link: '/ethnics', perm: 'ETHNICS' },
              { title: 'Trình độ', icon: 'school', link: '/degrees', perm: 'DEGREES' },
              { title: 'Chức vụ', icon: 'badge', link: '/chuc-vus', perm: 'POSITIONS' },
              { title: 'Phòng ban', icon: 'account_tree', link: '/orgs', perm: 'ORGS' },
            ],
          },
        ],
      },
      {
        title: 'Biên mục',
        icon: 'edit_note',
        module: 'CATALOG',
        children: [
          { title: 'Biên mục biểu ghi', icon: 'menu_book', link: '/catalog-bibs', perm: 'CATALOG_BIBS' },
          { title: 'Biểu mẫu biên mục', icon: 'list_alt', link: '/worksheets', perm: 'WORKSHEETS' },
          { title: 'Loại biểu ghi', icon: 'category', link: '/bib-types', perm: 'BIB_TYPES' },
        ],
      },
      {
        title: 'Lưu thông',
        icon: 'swap_vert',
        module: 'CIRCULATION',
        children: [
          { title: 'Mượn / Trả', icon: 'swap_vert', link: '/borrow', perm: 'BORROW' },
          { title: 'Yêu cầu mượn', icon: 'bookmark_add', link: '/request-books', perm: 'REQUEST_BOOKS' },
          { title: 'Lịch sử lưu thông', icon: 'history', link: '/loan-history', perm: 'LOAN_HISTORY' },
          { title: 'Báo cáo lưu thông', icon: 'bar_chart', link: '/circulation-report', perm: 'CIRC_REPORT' },
          { title: 'Quản lý phạt', icon: 'gavel', link: '/fines', perm: 'FINES' },
          { title: 'Photo copy tài liệu', icon: 'print', link: '/photocopy', perm: 'C_PHOTO' },
          { title: 'Lý do phạt', icon: 'rule_folder', link: '/cfine-types', perm: 'FINE_REASONS' },
          { title: 'Chính sách lưu thông', icon: 'rule', link: '/circ-policies', perm: 'CIRC_POLICIES' },
          { title: 'Điểm lưu thông', icon: 'place', link: '/circ-places', perm: 'CIRC_PLACES' },
        ],
      },
      {
        title: 'Tra cứu',
        icon: 'travel_explore',
        module: 'SEARCH',
        children: [
          { title: 'Chỉ mục tra cứu (OPAC)', icon: 'manage_search', link: '/search-index', perm: 'SEARCH_INDEX' },
          { title: 'Thống kê tra cứu', icon: 'query_stats', link: '/search-stats', perm: 'SEARCH_STATS' },
          { title: 'Tra cứu Z39.50', icon: 'travel_explore', link: '/z3950-search', perm: 'CATALOG_BIBS' },
          { title: 'Máy chủ Z39.50', icon: 'dns', link: '/z3950-configs', perm: 'Z3950_CONFIGS' },
        ],
      },
      {
        title: 'Quản lý Kho',
        icon: 'store',
        module: 'HOLDINGS',
        children: [
          { title: 'Tìm kiếm tài liệu', icon: 'manage_search', link: '/books', perm: 'DOC_SEARCH' },
          { title: 'Xếp giá', icon: 'shelves', link: '/shelving', perm: 'MAP_SHELVING' },
          { title: 'Kho', icon: 'warehouse', link: '/stores', perm: 'STORES' },
          { title: 'Loại kho', icon: 'category', link: '/store-types', perm: 'STORE_TYPES' },
        ],
      },
      {
        title: 'Phân hệ đã mua',
        icon: 'apps',
        children: modules.map((code) => ({ title: MODULE_NAMES[code] ?? code, icon: MODULE_ICONS[code] ?? 'extension', link: `/phan-he/${code}` })),
      },
      {
        title: 'Hệ thống',
        icon: 'dns',
        children: [
          { title: 'Cán bộ quản lý thư viện', icon: 'manage_accounts', link: '/users', perm: 'USER' },
          { title: 'Phân quyền', icon: 'admin_panel_settings', link: '/roles', perm: 'ROLE' },
          { title: 'Tham số hệ thống', icon: 'settings_applications', link: '/system-parameters', perm: 'SYSTEM_PARAMS' },
          { title: 'Tiền tệ', icon: 'currency_exchange', link: '/currencies', perm: 'CURRENCIES' },
          { title: 'Nhật ký hệ thống', icon: 'receipt_long', link: '/system-logs', perm: 'SYSTEM_LOG' },
          {
            title: 'Gửi thông báo',
            icon: 'mail',
            children: [
              { title: 'Cấu hình email', icon: 'settings', link: '/email-settings', perm: 'NOTIFICATION_CONFIG' },
              { title: 'Mẫu email', icon: 'drafts', link: '/email-templates', perm: 'NOTIFICATION_TEMPLATES' },
              { title: 'Nhật ký gửi tin', icon: 'history', link: '/notification-logs', perm: 'NOTIFICATION_LOGS' },
            ],
          },
        ],
      },
    ]);
  });

  private filter(items: MenuItem[]): MenuItem[] {
    const out: MenuItem[] = [];
    const modules = this.session.features()?.modules ?? [];
    for (const item of items) {
      if (item.module && !modules.includes(item.module)) continue;
      if (item.children) {
        const kids = this.filter(item.children);
        if (kids.length) out.push({ ...item, children: kids });
      } else if (!item.perm || this.session.can(`${item.perm}:view`)) {
        out.push(item);
      }
    }
    return out;
  }
}
