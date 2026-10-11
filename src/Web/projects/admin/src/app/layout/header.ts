import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LANGS, Language } from '../core/i18n';
import { AuthService } from '../core/auth.service';
import { Session } from '../core/session';
import { LayoutService } from './layout.service';

/** Thanh trên — như components/header của frontend monolith (nút thu gọn menu, menu người dùng). */
@Component({
  selector: 'app-header',
  imports: [RouterLink, TranslatePipe],
  host: { class: 'block shrink-0 z-20 relative' },
  template: `
    <header class="bg-white shadow-sm h-[60px] flex items-center justify-between px-4 z-10 shrink-0">
      @if (!session.me()?.mustChangePassword) {
        <button (click)="layout.toggleSidebar()" class="p-2 mr-2 rounded-lg hover:bg-gray-100 text-gray-600 transition-colors focus:outline-none">
          <span class="material-icons mt-1">menu</span>
        </button>
      } @else {
        <span></span>
      }

      <div class="flex items-center space-x-3">
        <div class="flex rounded-lg border border-gray-200 overflow-hidden text-xs font-semibold" role="group" [attr.aria-label]="'HEADER.LANGUAGE' | translate">
          @for (l of langs; track l) {
            <button type="button" (click)="language.use(l)" [attr.aria-pressed]="language.lang() === l" class="px-2 py-1.5 uppercase"
                    [class]="language.lang() === l ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'">{{ l }}</button>
          }
        </div>
        <div class="relative ml-2">
          <button (click)="layout.userMenuOpen.set(!layout.userMenuOpen()); $event.stopPropagation()"
                  class="flex items-center space-x-3 focus:outline-none p-1 rounded-full hover:bg-gray-50 transition-colors">
            <div class="hidden sm:block text-right">
              <div class="text-sm font-bold text-gray-800 leading-tight">{{ session.me()?.fullName }}</div>
              <div class="text-[11px] text-gray-500 leading-tight">{{ role() | translate: { code: session.features()?.code ?? '' } }}</div>
            </div>
            <img [src]="avatar()" class="w-9 h-9 rounded-full border border-gray-200 shadow-sm" alt="" />
          </button>

          @if (layout.userMenuOpen()) {
            <div class="absolute right-0 mt-2 w-52 bg-white rounded-lg shadow-lg border border-gray-100 py-1 z-50">
              <a routerLink="/profile" (click)="layout.userMenuOpen.set(false)" class="px-4 py-2 text-sm text-gray-700 hover:bg-blue-50 flex items-center transition-colors">
                <span class="material-icons mr-3 text-blue-500 text-[20px]">account_circle</span>
                {{ 'HEADER.PROFILE' | translate }}
              </a>
              @if (!session.readOnly()) {
                <a routerLink="/change-password" (click)="layout.userMenuOpen.set(false)" class="px-4 py-2 text-sm text-gray-700 hover:bg-blue-50 flex items-center transition-colors">
                  <span class="material-icons mr-3 text-emerald-500 text-[20px]">lock_reset</span>
                  {{ 'HEADER.CHANGE_PASSWORD' | translate }}
                </a>
              }
              <div class="border-t border-gray-100 my-1"></div>
              <button (click)="logout()" class="w-full text-left px-4 py-2 text-sm text-red-600 hover:bg-red-50 flex items-center transition-colors">
                <span class="material-icons mr-3 text-[20px]">logout</span>
                {{ 'HEADER.LOGOUT' | translate }}
              </button>
            </div>
          }
        </div>
      </div>
    </header>
  `,
})
export class Header {
  protected readonly layout = inject(LayoutService);
  protected readonly session = inject(Session);
  private readonly auth = inject(AuthService);
  protected readonly language = inject(Language);
  protected readonly langs = LANGS;

  /** Khoá dịch dòng vai trò dưới tên (tham số code = mã đơn vị). */
  protected readonly role = computed(() =>
    this.session.isSystem() ? 'HEADER.PLATFORM_ADMIN' : this.session.readOnly() ? 'HEADER.VIEWING_READONLY' : 'HEADER.TENANT_CODE');

  /** Ảnh chữ cái đầu vẽ tại chỗ (SVG) — không gọi dịch vụ ảnh bên ngoài như ui-avatars của bản cũ. */
  protected readonly avatar = computed(() => {
    const name = (this.session.me()?.fullName ?? '?').trim();
    const initials = name.split(/\s+/).slice(-2).map((w) => w[0]?.toUpperCase() ?? '').join('');
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="72" height="72"><rect width="72" height="72" fill="#0D6EFD"/><text x="50%" y="50%" dy=".35em" text-anchor="middle" font-family="Arial" font-size="28" fill="#fff">${initials}</text></svg>`;
    return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
  });

  protected async logout(): Promise<void> {
    this.session.clear();
    await this.auth.logout();
  }
}
