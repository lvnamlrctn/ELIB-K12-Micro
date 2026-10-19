import { Injectable, computed, inject } from '@angular/core';
import { MODULE_NAMES, Session } from '../core/session';

export interface MenuItem {
  /** Khoá dịch (MENU.*) — cũng là khoá nhận diện nhóm đang mở. */
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
 * Menu cùng cấu trúc với menu admin của frontend monolith (services/menu.ts); title là khoá dịch MENU.* (tên khoá theo ELIB-LRC).
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
        { title: 'MENU.DASHBOARD', icon: 'dashboard', link: '/' },
        { title: 'MENU.PLATFORM_STATS', icon: 'insights', link: '/platform-stats' },
        {
          title: 'MENU.SYSTEM',
          icon: 'dns',
          children: [
            { title: 'MENU.TENANTS', icon: 'business', link: '/tenant' },
            { title: 'MENU.PLATFORM_LOGS', icon: 'receipt_long', link: '/platform-logs' },
          ],
        },
      ];
    }

    const modules = this.session.features()?.modules ?? [];
    return this.filter([
      { title: 'MENU.DASHBOARD', icon: 'dashboard', link: '/' },
      {
        title: 'MENU.READER_MGMT',
        icon: 'people',
        children: [
          { title: 'MENU.READERS', icon: 'people', link: '/readers', perm: 'READERS' },
          { title: 'MENU.READER_GROUPS', icon: 'groups', link: '/reader-groups', perm: 'GROUPREADER' },
          {
            title: 'MENU.READER_PARAMS',
            icon: 'tune',
            children: [
              { title: 'MENU.COURSES', icon: 'school', link: '/courses', perm: 'COURSES' },
              { title: 'MENU.CLASSES', icon: 'class', link: '/academic-classes', perm: 'CLASSES' },
              { title: 'MENU.READER_TYPES', icon: 'local_library', link: '/reader-types', perm: 'READER_TYPES' },
              { title: 'MENU.NATIONALITIES', icon: 'flag', link: '/nationalities', perm: 'NATIONALITIES' },
              { title: 'MENU.PROFS', icon: 'workspace_premium', link: '/profs', perm: 'PROFS' },
              { title: 'MENU.ETHNICS', icon: 'diversity_3', link: '/ethnics', perm: 'ETHNICS' },
              { title: 'MENU.DEGREES', icon: 'school', link: '/degrees', perm: 'DEGREES' },
              { title: 'MENU.POSITIONS', icon: 'badge', link: '/chuc-vus', perm: 'POSITIONS' },
              { title: 'MENU.ORGS', icon: 'account_tree', link: '/orgs', perm: 'ORGS' },
            ],
          },
        ],
      },
      {
        title: 'MENU.CATALOGING',
        icon: 'edit_note',
        module: 'CATALOG',
        children: [
          { title: 'MENU.CATALOG_BIBS', icon: 'menu_book', link: '/catalog-bibs', perm: 'CATALOG_BIBS' },
          { title: 'MENU.WORKSHEETS', icon: 'list_alt', link: '/worksheets', perm: 'WORKSHEETS' },
          { title: 'MENU.BIB_TYPES', icon: 'category', link: '/bib-types', perm: 'BIB_TYPES' },
          { title: 'MENU.DIC_AUTHORS', icon: 'person_search', link: '/dic-authors', perm: 'DIC_AUTHORS' },
          { title: 'MENU.DIC_PUBLISHERS', icon: 'business', link: '/dic-publishers', perm: 'DIC_PUBLISHERS' },
          { title: 'MENU.DIC_KEYWORDS', icon: 'sell', link: '/dic-keywords', perm: 'DIC_KEYWORDS' },
          { title: 'MENU.DIC_CLASSES', icon: 'account_tree', link: '/dic-classes', perm: 'DIC_CLASSES' },
          { title: 'MENU.ISBD_CONFIGS', icon: 'format_quote', link: '/isbd-configs', perm: 'CONFIG_ISBD' },
          { title: 'MENU.CATALOG_LOGS', icon: 'history_edu', link: '/catalog-logs', perm: 'CATALOG_BIBS' },
        ],
      },
      {
        title: 'MENU.CIRCULATION_GROUP',
        icon: 'swap_vert',
        module: 'CIRCULATION',
        children: [
          { title: 'MENU.BORROW', icon: 'swap_vert', link: '/borrow', perm: 'BORROW' },
          { title: 'MENU.REQUEST_BOOKS', icon: 'bookmark_add', link: '/request-books', perm: 'REQUEST_BOOKS' },
          { title: 'MENU.LOAN_HISTORY', icon: 'history', link: '/loan-history', perm: 'LOAN_HISTORY' },
          { title: 'MENU.CIRC_REPORT', icon: 'bar_chart', link: '/circulation-report', perm: 'CIRC_REPORT' },
          { title: 'MENU.FINES', icon: 'gavel', link: '/fines', perm: 'FINES' },
          { title: 'MENU.PHOTOCOPY', icon: 'print', link: '/photocopy', perm: 'C_PHOTO' },
          { title: 'MENU.FINE_REASONS', icon: 'rule_folder', link: '/cfine-types', perm: 'FINE_REASONS' },
          { title: 'MENU.CIRC_POLICIES', icon: 'rule', link: '/circ-policies', perm: 'CIRC_POLICIES' },
          { title: 'MENU.CIRC_PLACES', icon: 'place', link: '/circ-places', perm: 'CIRC_PLACES' },
          { title: 'MENU.CHECK_IN_OUT', icon: 'badge', link: '/check-in-out', perm: 'CHECK_IN_OUT' },
          { title: 'MENU.CHECKIN_HISTORY', icon: 'history_toggle_off', link: '/checkin-history', perm: 'CHECK_IN_OUT' },
          { title: 'MENU.SIP2_STATIONS', icon: 'point_of_sale', link: '/sip2-stations', perm: 'SIP2LOG' },
          { title: 'MENU.SIP2_LOGS', icon: 'receipt_long', link: '/sip2-logs', perm: 'SIP2LOG' },
        ],
      },
      {
        title: 'MENU.SEARCH_GROUP',
        icon: 'travel_explore',
        module: 'SEARCH',
        children: [
          { title: 'MENU.SEARCH_INDEX', icon: 'manage_search', link: '/search-index', perm: 'SEARCH_INDEX' },
          { title: 'MENU.SEARCH_STATS', icon: 'query_stats', link: '/search-stats', perm: 'SEARCH_STATS' },
          { title: 'MENU.Z3950_SEARCH', icon: 'travel_explore', link: '/z3950-search', perm: 'CATALOG_BIBS' },
          { title: 'MENU.Z3950_CONFIGS', icon: 'dns', link: '/z3950-configs', perm: 'Z3950_CONFIGS' },
        ],
      },
      {
        title: 'MENU.ACQUISITION_GROUP',
        icon: 'add_shopping_cart',
        module: 'ACQUISITION',
        children: [
          { title: 'MENU.AB_ORDERS', icon: 'shopping_cart', link: '/ab-orders', perm: 'AB_ORDERS' },
          { title: 'MENU.AB_RECEIPTS', icon: 'receipt_long', link: '/ab-receipts', perm: 'AB_RECEIPTS' },
          { title: 'MENU.AB_RECEIPT_LOOKUP', icon: 'manage_search', link: '/ab-receipts/lookup', perm: 'AB_RECEIPTS' },
          { title: 'MENU.ACQ_REPORTS', icon: 'assessment', link: '/ab-reports', perm: 'AB_RECEIPTS' },
          { title: 'MENU.AB_SOURCES', icon: 'source', link: '/ab-sources', perm: 'AB_SOURCES' },
          { title: 'MENU.SUPPLIERS', icon: 'local_shipping', link: '/suppliers', perm: 'SUPPLIERS' },
          { title: 'MENU.BUDGETS', icon: 'account_balance', link: '/budgets', perm: 'BUDGETS' },
          { title: 'MENU.FUNDS', icon: 'savings', link: '/funds', perm: 'FUNDS' },
        ],
      },
      {
        title: 'MENU.DIGITAL_GROUP',
        icon: 'menu_book',
        module: 'DIGITAL',
        children: [
          { title: 'MENU.DIGITAL_ITEMS', icon: 'menu_book', link: '/digital-items', perm: 'DIGITAL_ITEMS' },
          { title: 'MENU.DIGITAL_LOANS', icon: 'swap_vert', link: '/digital-loans', perm: 'DIGITAL_LOANS' },
          { title: 'MENU.DIGITAL_REVIEWS', icon: 'reviews', link: '/digital-reviews', perm: 'DIGITAL_REVIEWS' },
          { title: 'MENU.DIGITAL_SUBMISSIONS', icon: 'upload_file', link: '/digital-submissions', perm: 'DIGITAL_SUBMISSIONS' },
          { title: 'MENU.DIGITAL_COLLECTIONS', icon: 'collections_bookmark', link: '/digital-collections', perm: 'DIGITAL_COLLECTIONS' },
          { title: 'MENU.DIGITAL_SUBJECTS', icon: 'category', link: '/digital-subjects', perm: 'DIGITAL_SUBJECTS' },
          { title: 'MENU.DIGITAL_TOPICS', icon: 'sell', link: '/digital-topics', perm: 'DIGITAL_TOPICS' },
          { title: 'MENU.DIGITAL_DIG_TYPES', icon: 'description', link: '/digital-dig-types', perm: 'DIGITAL_DIG_TYPES' },
          { title: 'MENU.DIGITAL_POLICIES', icon: 'rule', link: '/digital-policies', perm: 'DIGITAL_POLICIES' },
          { title: 'MENU.DIGITAL_POLICIES_BY_COLLECTION', icon: 'rule_folder', link: '/digital-policies-by-collection', perm: 'DIGITAL_POLICIES_BY_COLLECTION' },
        ],
      },
      {
        title: 'MENU.STORE_MGMT',
        icon: 'store',
        module: 'HOLDINGS',
        children: [
          { title: 'MENU.DOC_SEARCH', icon: 'manage_search', link: '/books', perm: 'DOC_SEARCH' },
          { title: 'MENU.SHELVING', icon: 'shelves', link: '/shelving', perm: 'MAP_SHELVING' },
          { title: 'MENU.PRINT_LABELS', icon: 'label', link: '/print-labels', perm: 'PRINT_LABEL' },
          { title: 'MENU.DELIVERIES', icon: 'local_shipping', link: '/ab-deliverers', perm: 'AB_DELIVERERS' },
          { title: 'MENU.STORE_MOVES', icon: 'swap_horiz', link: '/ab-moves', perm: 'AB_MOVES' },
          { title: 'MENU.INVENTORY', icon: 'inventory', link: '/inventory', perm: 'INVENTORY' },
          { title: 'MENU.BOOK_OUT_STORE', icon: 'qr_code_scanner', link: '/book-out-store', perm: 'BOOK_OUT_STORE' },
          { title: 'MENU.LOST_BOOKS', icon: 'report', link: '/lost-books', perm: 'LOST_BOOKS' },
          { title: 'MENU.LIQUIDATES', icon: 'delete_sweep', link: '/liquidates', perm: 'LIQUIDATES' },
          { title: 'MENU.RE_REGISTER', icon: 'published_with_changes', link: '/re-register-barcode', perm: 'RE_REGISTER' },
          { title: 'MENU.STAT_DOC', icon: 'query_stats', link: '/statistics-document', perm: 'STAT_DOC' },
          { title: 'MENU.STORE_BOOK_REPORT', icon: 'menu_book', link: '/store-book-report', perm: 'STORE_BOOK_REPORT' },
          { title: 'MENU.STORES', icon: 'warehouse', link: '/stores', perm: 'STORES' },
          { title: 'MENU.STORE_TYPES', icon: 'category', link: '/store-types', perm: 'STORE_TYPES' },
          { title: 'MENU.EXPORT_REASONS', icon: 'list_alt', link: '/export-reason', perm: 'EXPORT_REASON' },
          { title: 'MENU.BOOK_OUT_UNITS', icon: 'apartment', link: '/book-out-unit', perm: 'BOOK_OUT_UNIT' },
          { title: 'MENU.EXHIBITION_LOCATIONS', icon: 'location_on', link: '/exhibition-location', perm: 'EXHIBITION_LOCATION' },
        ],
      },
      {
        title: 'MENU.MODULES_OWNED',
        icon: 'apps',
        children: modules.map((code) => ({ title: MODULE_NAMES[code] ?? code, icon: MODULE_ICONS[code] ?? 'extension', link: `/phan-he/${code}` })),
      },
      {
        title: 'MENU.SYSTEM',
        icon: 'dns',
        children: [
          { title: 'MENU.USERS', icon: 'manage_accounts', link: '/users', perm: 'USER' },
          { title: 'MENU.ROLES', icon: 'admin_panel_settings', link: '/roles', perm: 'ROLE' },
          { title: 'MENU.SYSTEM_PARAMS', icon: 'settings_applications', link: '/system-parameters', perm: 'SYSTEM_PARAMS' },
          { title: 'MENU.CURRENCIES', icon: 'currency_exchange', link: '/currencies', perm: 'CURRENCIES' },
          { title: 'MENU.SYSTEM_LOG', icon: 'receipt_long', link: '/system-logs', perm: 'SYSTEM_LOG' },
          {
            title: 'MENU.NOTIFICATIONS',
            icon: 'mail',
            children: [
              { title: 'MENU.EMAIL_SETTINGS', icon: 'settings', link: '/email-settings', perm: 'NOTIFICATION_CONFIG' },
              { title: 'MENU.EMAIL_TEMPLATES', icon: 'drafts', link: '/email-templates', perm: 'NOTIFICATION_TEMPLATES' },
              { title: 'MENU.NOTIFICATION_LOGS', icon: 'history', link: '/notification-logs', perm: 'NOTIFICATION_LOGS' },
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
