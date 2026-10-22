import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, PaymentAccount, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading } from '../../shared/ui';

/**
 * Tài khoản nhận tiền của đơn vị: VietQR (ngân hàng + khoá Sepay xác nhận tiền về) và VNPAY (mã website + HashSecret). Bí mật chỉ nhập vào,
 * không bao giờ hiện lại — để trống khi lưu là giữ nguyên. Trang cũng hiện đúng địa chỉ webhook của host này để dán vào bảng điều khiển Sepay/VNPAY.
 */
@Component({
  selector: 'app-payment-account',
  imports: [FormsModule, Loading, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'PAYMENT_ACCOUNT.TITLE' | translate }}</h4>
      <p class="text-sm text-gray-500 mt-1">{{ 'PAYMENT_ACCOUNT.HINT' | translate }}</p></div>
    <div class="relative">
      @if (busy()) { <app-loading /> }
      @if (account(); as a) {
        <div class="grid grid-cols-1 xl:grid-cols-2 gap-5">
          <div class="panel !mb-0">
            <label class="flex items-center gap-2 font-semibold mb-3"><input type="checkbox" [(ngModel)]="f.vietQrEnabled" [disabled]="!canEdit()" /> {{ 'PAYMENT_ACCOUNT.VIETQR' | translate }}</label>
            <div class="grid grid-cols-2 gap-3">
              <div><label class="field-label" for="pa-bin">{{ 'PAYMENT_ACCOUNT.BANK_BIN' | translate }}</label><input id="pa-bin" class="input font-mono" maxlength="6" [(ngModel)]="f.bankBin" [disabled]="!canEdit()" /></div>
              <div><label class="field-label" for="pa-no">{{ 'PAYMENT_ACCOUNT.ACCOUNT_NO' | translate }}</label><input id="pa-no" class="input font-mono" maxlength="19" [(ngModel)]="f.accountNo" [disabled]="!canEdit()" /></div>
              <div class="col-span-2"><label class="field-label" for="pa-name">{{ 'PAYMENT_ACCOUNT.ACCOUNT_NAME' | translate }}</label><input id="pa-name" class="input" maxlength="100" [(ngModel)]="f.accountName" [disabled]="!canEdit()" /></div>
              <div class="col-span-2">
                <label class="field-label" for="pa-sepay">{{ 'PAYMENT_ACCOUNT.SEPAY_KEY' | translate }} @if (a.hasSepayApiKey) { <span class="badge-on ml-1">{{ 'PAYMENT_ACCOUNT.HAS_SECRET' | translate }}</span> }</label>
                <input id="pa-sepay" type="password" autocomplete="new-password" class="input font-mono" [(ngModel)]="sepay" [placeholder]="(a.hasSepayApiKey ? 'PAYMENT_ACCOUNT.KEEP' : 'PAYMENT_ACCOUNT.ENTER') | translate" [disabled]="!canEdit()" />
              </div>
            </div>
            <div class="mt-3 text-xs bg-gray-50 border border-gray-100 rounded-lg p-3">
              <div class="font-medium">{{ 'PAYMENT_ACCOUNT.SEPAY_URL' | translate }}</div>
              <div class="flex items-center gap-2 mt-1"><code class="break-all">{{ origin }}/hooks/sepay</code>
                <button type="button" class="icon-btn" (click)="copy(origin + '/hooks/sepay')" [title]="'PAYMENT_ACCOUNT.COPY' | translate"><span class="material-icons text-[16px]">content_copy</span></button></div>
              <div class="text-gray-500 mt-1">{{ 'PAYMENT_ACCOUNT.SEPAY_HELP' | translate }}</div>
            </div>
          </div>

          <div class="panel !mb-0">
            <label class="flex items-center gap-2 font-semibold mb-3"><input type="checkbox" [(ngModel)]="f.vnPayEnabled" [disabled]="!canEdit()" /> VNPAY</label>
            <div class="grid grid-cols-2 gap-3">
              <div><label class="field-label" for="pa-tmn">{{ 'PAYMENT_ACCOUNT.TMN' | translate }}</label><input id="pa-tmn" class="input font-mono" maxlength="20" [(ngModel)]="f.vnPayTmnCode" [disabled]="!canEdit()" /></div>
              <div>
                <label class="field-label" for="pa-hash">HashSecret @if (a.hasVnPayHashSecret) { <span class="badge-on ml-1">{{ 'PAYMENT_ACCOUNT.HAS_SECRET' | translate }}</span> }</label>
                <input id="pa-hash" type="password" autocomplete="new-password" class="input font-mono" [(ngModel)]="vnpay" [placeholder]="(a.hasVnPayHashSecret ? 'PAYMENT_ACCOUNT.KEEP' : 'PAYMENT_ACCOUNT.ENTER') | translate" [disabled]="!canEdit()" />
              </div>
              <div class="col-span-2"><label class="field-label" for="pa-url">{{ 'PAYMENT_ACCOUNT.VNPAY_URL' | translate }}</label><input id="pa-url" class="input font-mono" maxlength="300" [(ngModel)]="f.vnPayPaymentUrl" [disabled]="!canEdit()" /></div>
            </div>
            <div class="mt-3 text-xs bg-gray-50 border border-gray-100 rounded-lg p-3">
              <div class="font-medium">{{ 'PAYMENT_ACCOUNT.IPN_URL' | translate }}</div>
              <div class="flex items-center gap-2 mt-1"><code class="break-all">{{ origin }}/hooks/vnpay/ipn</code>
                <button type="button" class="icon-btn" (click)="copy(origin + '/hooks/vnpay/ipn')" [title]="'PAYMENT_ACCOUNT.COPY' | translate"><span class="material-icons text-[16px]">content_copy</span></button></div>
              <div class="text-gray-500 mt-1">{{ 'PAYMENT_ACCOUNT.IPN_HELP' | translate }}</div>
            </div>
          </div>
        </div>

        <div class="panel mt-5 flex items-end gap-4 flex-wrap">
          <div><label class="field-label" for="pa-exp">{{ 'PAYMENT_ACCOUNT.EXPIRE' | translate }}</label><input id="pa-exp" type="number" min="5" max="60" class="input w-32" [(ngModel)]="f.expireMinutes" [disabled]="!canEdit()" /></div>
          @if (canEdit()) {
            <button type="button" class="btn-primary ml-auto" [disabled]="busy()" (click)="save()"><span class="material-icons text-[18px]">save</span> {{ 'COMMON.SAVE' | translate }}</button>
          }
        </div>
      }
    </div>
  `,
})
export class PaymentAccountPage implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected readonly account = signal<PaymentAccount | null>(null);
  protected readonly busy = signal(false);
  protected readonly origin = window.location.origin;
  protected f = { vietQrEnabled: false, bankBin: '', accountNo: '', accountName: '', vnPayEnabled: false, vnPayTmnCode: '', vnPayPaymentUrl: '', expireMinutes: 15 };
  protected sepay = '';
  protected vnpay = '';

  async ngOnInit(): Promise<void> {
    this.busy.set(true);
    try {
      this.apply(await this.api.paymentAccount());
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected canEdit(): boolean {
    return this.session.can('PAYMENT_ACCOUNTS:edit');
  }

  protected async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.toastr.success('PAYMENT_ACCOUNT.COPIED');
    } catch {
      /* trình duyệt chặn clipboard — người dùng tự chép từ ô hiển thị */
    }
  }

  protected async save(): Promise<void> {
    this.busy.set(true);
    try {
      const f = this.f;
      this.apply(await this.api.savePaymentAccount({
        vietQrEnabled: f.vietQrEnabled, bankBin: f.bankBin.trim() || null, accountNo: f.accountNo.trim() || null, accountName: f.accountName.trim() || null,
        sepayApiKey: this.sepay.trim() || null, clearSepayApiKey: false,
        vnPayEnabled: f.vnPayEnabled, vnPayTmnCode: f.vnPayTmnCode.trim() || null, vnPayHashSecret: this.vnpay.trim() || null, clearVnPayHashSecret: false,
        vnPayPaymentUrl: f.vnPayPaymentUrl.trim() || null, expireMinutes: Number(f.expireMinutes) || 15,
      }));
      this.toastr.success('PAYMENT_ACCOUNT.SAVED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private apply(a: PaymentAccount): void {
    this.account.set(a);
    this.f = {
      vietQrEnabled: a.vietQrEnabled, bankBin: a.bankBin ?? '', accountNo: a.accountNo ?? '', accountName: a.accountName ?? '', vnPayEnabled: a.vnPayEnabled,
      vnPayTmnCode: a.vnPayTmnCode ?? '', vnPayPaymentUrl: a.vnPayPaymentUrl, expireMinutes: a.expireMinutes,
    };
    this.sepay = '';
    this.vnpay = '';
  }
}
