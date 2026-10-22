import { Component, OnDestroy, OnInit, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, PaymentTx, errorMessage } from '../../core/api';
import { LNumberPipe } from '../../core/i18n';
import { QrCode } from '../../../../../shared/qr-code';
import { Loading, Modal } from '../../shared/ui';

/**
 * Hộp thoại thu phí bằng mã QR tại quầy: chọn cổng (VietQR / VNPAY) nếu đơn vị bật nhiều cổng, sinh mã cho khoản thu của một phiếu/phiếu sao chụp/
 * phí cấp lại thẻ, đếm ngược, tự hỏi trạng thái 2 giây/lần và báo khi tiền về. Số tiền do server lấy từ nguồn — không nhập ở đây.
 */
@Component({
  selector: 'app-payment-dialog',
  imports: [Modal, Loading, QrCode, LNumberPipe, RouterLink, TranslatePipe],
  template: `
    <app-modal title="PAYMENT_DIALOG.TITLE" widthClass="max-w-md" (closed)="close()">
      <div class="relative min-h-40">
        @if (busy()) { <app-loading /> }
        @if (error()) { <div class="alert-error mb-3">{{ error() }}</div> }

        @if (providers().length === 0 && !busy() && !tx()) {
          <p class="text-sm text-gray-600">{{ 'PAYMENT_DIALOG.NO_PROVIDER' | translate }}</p>
          <a routerLink="/payment-account" class="text-teal-700 underline text-sm" (click)="close()">{{ 'PAYMENT_DIALOG.GO_SETTINGS' | translate }}</a>
        } @else if (!tx()) {
          <p class="text-sm text-gray-600 mb-3">{{ 'PAYMENT_DIALOG.PICK' | translate }}</p>
          <div class="flex gap-2">
            @for (p of providers(); track p) {
              <button type="button" class="btn-secondary flex-1 justify-center" (click)="create(p)">{{ p === 'VNPAY' ? 'VNPAY' : 'VietQR' }}</button>
            }
          </div>
        } @else {
          @let t = tx()!;
          <div class="text-center">
            <div class="text-sm text-gray-500">{{ t.label }}</div>
            <div class="text-3xl font-bold text-teal-700 my-1">{{ t.amount | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div>
            <div class="font-mono text-xs text-gray-500 mb-3">{{ t.transactionCode }}</div>
            @switch (t.status) {
              @case ('Paid') {
                <div class="alert-success !block"><span class="material-icons align-middle">check_circle</span> {{ 'PAYMENT_DIALOG.PAID' | translate }}</div>
              }
              @case ('PaidLate') {
                <div class="alert-success !block"><span class="material-icons align-middle">check_circle</span> {{ 'PAYMENT_DIALOG.PAID_LATE' | translate }}</div>
              }
              @case ('Pending') {
                <div class="inline-block border border-gray-200 rounded-xl p-2 bg-white"><elib-qr [value]="t.qrContent" cssSize="240px" [label]="t.transactionCode" /></div>
                <div class="text-sm mt-2" [class.text-red-600]="remaining() <= 60">{{ 'PAYMENT_DIALOG.EXPIRES_IN' | translate: { time: clock() } }}</div>
                <p class="text-xs text-gray-500 mt-1">{{ (t.provider === 'VNPAY' ? 'PAYMENT_DIALOG.HINT_VNPAY' : 'PAYMENT_DIALOG.HINT_VIETQR') | translate }}</p>
                @if (t.provider === 'VNPAY') {
                  <a [href]="t.qrContent" target="_blank" rel="noopener noreferrer" class="text-teal-700 underline text-sm">{{ 'PAYMENT_DIALOG.OPEN_GATEWAY' | translate }}</a>
                }
              }
              @default {
                <div class="alert-error !block">{{ 'PAYMENT_DIALOG.EXPIRED' | translate }}</div>
                <button type="button" class="btn-primary mt-3" (click)="create(t.provider)">{{ 'PAYMENT_DIALOG.NEW_CODE' | translate }}</button>
              }
            }
          </div>
        }
      </div>
      <ng-container footer><button type="button" class="btn-secondary" (click)="close()">{{ 'COMMON.CLOSE' | translate }}</button></ng-container>
    </app-modal>
  `,
})
export class PaymentDialog implements OnInit, OnDestroy {
  /** Nguồn khoản thu: circulation (phiếu phạt, sao chụp) hoặc patron (phí cấp lại thẻ). */
  readonly sourceService = input.required<string>();
  readonly sourceId = input.required<string>();
  readonly closed = output<void>();
  /** Tiền đã về — nơi dùng nạp lại phiếu / danh sách. */
  readonly paid = output<void>();

  private readonly api = inject(Api);
  protected readonly providers = signal<string[]>([]);
  protected readonly tx = signal<PaymentTx | null>(null);
  protected readonly busy = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly remaining = signal(0);
  protected readonly clock = signal('');
  private timer: ReturnType<typeof setInterval> | null = null;
  private pollTick = 0;

  async ngOnInit(): Promise<void> {
    try {
      this.providers.set(await this.api.paymentProviders());
      if (this.providers().length === 1) await this.create(this.providers()[0]);
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  ngOnDestroy(): void {
    this.stop();
  }

  protected close(): void {
    this.stop();
    this.closed.emit();
  }

  protected async create(provider: string): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.tx.set(await this.api.createPayment(this.sourceService(), this.sourceId(), provider));
      this.start();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private start(): void {
    this.stop();
    this.tick();
    this.timer = setInterval(() => this.tick(), 1000);
  }

  private stop(): void {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
  }

  private tick(): void {
    const t = this.tx();
    if (!t || t.status !== 'Pending') return;
    const left = Math.max(0, Math.floor((new Date(t.expiresAt).getTime() - Date.now()) / 1000));
    this.remaining.set(left);
    this.clock.set(`${Math.floor(left / 60)}:${String(left % 60).padStart(2, '0')}`);
    if (++this.pollTick % 2 === 0 || left === 0) void this.poll(t);
  }

  private async poll(t: PaymentTx): Promise<void> {
    try {
      const fresh = await this.api.paymentStatus(t.publicId);
      if (this.tx()?.publicId !== fresh.publicId) return;
      this.tx.set({ ...t, status: fresh.status, paidAt: fresh.paidAt, needsReview: fresh.needsReview });
      if (fresh.status !== 'Pending') {
        this.stop();
        if (fresh.status === 'Paid' || fresh.status === 'PaidLate') this.paid.emit();
      }
    } catch {
      /* mất mạng thoáng qua — vòng sau hỏi lại */
    }
  }
}
