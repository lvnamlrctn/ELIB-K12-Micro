import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LDatePipe, Language } from '../core/i18n';
import { ReaderAuth, ReaderMe } from '../core/reader-auth';

/** Tài khoản bạn đọc: chưa đăng nhập → nút đăng nhập; đã đăng nhập → thông tin thẻ, đổi mật khẩu, đăng xuất. */
@Component({
  selector: 'opac-account',
  imports: [FormsModule, LDatePipe, TranslatePipe],
  template: `
    <div class="max-w-3xl mx-auto px-4 py-8">
      @if (!auth.loggedIn()) {
        <div class="bg-white rounded-2xl border border-slate-200 p-8 text-center">
          <span class="material-icons text-blue-600 text-5xl">account_circle</span>
          <h1 class="text-xl font-semibold text-slate-900 mt-3">{{ 'OPAC.MY_LIBRARY.ACCOUNT_TITLE' | translate }}</h1>
          <p class="text-slate-600 mt-2">{{ 'OPAC.MY_LIBRARY.LOGIN_HINT' | translate }}</p>
          @if (error()) { <p class="text-red-600 text-sm mt-3" role="alert">{{ error() }}</p> }
          <button type="button" class="mt-6 inline-flex items-center gap-2 px-5 py-2.5 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700"
                  (click)="auth.login('/tai-khoan')">
            <span class="material-icons text-[20px]">login</span>{{ 'OPAC.NAVBAR.LOGIN' | translate }}
          </button>
          <p class="text-xs text-slate-500 mt-4">{{ 'OPAC.MY_LIBRARY.NO_PASSWORD_HINT' | translate }}</p>
        </div>
      } @else {
        <div class="flex items-center justify-between gap-3 mb-6 flex-wrap">
          <h1 class="text-2xl font-semibold text-slate-900">{{ 'OPAC.NAVBAR.MY_ACCOUNT' | translate }}</h1>
          <button type="button" class="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-50"
                  (click)="auth.logout()"><span class="material-icons text-[18px]">logout</span>{{ 'OPAC.NAVBAR.LOGOUT' | translate }}</button>
        </div>

        @if (me(); as m) {
          <section class="bg-white rounded-2xl border border-slate-200 p-6">
            <div class="flex items-center gap-4">
              <span class="w-14 h-14 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
                <span class="material-icons text-3xl">badge</span></span>
              <div class="min-w-0">
                <div class="text-lg font-semibold text-slate-900">{{ m.fullName }}</div>
                <div class="text-slate-600 text-sm">{{ 'OPAC.MY_LIBRARY.CARD_NO' | translate }} <span class="font-mono">{{ m.cardNo }}</span></div>
              </div>
            </div>
            <dl class="grid grid-cols-1 sm:grid-cols-3 gap-4 mt-6 text-sm">
              <div><dt class="text-slate-500">Email</dt><dd class="text-slate-800 break-all">{{ m.email || '—' }}</dd></div>
              <div><dt class="text-slate-500">{{ 'OPAC.MY_LIBRARY.EXPIRE_DATE' | translate }}</dt>
                <dd [class.text-red-600]="expired(m)">{{ m.expireDate ? (m.expireDate | ldate) : ('OPAC.MY_LIBRARY.NO_EXPIRY' | translate) }}{{ expired(m) ? ' ' + ('OPAC.MY_LIBRARY.EXPIRED' | translate) : '' }}</dd></div>
              <div><dt class="text-slate-500">{{ 'OPAC.MY_LIBRARY.THIS_LOGIN' | translate }}</dt><dd class="text-slate-800">{{ m.lastLoginAt ? (m.lastLoginAt | ldate: 'dd/MM/yyyy HH:mm') : '—' }}</dd></div>
            </dl>
          </section>
        } @else if (error()) {
          <p class="text-red-600" role="alert">{{ error() }}</p>
        } @else {
          <div class="flex justify-center py-12"><span class="material-icons animate-spin text-blue-600 text-3xl">autorenew</span></div>
        }

        <section class="bg-white rounded-2xl border border-slate-200 p-6 mt-6">
          <h2 class="font-semibold text-slate-900 mb-4">{{ 'OPAC.MY_LIBRARY.CHANGE_PASSWORD' | translate }}</h2>
          <form class="grid grid-cols-1 sm:grid-cols-3 gap-4 items-end" (ngSubmit)="changePassword()">
            <label class="block text-sm"><span class="text-slate-600">{{ 'OPAC.MY_LIBRARY.CURRENT_PASSWORD' | translate }}</span>
              <input type="password" name="current" autocomplete="current-password" [(ngModel)]="current" required
                     class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" /></label>
            <label class="block text-sm"><span class="text-slate-600">{{ 'OPAC.MY_LIBRARY.NEW_PASSWORD' | translate }}</span>
              <input type="password" name="next" autocomplete="new-password" [(ngModel)]="next" required minlength="6"
                     class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" /></label>
            <label class="block text-sm"><span class="text-slate-600">{{ 'OPAC.MY_LIBRARY.CONFIRM_PASSWORD' | translate }}</span>
              <input type="password" name="confirm" autocomplete="new-password" [(ngModel)]="confirm" required
                     class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" /></label>
            <div class="sm:col-span-3 flex items-center gap-3 flex-wrap">
              <button type="submit" [disabled]="busy()"
                      class="px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50">{{ 'OPAC.MY_LIBRARY.CHANGE_PASSWORD' | translate }}</button>
              @if (pwMessage(); as msg) {
                <span class="text-sm" [class.text-green-700]="msg.ok" [class.text-red-600]="!msg.ok" role="status">{{ msg.text | translate }}</span>
              }
            </div>
          </form>
        </section>

        <section class="rounded-2xl border border-dashed border-slate-300 p-6 mt-6 text-sm text-slate-600">
          <span class="material-icons align-middle text-slate-400 mr-1">schedule</span>
          {{ 'OPAC.MY_LIBRARY.LOANS_COMING' | translate }}
        </section>
      }
    </div>
  `,
})
export class Account implements OnInit {
  protected readonly auth = inject(ReaderAuth);
  private readonly language = inject(Language);
  protected readonly me = signal<ReaderMe | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly pwMessage = signal<{ ok: boolean; text: string } | null>(null);
  protected current = '';
  protected next = '';
  protected confirm = '';

