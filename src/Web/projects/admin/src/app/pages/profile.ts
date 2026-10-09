import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Session } from '../core/session';

/** Thông tin cá nhân — tài khoản đang đăng nhập (chỉ xem; sửa hồ sơ do quản trị đơn vị làm ở Cán bộ quản lý thư viện). */
@Component({
  selector: 'app-profile',
  imports: [RouterLink],
  template: `
    <div class="mb-5">
      <h4 class="page-title">Thông tin cá nhân</h4>
      <p class="text-sm text-gray-500 mt-1">Tài khoản đang đăng nhập</p>
    </div>

    @if (session.me(); as me) {
      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden max-w-2xl">
        <div class="p-6 sm:p-8 flex items-center gap-5 border-b border-gray-100">
          <div class="w-16 h-16 rounded-full bg-blue-600 text-white flex items-center justify-center text-2xl font-bold">{{ initials() }}</div>
          <div>
            <div class="text-lg font-bold text-gray-800">{{ me.fullName }}</div>
            <div class="text-sm text-gray-500">{{ me.userName }}</div>
          </div>
        </div>
        <dl class="p-6 sm:p-8 grid grid-cols-1 sm:grid-cols-3 gap-y-4 text-sm">
          <dt class="text-gray-500">Đơn vị</dt>
          <dd class="sm:col-span-2 font-medium text-gray-800">{{ session.isSystem() ? 'Quản trị nền tảng' : session.features()?.name + ' (' + session.features()?.code + ')' }}</dd>
          <dt class="text-gray-500">Quyền</dt>
          <dd class="sm:col-span-2 text-gray-800">
            @if (me.permissions.includes('*')) { <span class="badge-info">Toàn quyền</span> }
            @else { {{ me.permissions.length }} quyền }
          </dd>
        </dl>
        <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex justify-end">
          <a routerLink="/change-password" class="btn-primary"><span class="material-icons text-[18px]">lock_reset</span>Đổi mật khẩu</a>
        </div>
      </div>
    }
  `,
})
export class Profile {
  protected readonly session = inject(Session);
  protected readonly initials = computed(() =>
    (this.session.me()?.fullName ?? '?').trim().split(/\s+/).slice(-2).map((w) => w[0]?.toUpperCase() ?? '').join(''));
}
