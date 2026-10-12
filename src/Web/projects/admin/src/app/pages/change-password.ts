import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Api, errorMessage } from '../core/api';
import { TranslatePipe } from '@ngx-translate/core';
import { Session } from '../core/session';
import { ToastrService } from '../shared/toastr';

/** Đổi mật khẩu — giao diện như pages/admin/change-password của admin cũ. */
@Component({
  selector: 'app-change-password',
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="mb-5">
      <h4 class="page-title">{{ 'CHANGE_PASSWORD.TITLE' | translate }}</h4>
      <p class="text-sm text-gray-500 mt-1">{{ 'CHANGE_PASSWORD.SUBTITLE' | translate }}</p>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden max-w-2xl">
      <form (ngSubmit)="submit()">
        <div class="p-6 sm:p-8">
          @if (session.me()?.mustChangePassword) {
            <div class="mb-6 p-4 rounded-lg bg-amber-50 text-amber-800 border border-amber-200 flex items-start">
              <span class="material-icons mr-2">info</span>
              {{ 'CHANGE_PASSWORD.FIRST_LOGIN' | translate }}
            </div>
          }
          @if (error()) {
            <div class="mb-6 p-4 rounded-lg bg-red-50 text-red-700 border border-red-200 flex items-center">
              <span class="material-icons mr-2">error_outline</span>{{ error() | translate }}
            </div>
          }

          <div class="space-y-5">
            @for (f of fields; track f.key) {
              <div>
                <label [for]="f.key" class="block text-sm font-semibold text-gray-700 mb-2">{{ f.label | translate }}</label>
                <div class="relative">
                  <input type="password" [id]="f.key" [name]="f.key" [(ngModel)]="values[f.key]" [autocomplete]="f.autocomplete" required
                         class="w-full pl-10 pr-4 py-2.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow" />
                  <span class="material-icons absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-[20px]">{{ f.icon }}</span>
                </div>
                @if (f.hint) { <p class="text-xs text-gray-500 mt-1.5">{{ f.hint | translate }}</p> }
              </div>
            }
          </div>
        </div>

        <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button type="button" (click)="clear()" class="btn-secondary bg-white">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" [disabled]="busy()" class="btn-primary !px-6">
            <span class="material-icons text-[18px]">save</span>
            {{ (busy() ? 'CHANGE_PASSWORD.UPDATING' : 'CHANGE_PASSWORD.UPDATE_PASSWORD') | translate }}
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
    { key: 'current', label: 'CHANGE_PASSWORD.CURRENT_PASSWORD', icon: 'lock', autocomplete: 'current-password', hint: '' },
    { key: 'next', label: 'CHANGE_PASSWORD.NEW_PASSWORD', icon: 'lock_reset', autocomplete: 'new-password', hint: 'CHANGE_PASSWORD.NEW_HINT' },
    { key: 'confirm', label: 'CHANGE_PASSWORD.CONFIRM_NEW_PASSWORD', icon: 'check_circle_outline', autocomplete: 'new-password', hint: '' },
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
      this.error.set('CHANGE_PASSWORD.ERR_BOTH_REQUIRED');
      return;
    }
    if (next !== confirm) {
      this.error.set('CHANGE_PASSWORD.ERR_MISMATCH');
      return;
    }
    this.busy.set(true);
    try {
      const forced = this.session.me()?.mustChangePassword;
      await this.api.changePassword(current, next);
      this.clear();
      await this.session.load(true);
      this.toastr.success('CHANGE_PASSWORD.SUCCESS_MSG');
      if (forced) await this.router.navigateByUrl('/');
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
