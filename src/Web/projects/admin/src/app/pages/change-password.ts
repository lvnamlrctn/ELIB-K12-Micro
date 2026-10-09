import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Api, errorMessage } from '../core/api';
import { Session } from '../core/session';
import { ToastrService } from '../shared/toastr';

/** Đổi mật khẩu — giao diện như pages/admin/change-password của admin cũ. */
@Component({
  selector: 'app-change-password',
  imports: [FormsModule],
  template: `
    <div class="mb-5">
      <h4 class="page-title">Đổi mật khẩu</h4>
      <p class="text-sm text-gray-500 mt-1">Bảo mật tài khoản của bạn với mật khẩu mới</p>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden max-w-2xl">
      <form (ngSubmit)="submit()">
        <div class="p-6 sm:p-8">
          @if (session.me()?.mustChangePassword) {
            <div class="mb-6 p-4 rounded-lg bg-amber-50 text-amber-800 border border-amber-200 flex items-start">
              <span class="material-icons mr-2">info</span>
              Đây là lần đăng nhập đầu tiên hoặc mật khẩu vừa được đặt lại. Vui lòng đổi mật khẩu để tiếp tục sử dụng.
            </div>
          }
          @if (error()) {
            <div class="mb-6 p-4 rounded-lg bg-red-50 text-red-700 border border-red-200 flex items-center">
              <span class="material-icons mr-2">error_outline</span>{{ error() }}
            </div>
          }

          <div class="space-y-5">
            @for (f of fields; track f.key) {
              <div>
                <label [for]="f.key" class="block text-sm font-semibold text-gray-700 mb-2">{{ f.label }}</label>
                <div class="relative">
                  <input type="password" [id]="f.key" [name]="f.key" [(ngModel)]="values[f.key]" [autocomplete]="f.autocomplete" required
                         class="w-full pl-10 pr-4 py-2.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow" />
                  <span class="material-icons absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-[20px]">{{ f.icon }}</span>
                </div>
                @if (f.hint) { <p class="text-xs text-gray-500 mt-1.5">{{ f.hint }}</p> }
              </div>
            }
          </div>
        </div>

        <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button type="button" (click)="clear()" class="btn-secondary bg-white">Huỷ</button>
          <button type="submit" [disabled]="busy()" class="btn-primary !px-6">
            <span class="material-icons text-[18px]">save</span>
            {{ busy() ? 'Đang cập nhật...' : 'Cập nhật mật khẩu' }}
          </button>
        </div>
      </form>
    </div>
  `,
})
export class ChangePassword {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly fields = [
    { key: 'current', label: 'Mật khẩu hiện tại', icon: 'lock', autocomplete: 'current-password', hint: '' },
    { key: 'next', label: 'Mật khẩu mới', icon: 'lock_reset', autocomplete: 'new-password', hint: 'Tối thiểu 8 ký tự, gồm cả chữ và số.' },
    { key: 'confirm', label: 'Xác nhận mật khẩu mới', icon: 'check_circle_outline', autocomplete: 'new-password', hint: '' },
  ];
  protected values: Record<string, string> = { current: '', next: '', confirm: '' };
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected clear(): void {
    this.values = { current: '', next: '', confirm: '' };
    this.error.set('');
  }

  protected async submit(): Promise<void> {
    this.error.set('');
    const { current, next, confirm } = this.values;
    if (!current || !next) {
      this.error.set('Vui lòng nhập đủ mật khẩu hiện tại và mật khẩu mới.');
      return;
    }
    if (next !== confirm) {
      this.error.set('Mật khẩu xác nhận không khớp.');
      return;
    }
    this.busy.set(true);
    try {
      const forced = this.session.me()?.mustChangePassword;
      await this.api.changePassword(current, next);
      this.clear();
      await this.session.load(true);
      this.toastr.success('Mật khẩu đã được thay đổi thành công!');
      if (forced) await this.router.navigateByUrl('/');
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
