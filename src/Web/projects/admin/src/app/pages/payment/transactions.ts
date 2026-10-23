import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, PaymentSearch, PaymentSearchResult, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ReasonDialog, ReasonResult } from '../../shared/reason-dialog';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { DateInput } from '../../shared/date-input';

/**
 * Giao dịch thanh toán QR và đối soát: lọc theo trạng thái/cổng/ngày, tổng đã thu / đang chờ / cần đối soát, và xử lý các giao dịch cần can thiệp tay
 * (tiền về sau khi mã hết hạn, sai số tiền, khoản đã thu cách khác → cần hoàn tiền). "Đồng bộ lại" dựng lại khoản nợ từ phiếu phạt/sao chụp hiện có.
 */
@Component({
  selector: 'app-payment-transactions',
  imports: [DateInput, FormsModule, LDatePipe, LNumberPipe, Loading, Paginator, ReasonDialog, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'PAYMENT_TX.TITLE' | translate }}</h4>
      @if (session.can('PAYMENT_RECONCILIATION:edit')) {
        <button type="button" class="btn-secondary ml-auto" [disabled]="busy()" (click)="resync()"><span class="material-icons text-[18px]">sync</span> {{ 'PAYMENT_TX.RESYNC' | translate }}</button>
      }
    </div>

    <div class="panel grid grid-cols-1 md:grid-cols-6 gap-3 items-end">
      <div class="md:col-span-2"><label class="field-label" for="pt-kw">{{ 'PAYMENT_TX.SEARCH' | translate }}</label>
        <input id="pt-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" /></div>
      <div><label class="field-label" for="pt-st">{{ 'PAYMENT_TX.STATUS' | translate }}</label>
        <select id="pt-st" class="input" [(ngModel)]="search.transactionStatus" (change)="find()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          <option value="Pending">{{ 'PAYMENT_TX.S_Pending' | translate }}</option><option value="Paid">{{ 'PAYMENT_TX.S_Paid' | translate }}</option>
          <option value="PaidLate">{{ 'PAYMENT_TX.S_PaidLate' | translate }}</option><option value="Expired">{{ 'PAYMENT_TX.S_Expired' | translate }}</option>
          <option value="Cancelled">{{ 'PAYMENT_TX.S_Cancelled' | translate }}</option>
        </select></div>
      <div><label class="field-label" for="pt-pr">{{ 'PAYMENT_TX.PROVIDER' | translate }}</label>
        <select id="pt-pr" class="input" [(ngModel)]="search.provider" (change)="find()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option><option value="VietQR">VietQR</option><option value="VNPAY">VNPAY</option>
        </select></div>
      <div><label class="field-label" for="pt-from">{{ 'COMMON.FROM' | translate }}</label><app-date-input fieldId="pt-from" [(ngModel)]="search.from" (ngModelChange)="find()"></app-date-input></div>
      <div><label class="field-label" for="pt-to">{{ 'COMMON.TO' | translate }}</label><app-date-input fieldId="pt-to" [(ngModel)]="search.to" (ngModelChange)="find()"></app-date-input></div>
      <label class="md:col-span-6 flex items-center gap-2 text-sm"><input type="checkbox" [ngModel]="search.needsReview === true" (ngModelChange)="search.needsReview = $event ? true : null; find()" /> {{ 'PAYMENT_TX.ONLY_REVIEW' | translate }}</label>
    </div>

    @if (result(); as r) {
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4 text-sm">
        <div class="panel !mb-0"><div class="text-gray-500">{{ 'PAYMENT_TX.TOTAL_PAID' | translate }}</div><div class="text-xl font-semibold text-green-700">{{ r.totals.paid | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div></div>
        <div class="panel !mb-0"><div class="text-gray-500">{{ 'PAYMENT_TX.TOTAL_PENDING' | translate }}</div><div class="text-xl font-semibold">{{ r.totals.pending | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div></div>
        <div class="panel !mb-0"><div class="text-gray-500">{{ 'PAYMENT_TX.TOTAL_REVIEW' | translate: { count: r.totals.needsReviewCount } }}</div><div class="text-xl font-semibold text-amber-700">{{ r.totals.needsReview | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div></div>
      </div>
    }

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-52">{{ 'PAYMENT_TX.CODE' | translate }}</th><th class="th">{{ 'PAYMENT_TX.CHARGE' | translate }}</th><th class="th w-20">{{ 'PAYMENT_TX.PROVIDER' | translate }}</th>
            <th class="th w-32 !text-right">{{ 'FINE_TICKET_PAGE.AMOUNT' | translate }}</th><th class="th w-36">{{ 'PAYMENT_TX.STATUS' | translate }}</th><th class="th w-40">{{ 'PAYMENT_TX.PAID_AT' | translate }}</th>
            <th class="th w-24 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (t of result()?.page?.items ?? []; track t.publicId) {
              <tr class="align-top">
                <td class="td font-mono text-[12px]">{{ t.transactionCode }}<div class="text-gray-400">{{ t.createdAt | ldate: 'dd/MM/yyyy HH:mm' }}</div></td>
                <td class="td">{{ t.label }}<div class="text-[11px] text-gray-500 font-mono">{{ t.cardNo }}</div>
                  @if (t.needsReview) { <div class="text-[12px] text-amber-700 mt-0.5">⚠ {{ t.reviewNote }}</div> }</td>
                <td class="td text-[13px]">{{ t.provider }}</td>
                <td class="td text-right">{{ t.amount | lnumber: '1.0-0' }}</td>
                <td class="td"><span [class]="badge(t.status)">{{ statusKey(t.status) | translate }}</span></td>
                <td class="td text-[13px]">{{ t.paidAt ? (t.paidAt | ldate: 'dd/MM/yyyy HH:mm') : '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ t.providerRef ?? '' }}</div></td>
                <td class="td text-center">
                  @if (t.needsReview && session.can('PAYMENT_RECONCILIATION:edit')) {
                    <button type="button" class="btn-secondary !py-1 !px-2" (click)="resolving.set(t.publicId)">{{ 'PAYMENT_TX.RESOLVE' | translate }}</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (result(); as r) { <app-paginator [total]="r.page.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" /> }
    </div>

    @if (resolving(); as id) {
      <app-reason-dialog title="PAYMENT_TX.RESOLVE_TITLE" message="PAYMENT_TX.RESOLVE_MSG" (confirmed)="resolve(id, $event)" (cancelled)="resolving.set(null)" />
    }
  `,
})
export class PaymentTransactions implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected search: PaymentSearch = { keyword: '', transactionStatus: null, provider: null, needsReview: null, from: null, to: null, pageIndex: 1, pageSize: 20 };
  protected readonly result = signal<PaymentSearchResult | null>(null);
  protected readonly busy = signal(false);
  protected readonly resolving = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected find(): void {
    this.search.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.search.pageIndex = e.pageIndex;
    this.search.pageSize = e.pageSize;
    void this.load();
  }

  protected badge(status: string): string {
    return status === 'Paid' ? 'badge-on' : status === 'PaidLate' ? 'badge-warn' : status === 'Pending' ? 'badge-info' : 'badge-off';
  }

  protected statusKey(status: string): string {
    switch (status) {
      case 'Paid': return 'PAYMENT_TX.S_Paid';
      case 'PaidLate': return 'PAYMENT_TX.S_PaidLate';
      case 'Pending': return 'PAYMENT_TX.S_Pending';
      case 'Expired': return 'PAYMENT_TX.S_Expired';
      default: return 'PAYMENT_TX.S_Cancelled';
    }
  }

  protected async resolve(id: string, input: ReasonResult): Promise<void> {
    this.resolving.set(null);
    try {
      await this.api.resolvePaymentReview(id, input.reason);
      this.toastr.success('PAYMENT_TX.RESOLVED');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async resync(): Promise<void> {
    this.busy.set(true);
    try {
      const r = await this.api.resyncCharges();
      this.toastr.success('PAYMENT_TX.SYNCED', { count: r.synced });
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      const s = this.search;
      this.result.set(await this.api.searchPayments({ ...s, keyword: s.keyword?.trim() || undefined, from: s.from || null, to: s.to || null }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
