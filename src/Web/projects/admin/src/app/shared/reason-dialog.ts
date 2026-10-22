import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Modal } from './ui';

export interface ReasonResult { reason: string; amount: number | null; }

/**
 * Hộp thoại nhập lý do cho thao tác quan trọng (giảm trừ, sửa/xoá dòng phạt, mở lại, điều chỉnh, từ chối duyệt…). Lý do 1–500 ký tự,
 * server vẫn kiểm lại. Tuỳ chọn thêm ô số tiền (điều chỉnh). `preview` hiện các dòng "trước → sau" để người thao tác xác nhận.
 */
@Component({
  selector: 'app-reason-dialog',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal [title]="title()" widthClass="max-w-lg" (closed)="cancelled.emit()">
      @if (message()) { <p class="text-sm text-gray-600 mb-3">{{ message()! | translate: params() }}</p> }
      @if (preview().length) {
        <ul class="mb-3 text-sm bg-gray-50 border border-gray-100 rounded-lg divide-y divide-gray-100">
          @for (p of preview(); track p.label) {
            <li class="px-3 py-1.5 flex justify-between gap-3"><span class="text-gray-500">{{ p.label | translate }}</span>
              <span><span class="text-red-700 line-through">{{ p.from }}</span> → <span class="text-green-700 font-medium">{{ p.to }}</span></span></li>
          }
        </ul>
      }
      @if (showAmount()) {
        <div class="mb-3">
          <label class="field-label" for="rd-amount">{{ amountLabel() | translate }}</label>
          <input id="rd-amount" type="number" min="0" class="input" [(ngModel)]="amount" />
        </div>
      }
      <label class="field-label" for="rd-reason">{{ 'REASON_DIALOG.REASON' | translate }} @if (required()) { <span class="text-red-600">*</span> }</label>
      <textarea id="rd-reason" rows="3" maxlength="500" class="input" [(ngModel)]="reason" [placeholder]="'REASON_DIALOG.PLACEHOLDER' | translate"></textarea>
      @if (error()) { <p class="text-xs text-red-600 mt-1">{{ error()! | translate }}</p> }
      <p class="text-xs text-gray-400 mt-2">{{ 'REASON_DIALOG.HINT' | translate }}</p>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="cancelled.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="button" class="btn-primary" (click)="submit()">{{ confirmText() | translate }}</button>
      </ng-container>
    </app-modal>
  `,
})
export class ReasonDialog {
  readonly title = input.required<string>();
  readonly message = input<string | null>(null);
  readonly params = input<Record<string, unknown>>({});
  readonly required = input(true);
  readonly confirmText = input('COMMON.CONFIRM');
  readonly showAmount = input(false);
  readonly amountLabel = input('REASON_DIALOG.AMOUNT');
  readonly preview = input<{ label: string; from: string; to: string }[]>([]);
  readonly confirmed = output<ReasonResult>();
  readonly cancelled = output<void>();

  protected reason = '';
  protected amount: number | null = null;
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    const reason = this.reason.trim();
    if (this.required() && !reason) { this.error.set('REASON_DIALOG.REQUIRED'); return; }
    if (this.showAmount() && !(Number(this.amount) > 0)) { this.error.set('REASON_DIALOG.AMOUNT_REQUIRED'); return; }
    this.confirmed.emit({ reason, amount: this.showAmount() ? Number(this.amount) : null });
  }
}
