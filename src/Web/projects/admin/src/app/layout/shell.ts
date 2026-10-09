import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { Session } from '../core/session';
import { Header } from './header';
import { LayoutService } from './layout.service';
import { Sidebar } from './sidebar';

/** Khung trang admin — bố cục như pages/admin/layout của frontend monolith. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, Sidebar, Header, DatePipe],
  template: `
    <div class="flex h-screen bg-gray-100 overflow-hidden font-sans">
      @if (!session.me()?.mustChangePassword) {
        <app-sidebar />
      }
      <div class="flex-1 flex flex-col min-w-0 overflow-hidden relative">
        <app-header />
        @if (session.readOnly()) {
          <div class="bg-amber-50 border-b border-amber-200 text-amber-800 text-sm px-4 py-2 flex items-center gap-2 shrink-0">
            <span class="material-icons text-[20px]">visibility</span>
            <span class="flex-1">
              Bạn đang xem <b>{{ session.features()?.name }}</b> với vai quản trị nền tảng — <b>chỉ đọc</b>, mọi thao tác thêm/sửa/xoá đều bị chặn.
              Phiên hết hạn lúc {{ session.me()?.expiresAt | date: 'HH:mm' }}.
            </span>
            <button (click)="exit()" class="btn !py-1 !px-3 bg-amber-600 hover:bg-amber-700 text-white"><span class="material-icons text-[16px]">logout</span>Thoát</button>
          </div>
        }
        <main class="flex-1 overflow-y-auto bg-[#f0f2f5] p-4 lg:p-6 custom-scrollbar" (click)="layout.closeMenus()">
          <router-outlet />
        </main>
        <footer class="bg-white border-t border-gray-200 py-4 px-6 text-center text-sm text-gray-500 shrink-0">
          Phần mềm Quản lý thư viện <strong class="text-gray-700 font-semibold">ELIB-K12</strong> &copy; {{ year }}
        </footer>
      </div>
    </div>
  `,
})
export class Shell {
  protected readonly session = inject(Session);
  protected readonly layout = inject(LayoutService);
  protected readonly year = new Date().getFullYear();
  private readonly auth = inject(AuthService);

  protected async exit(): Promise<void> {
    this.session.clear();
    await this.auth.logout();
  }
}
