import { Injectable, computed, inject } from '@angular/core';
import { MODULE_NAMES, Session } from '../core/session';

export interface MenuItem {
  title: string;
  icon?: string;
  link?: string;
  /** Mã module quyền (USER, ORGS…) — hiện khi có quyền MODULE:view. */
  perm?: string;
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
    for (const item of items) {
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
