import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class LayoutService {
  desktopMenuOpen = signal(true);
  mobileMenuOpen = signal(false);
  langOpen = signal(false);
  userMenuOpen = signal(false);

  private static readonly SIDEBAR_WIDTH_KEY = 'sidebar-width';
  static readonly SIDEBAR_MIN_WIDTH = 200;
  static readonly SIDEBAR_MAX_WIDTH = 420;
  private static readonly SIDEBAR_DEFAULT_WIDTH = 260;

  sidebarWidth = signal<number>(this.loadSidebarWidth());

  private loadSidebarWidth(): number {
    try {
      const raw = Number(localStorage.getItem(LayoutService.SIDEBAR_WIDTH_KEY));
      if (raw && raw >= LayoutService.SIDEBAR_MIN_WIDTH && raw <= LayoutService.SIDEBAR_MAX_WIDTH) return raw;
    } catch { /* localStorage không khả dụng (SSR/private mode) — dùng giá trị mặc định */ }
    return LayoutService.SIDEBAR_DEFAULT_WIDTH;
  }

  setSidebarWidth(px: number): void {
    const clamped = Math.min(LayoutService.SIDEBAR_MAX_WIDTH, Math.max(LayoutService.SIDEBAR_MIN_WIDTH, Math.round(px)));
    this.sidebarWidth.set(clamped);
    try { localStorage.setItem(LayoutService.SIDEBAR_WIDTH_KEY, String(clamped)); } catch { /* bỏ qua nếu không lưu được */ }
  }

  toggleSidebar() {
    if (window.innerWidth >= 768) {
      this.desktopMenuOpen.update(v => !v);
    } else {
      this.mobileMenuOpen.update(v => !v);
    }
  }

  closeMenus() {
    this.langOpen.set(false);
    this.userMenuOpen.set(false);
  }
}
