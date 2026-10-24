import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { QrCode } from '../../../../shared/qr-code';
import { LNumberPipe, Language } from '../core/i18n';
import { MyCharge, MyPayment, PaymentApi } from '../core/payment';

/**
 * "Phí / Nợ của tôi": các khoản còn nợ (phiếu phạt, sao chụp, phí cấp lại thẻ) và nút thanh toán bằng mã QR (VietQR / VNPAY). Mã hết hạn sau ít phút;
 * trang tự hỏi trạng thái 2 giây/lần và báo khi tiền về (đối soát do thư viện, bạn đọc không cần làm gì thêm).
 */
@Component({
  selector: 'opac-my-charges',
  imports: [LNumberPipe, QrCode, TranslatePipe],
  template: `
    <section class="bg-white rounded-xl shadow-sm border border-gray-100 p-6 mt-6">
      <h2 class="font-semibold text-gray-900 mb-4">{{ 'OPAC.PAYMENT.TITLE' | translate }}</h2>
      @if (error()) { <p class="text-red-600 text-sm mb-2" role="alert">{{ error() }}</p> }
      @if (notice()) { <div class="mb-3 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2" role="status">{{ notice() }}</div> }
      @if (charges(); as list) {
        @if (list.length) {
          <ul class="divide-y divide-gray-100">
            @for (c of list; track c.publicId) {
              <li class="py-3 flex items-center justify-between gap-3 flex-wrap">
                <div class="min-w-0"><div class="font-medium text-gray-900">{{ c.label }}</div>
                  <div class="text-sm text-gray-500">{{ c.amount | lnumber: '1.0-0' }} {{ 'OPAC.PAYMENT.CURRENCY' | translate }}</div></div>
                <div class="flex gap-2">
                  @for (p of c.providers; track p) {
                    <button type="button" class="px-3 py-1.5 rounded-lg bg-main text-white text-sm hover:bg-blue-700 disabled:opacity-60" [disabled]="busy()" (click)="pay(c, p)">
                      {{ 'OPAC.PAYMENT.PAY_WITH' | translate: { provider: p } }}</button>
                  } @empty {
                    <span class="text-xs text-gray-500">{{ 'OPAC.PAYMENT.PAY_AT_COUNTER' | translate }}</span>
                  }
                </div>
              </li>
            }
          </ul>
        } @else {
          <p class="text-gray-500 text-sm">{{ 'OPAC.PAYMENT.NONE' | translate }}</p>
        }
      } @else if (!error()) {
        <div class="flex justify-center py-6"><i class="fas fa-circle-notch animate-spin text-main text-2xl"></i></div>
      }
    </section>

    @if (tx(); as t) {
      <div class="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
        <div class="bg-white rounded-2xl shadow-xl w-full max-w-sm p-6 text-center">
          <div class="text-sm text-gray-500">{{ t.label }}</div>
          <div class="text-3xl font-bold text-main my-1">{{ t.amount | lnumber: '1.0-0' }} {{ 'OPAC.PAYMENT.CURRENCY' | translate }}</div>
          <div class="font-mono text-xs text-gray-400 mb-3">{{ t.transactionCode }}</div>
          @if (t.status === 'Pending') {
            <div class="inline-block border border-gray-200 rounded-xl p-2"><elib-qr [value]="t.qrContent" cssSize="240px" [label]="t.transactionCode" /></div>
            <div class="text-sm mt-2" [class.text-red-600]="left() <= 60">{{ 'OPAC.PAYMENT.EXPIRES_IN' | translate: { time: clock() } }}</div>
            <p class="text-xs text-gray-500 mt-1">{{ (t.provider === 'VNPAY' ? 'OPAC.PAYMENT.HINT_VNPAY' : 'OPAC.PAYMENT.HINT_VIETQR') | translate }}</p>
            @if (t.provider === 'VNPAY') { <a [href]="t.qrContent" target="_blank" rel="noopener noreferrer" class="text-main underline text-sm">{{ 'OPAC.PAYMENT.OPEN_GATEWAY' | translate }}</a> }
          } @else {
            <p class="text-red-600 text-sm">{{ 'OPAC.PAYMENT.EXPIRED' | translate }}</p>
          }
          <button type="button" class="mt-4 px-4 py-2 rounded-lg border border-gray-300 text-gray-700 text-sm hover:bg-gray-50" (click)="close()">{{ 'OPAC.PAYMENT.CLOSE' | translate }}</button>
        </div>
      </div>
    }
  `,
})
export class MyCharges implements OnInit, OnDestroy {
  private readonly api = inject(PaymentApi);
  private readonly language = inject(Language);

  protected readonly charges = signal<MyCharge[] | null>(null);
  protected readonly tx = signal<MyPayment | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly left = signal(0);
  protected readonly clock = signal('');
  private timer: ReturnType<typeof setInterval> | null = null;
  private ticks = 0;

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    this.stop();
  }

  protected async pay(charge: MyCharge, provider: string): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.tx.set(await this.api.create(charge.publicId, provider));
      this.start();
    } catch (e) {
      this.error.set(this.language.apiError(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected close(): void {
    this.stop();
    this.tx.set(null);
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.charges.set(await this.api.charges());
    } catch (e) {
      this.error.set(this.language.apiError(e));
    }
  }

  private start(): void {
    this.stop();
    this.ticks = 0;
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
    this.left.set(left);
    this.clock.set(`${Math.floor(left / 60)}:${String(left % 60).padStart(2, '0')}`);
    if (++this.ticks % 2 === 0 || left === 0) void this.poll(t);
  }

  private async poll(t: MyPayment): Promise<void> {
    try {
      const fresh = await this.api.status(t.publicId);
      if (this.tx()?.publicId !== fresh.publicId) return;
      if (fresh.status === 'Paid' || fresh.status === 'PaidLate') {
        this.stop();
        this.tx.set(null);
        this.notice.set(this.language.t('OPAC.PAYMENT.PAID'));
        await this.load();
      } else if (fresh.status !== 'Pending') {
        this.stop();
        this.tx.set({ ...t, status: fresh.status });
      }
    } catch {
      /* mất mạng thoáng qua — vòng sau hỏi lại */
    }
  }
}
