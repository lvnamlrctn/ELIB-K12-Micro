import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LDatePipe } from '../../core/i18n';
import { Api, Reader, ReaderAccount, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';

/** Nhãn tình trạng đăng nhập OPAC trên danh sách bạn đọc (text: khoá dịch). */
export function accountLabel(a: ReaderAccount | undefined): { text: string; css: string } {
  if (!a) return { text: '—', css: 'text-gray-400' };
  if (a.lockedOut) return { text: 'READER_PASSWORD.STATE_LOCKED_OUT', css: 'bg-red-50 text-red-700' };
  if (!a.hasPassword) return { text: 'READER_PASSWORD.STATE_NONE', css: 'bg-gray-100 text-gray-600' };
  if (a.cardNoPassword) return { text: 'READER_PASSWORD.STATE_CARDNO', css: 'bg-amber-50 text-amber-700' };
  return { text: 'READER_PASSWORD.STATE_SET', css: 'bg-green-50 text-green-700' };
}

/**
 * Mật khẩu đăng nhập OPAC (monolith: Reader/ResetPassword, BulkResetPassword). Mật khẩu nằm ở identity, không đi cùng hồ sơ bạn đọc.
 * Một bạn đọc (account có giá trị) hoặc nhiều bạn đọc đã chọn; bạn đọc luôn phải đổi mật khẩu ở lần đăng nhập đầu.
 */
@Component({
  selector: 'app-reader-password',
  imports: [FormsModule, LDatePipe, Modal, TranslatePipe],
  template: `
    <app-modal [title]="bulk() ? ('READER_PASSWORD.TITLE_BULK' | translate: { count: readers().length }) : ('READER_PASSWORD.TITLE_ONE' | translate: { card: readers()[0].cardNo })" (closed)="closed.emit()">
      @if (!bulk()) {
        <div class="text-sm text-gray-600 mb-4 space-y-1">
          <div><b>{{ readers()[0].fullName }}</b> — <span class="font-mono">{{ readers()[0].cardNo }}</span></div>
          @if (account(); as a) {
            <div>{{ 'READER_PASSWORD.CURRENT' | translate }}: {{ label(a).text | translate }}@if (a.hasPassword && a.mustChangePassword) { <span>, {{ 'READER_PASSWORD.NOT_CHANGED_YET' | translate }}</span> }</div>
            <div>{{ 'READER_PASSWORD.LAST_LOGIN' | translate }}: {{ a.lastLoginAt ? (a.lastLoginAt | ldate: 'dd/MM/yyyy HH:mm') : ('READER_PASSWORD.NEVER' | translate) }}</div>
            @if (a.lockedOut) {
              <div class="flex items-center gap-2 text-red-700">{{ 'READER_PASSWORD.LOCKED_OUT_TEXT' | translate }}
                <button type="button" class="btn-secondary !py-1 !px-2" [disabled]="busy()" (click)="unlock()">{{ 'READER_PASSWORD.UNLOCK' | translate }}</button></div>
            }
          }
        </div>
      }
      <fieldset class="space-y-3">
        <legend class="form-label">{{ 'READER.NEW_PASSWORD' | translate }}</legend>
        <label class="flex items-start gap-2 text-sm cursor-pointer">
          <input type="radio" name="pw-mode" class="mt-1" [checked]="useCardNo()" (change)="useCardNo.set(true)" />
          <span>{{ 'READER_PASSWORD.USE_CARDNO' | translate }}<span class="block text-xs text-gray-500">{{ 'READER_PASSWORD.USE_CARDNO_HINT' | translate }}</span></span>
        </label>
        <label class="flex items-start gap-2 text-sm cursor-pointer">
          <input type="radio" name="pw-mode" class="mt-1" [checked]="!useCardNo()" (change)="useCardNo.set(false)" />
          <span>{{ (bulk() ? 'READER_PASSWORD.SET_COMMON' : 'READER_PASSWORD.SET') | translate }}</span>
        </label>
        @if (!useCardNo()) {
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pl-6">
            <div><label class="form-label" for="pw-new">{{ 'READER.PASSWORD' | translate }}</label>
              <input id="pw-new" type="password" class="input" autocomplete="new-password" [(ngModel)]="password" /></div>
            <div><label class="form-label" for="pw-confirm">{{ 'READER.CONFIRM_PASSWORD' | translate }}</label>
              <input id="pw-confirm" type="password" class="input" autocomplete="new-password" [(ngModel)]="confirm" /></div>
          </div>
        }
      </fieldset>
      <p class="text-xs text-gray-500 mt-4">{{ 'READER_PASSWORD.RULES' | translate }}</p>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="busy()" (click)="save()"><span class="material-icons text-[18px]">key</span>{{ 'READER_PASSWORD.SAVE' | translate }}</button>
      </ng-container>
    </app-modal>
  `,
})
export class ReaderPassword {
  readonly readers = input.required<Pick<Reader, 'publicId' | 'cardNo' | 'fullName'>[]>();
  readonly account = input<ReaderAccount | undefined>();
  /** Bạn đọc đã chọn trên danh sách (có thể chỉ một) — dùng API hàng loạt, không hiện thông tin từng người. */
  readonly bulk = input(false);
  readonly closed = output<void>();
  readonly saved = output<void>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly useCardNo = signal(true);
  protected readonly busy = signal(false);
  protected password = '';
  protected confirm = '';
  protected readonly label = accountLabel;

  protected async save(): Promise<void> {
    const useCardNo = this.useCardNo();
    if (!useCardNo) {
      if (this.password.length < 6) {
        this.toastr.warning('READER.RESET_PASSWORD_MIN');
        return;
      }
      if (this.password !== this.confirm) {
        this.toastr.warning('READER.PASSWORD_MISMATCH');
        return;
      }
    }
    const password = useCardNo ? null : this.password;
    const readers = this.readers();
    this.busy.set(true);
    try {
      if (!this.bulk()) {
        await this.api.setReaderPassword(readers[0].publicId, password, useCardNo);
        this.toastr.success('READER_PASSWORD.DONE_ONE');
      } else {
        const r = await this.api.bulkSetReaderPassword(readers.map((x) => x.publicId), password, useCardNo);
        this.toastr.success('READER_PASSWORD.DONE_BULK', { count: r.updatedCount });
        if (r.notFound.length) this.toastr.warning('READER_PASSWORD.NOT_FOUND', { count: r.notFound.length });
      }
      this.saved.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async unlock(): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.unlockReaderAccount(this.readers()[0].publicId);
      this.toastr.success('READER_PASSWORD.UNLOCKED');
      this.saved.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
