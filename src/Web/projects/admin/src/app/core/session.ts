import { Injectable, computed, inject, signal } from '@angular/core';
import { Api, Features, Me } from './api';

/** Tên hiển thị module bán (đồng bộ với ModuleCatalog của service tenant). */
export const MODULE_NAMES: Record<string, string> = {
  CATALOG: 'Biên mục',
  HOLDINGS: 'Quản lý kho',
  CIRCULATION: 'Lưu thông',
  ACQUISITION: 'Bổ sung',
  SERIALS: 'Ấn phẩm định kỳ',
  DIGITAL: 'Thư viện số',
  SEARCH: 'Tra cứu OPAC',
  PAYMENT: 'Thanh toán trực tuyến',
  SPACE: 'Không gian & đặt phòng',
  AI: 'Trợ lý AI',
  PORTAL: 'Cổng thông tin',
  REPORTING: 'Báo cáo tổng hợp',
  SCHOOL: 'Chương trình, EOffice, Huy hiệu',
};

/**
 * Người đang đăng nhập + đơn vị của host. tenantId = null → quản trị nền tảng (host hệ thống).
 * Menu ẩn/hiện theo license (docs 08 §6) — chỉ là giao diện; gateway và service vẫn tự chặn.
 */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly api = inject(Api);

  readonly me = signal<Me | null>(null);
  readonly features = signal<Features | null>(null);
  readonly isSystem = computed(() => this.me()?.tenantId == null);

  private loading: Promise<void> | null = null;

  load(force = false): Promise<void> {
    if (!force && this.loading) return this.loading;
    this.loading = (async () => {
      const me = await this.api.me();
      this.me.set(me);
      this.features.set(me.tenantId == null ? null : await this.api.features());
    })();
    this.loading.catch(() => (this.loading = null));
    return this.loading;
  }

  /** Quyền dạng MODULE:action. "*" = toàn quyền; "*:view" = xem mọi chức năng (quản trị nền tảng đóng vai đơn vị). */
  can(permission: string): boolean {
    const granted = this.me()?.permissions ?? [];
    return granted.includes('*') || granted.includes(permission) || (granted.includes('*:view') && permission.endsWith(':view'));
  }

  readonly readOnly = computed(() => !!this.me()?.readOnly);

  clear(): void {
    this.me.set(null);
    this.features.set(null);
    this.loading = null;
  }
}
