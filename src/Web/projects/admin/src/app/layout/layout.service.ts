import { Injectable, signal } from '@angular/core';

/** Trạng thái khung admin — như services/layout.ts của frontend monolith (sidebar kéo giãn, thu gọn, menu người dùng). */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  static readonly SIDEBAR_MIN_WIDTH = 200;
  static readonly SIDEBAR_MAX_WIDTH = 420;
  private static readonly SIDEBAR_DEFAULT_WIDTH = 260;
  private static readonly SIDEBAR_WIDTH_KEY = 'sidebar-width';

  readonly desktopMenuOpen = signal(true);
  readonly mobileMenuOpen = signal(false);
  readonly userMenuOpen = signal(false);
  readonly sidebarWidth = signal<number>(this.loadSidebarWidth());

  setSidebarWidth(px: number): void {
    const clamped = Math.min(LayoutService.SIDEBAR_MAX_WIDTH, Math.max(LayoutService.SIDEBAR_MIN_WIDTH, Math.round(px)));
    this.sidebarWidth.set(clamped);
    try {
      localStorage.setItem(LayoutService.SIDEBAR_WIDTH_KEY, String(clamped));
    } catch {
      /* không lưu được (chế độ riêng tư) — chỉ giữ trong phiên */
    }
  }

  toggleSidebar(): void {
    if (window.innerWidth >= 768) this.desktopMenuOpen.update((v) => !v);
    else this.mobileMenuOpen.update((v) => !v);
  }

  closeMenus(): void {
    this.userMenuOpen.set(false);
  }

  private loadSidebarWidth(): number {
    try {
      const raw = Number(localStorage.getItem(LayoutService.SIDEBAR_WIDTH_KEY));
      if (raw >= LayoutService.SIDEBAR_MIN_WIDTH && raw <= LayoutService.SIDEBAR_MAX_WIDTH) return raw;
    } catch {
      /* localStorage không khả dụng — dùng mặc định */
    }
    return LayoutService.SIDEBAR_DEFAULT_WIDTH;
  }
}
