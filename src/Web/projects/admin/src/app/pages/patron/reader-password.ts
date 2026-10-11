import { DatePipe } from '@angular/common';
import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, Reader, ReaderAccount, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';

/** Nhãn tình trạng đăng nhập OPAC trên danh sách bạn đọc. */
export function accountLabel(a: ReaderAccount | undefined): { text: string; css: string } {
  if (!a) return { text: '—', css: 'text-gray-400' };
  if (a.lockedOut) return { text: 'Tạm khoá đăng nhập', css: 'bg-red-50 text-red-700' };
  if (!a.hasPassword) return { text: 'Chưa có mật khẩu', css: 'bg-gray-100 text-gray-600' };
  if (a.cardNoPassword) return { text: 'Mật khẩu = số thẻ', css: 'bg-amber-50 text-amber-700' };
  return { text: 'Đã đặt mật khẩu', css: 'bg-green-50 text-green-700' };
}

/**
 * Mật khẩu đăng nhập OPAC (monolith: Reader/ResetPassword, BulkResetPassword). Mật khẩu nằm ở identity, không đi cùng hồ sơ bạn đọc.
 * Một bạn đọc (account có giá trị) hoặc nhiều bạn đọc đã chọn; bạn đọc luôn phải đổi mật khẩu ở lần đăng nhập đầu.
 */
@Component({
  selector: 'app-reader-password',
  imports: [FormsModule, DatePipe, Modal],
  template: `
    <app-modal [title]="bulk() ? 'Đặt mật khẩu OPAC cho ' + readers().length + ' bạn đọc' : 'Mật khẩu OPAC — ' + readers()[0].cardNo" (closed)="closed.emit()">
      @if (!bulk()) {
        <div class="text-sm text-gray-600 mb-4 space-y-1">
          <div><b>{{ readers()[0].fullName }}</b> — thẻ <span class="font-mono">{{ readers()[0].cardNo }}</span></div>
          @if (account(); as a) {
            <div>Hiện tại: {{ label(a).text }}@if (a.hasPassword && a.mustChangePassword) { <span>, chưa đổi lần đầu</span> }</div>
            <div>Đăng nhập gần nhất: {{ a.lastLoginAt ? (a.lastLoginAt | date: 'dd/MM/yyyy HH:mm') : 'chưa đăng nhập' }}</div>
            @if (a.lockedOut) {
              <div class="flex items-center gap-2 text-red-700">Đăng nhập sai nhiều lần, đang bị khoá tạm.
                <button type="button" class="btn-secondary !py-1 !px-2" [disabled]="busy()" (click)="unlock()">Mở khoá</button></div>
            }
          }
        </div>
      }
      <fieldset class="space-y-3">
        <legend class="form-label">Mật khẩu mới</legend>
        <label class="flex items-start gap-2 text-sm cursor-pointer">
          <input type="radio" name="pw-mode" class="mt-1" [checked]="useCardNo()" (change)="useCardNo.set(true)" />
          <span>Dùng <b>số thẻ</b> làm mật khẩu<span class="block text-xs text-gray-500">Mỗi bạn đọc đăng nhập bằng số thẻ của mình (không phân biệt hoa/thường).</span></span>
        </label>
        <label class="flex items-start gap-2 text-sm cursor-pointer">
          <input type="radio" name="pw-mode" class="mt-1" [checked]="!useCardNo()" (change)="useCardNo.set(false)" />
          <span>Đặt mật khẩu{{ bulk() ? ' chung' : '' }}</span>
        </label>
        @if (!useCardNo()) {
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pl-6">
            <div><label class="form-label" for="pw-new">Mật khẩu</label>
              <input id="pw-new" type="password" class="input" autocomplete="new-password" [(ngModel)]="password" /></div>
            <div><label class="form-label" for="pw-confirm">Nhập lại</label>
              <input id="pw-confirm" type="password" class="input" autocomplete="new-password" [(ngModel)]="confirm" /></div>
          </div>
        }
      </fieldset>
      <p class="text-xs text-gray-500 mt-4">Mật khẩu từ 6 ký tự. Bạn đọc phải đổi mật khẩu ở lần đăng nhập OPAC đầu tiên.</p>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">Huỷ</button>
        <button type="button" class="btn-primary" [disabled]="busy()" (click)="save()"><span class="material-icons text-[18px]">key</span>Lưu mật khẩu</button>
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
        this.toastr.warning('Mật khẩu tối thiểu 6 ký tự.');
        return;
      }
      if (this.password !== this.confirm) {
        this.toastr.warning('Hai lần nhập mật khẩu không khớp.');
        return;
      }
    }
    const password = useCardNo ? null : this.password;
    const readers = this.readers();
    this.busy.set(true);
    try {
      if (!this.bulk()) {
        await this.api.setReaderPassword(readers[0].publicId, password, useCardNo);
        this.toastr.success('Đã đặt mật khẩu OPAC.');
      } else {
        const r = await this.api.bulkSetReaderPassword(readers.map((x) => x.publicId), password, useCardNo);
        this.toastr.success(`Đã đặt mật khẩu OPAC cho ${r.updatedCount} bạn đọc.`);
        if (r.notFound.length) this.toastr.warning(`${r.notFound.length} bạn đọc không còn trong hệ thống — đã bỏ qua.`);
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
      this.toastr.success('Đã mở khoá đăng nhập.');
      this.saved.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