  async ngOnInit(): Promise<void> {
    if (!(await this.auth.restore())) return;
    try {
      this.me.set(await this.auth.me());
    } catch (e) {
      if (e instanceof HttpErrorResponse && (e.status === 401 || e.status === 403)) {
        // Token bị thu hồi (thẻ khoá, thư viện đặt lại mật khẩu…) — đăng nhập lại.
        await this.auth.forget();
      }
      this.error.set(this.language.apiError(e));
    }
  }

  protected expired(m: ReaderMe): boolean {
    return !!m.expireDate && m.expireDate < new Date().toISOString().slice(0, 10);
  }

  protected async changePassword(): Promise<void> {
    if (this.next.length < 6) return this.pwMessage.set({ ok: false, text: 'OPAC.MY_LIBRARY.PASSWORD_MIN' });
    if (this.next !== this.confirm) return this.pwMessage.set({ ok: false, text: 'OPAC.MY_LIBRARY.PASSWORD_MISMATCH' });
    this.busy.set(true);
    try {
      await this.auth.changePassword(this.current, this.next);
      this.current = this.next = this.confirm = '';
      this.pwMessage.set({ ok: true, text: 'OPAC.MY_LIBRARY.PASSWORD_CHANGED' });
    } catch (e) {
      this.pwMessage.set({ ok: false, text: this.language.apiError(e) });
    } finally {
      this.busy.set(false);
    }
  }
}
