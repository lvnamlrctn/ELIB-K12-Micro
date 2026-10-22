import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CardFee, CrudPage, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { PaymentDialog } from '../payment/payment-dialog';
import { ReasonDialog, ReasonResult } from '../../shared/reason-dialog';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

/**
 * Phí cấp lại thẻ (monolith: tham số PHI_CAP_LAI_THE + màn bạn đọc): lập khoản phí cho bạn đọc (số tiền + lý do), thu tiền mặt tại quầy hoặc bằng mã QR
 * (cần phân hệ thanh toán), huỷ khoản lập nhầm. Mỗi bạn đọc chỉ có một khoản chưa thu tại một thời điểm.
 */
@Component({
  selector: 'app-card-fees',
  imports: [FormsModule, LDatePipe, LNumberPipe, Loading, Paginator, ReasonDialog, PaymentDialog, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'CARD_FEES.TITLE' | translate }}</h4></div>

    @if (session.can('READER_CARD_FEES:add')) {
      <div class="panel grid grid-cols-1 md:grid-cols-5 gap-3 items-end">
        <div><label class="field-label" for="cf-card">{{ 'CARD_FEES.CARD_NO' | translate }}</label><input id="cf-card" class="input font-mono" [(ngModel)]="card" /></div>
        <div><label class="field-label" for="cf-amount">{{ 'CARD_FEES.AMOUNT' | translate }}</label><input id="cf-amount" type="number" min="1000" class="input" [(ngModel)]="amount" /></div>
        <div class="md:col-span-2"><label class="field-label" for="cf-reason">{{ 'CARD_FEES.REASON' | translate }}</label><input id="cf-reason" class="input" maxlength="500" [(ngModel)]="reason" /></div>
        <button type="button" class="btn-primary justify-center" [disabled]="busy() || !card.trim() || !(amount > 0) || !reason.trim()" (click)="issue()">
          <span class="material-icons text-[18px]">add_card</span> {{ 'CARD_FEES.ISSUE' | translate }}</button>
      </div>
    }

    <div class="panel flex gap-3 items-end flex-wrap">
      <div><label class="field-label" for="cf-kw">{{ 'COMMON.SEARCH' | translate }}</label><input id="cf-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="find()" /></div>
      <div><label class="field-label" for="cf-st">{{ 'CARD_FEES.STATUS' | translate }}</label>
        <select id="cf-st" class="input" [(ngModel)]="status" (change)="find()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option><option value="Open">{{ 'CARD_FEES.S_Open' | translate }}</option>
          <option value="Paid">{{ 'CARD_FEES.S_Paid' | translate }}</option><option value="Cancelled">{{ 'CARD_FEES.S_Cancelled' | translate }}</option>
        </select></div>
      <button type="button" class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[860px]">
          <thead><tr>
            <th class="th w-28">{{ 'CARD_FEES.CARD_NO' | translate }}</th><th class="th">{{ 'CARD_FEES.READER' | translate }}</th><th class="th w-32 !text-right">{{ 'CARD_FEES.AMOUNT' | translate }}</th>
            <th class="th">{{ 'CARD_FEES.REASON' | translate }}</th><th class="th w-36">{{ 'CARD_FEES.STATUS' | translate }}</th><th class="th w-56 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (f of page()?.items ?? []; track f.publicId) {
              <tr class="align-top">
                <td class="td font-mono text-[13px]">{{ f.cardNo }}</td>
                <td class="td">{{ f.readerName }}<div class="text-[11px] text-gray-400">{{ f.createdAt | ldate: 'dd/MM/yyyy HH:mm' }} · {{ f.createdByName ?? '' }}</div></td>
                <td class="td text-right">{{ f.amount | lnumber: '1.0-0' }}</td>
                <td class="td text-[13px] text-gray-600">{{ f.reason }}</td>
                <td class="td"><span [class]="f.status === 'Paid' ? 'badge-on' : f.status === 'Open' ? 'badge-info' : 'badge-off'">{{ statusKey(f.status) | translate }}</span>
                  @if (f.paidVia) { <div class="text-[11px] text-gray-500">{{ f.paidVia === 'QR' ? ('CARD_FEES.VIA_QR' | translate) : ('CARD_FEES.VIA_CASH' | translate) }}</div> }</td>
                <td class="td text-center whitespace-nowrap">
                  @if (f.status === 'Open') {
                    @if (canQr()) { <button type="button" class="btn-primary !py-1 !px-2 mr-1" (click)="paying.set(f)"><span class="material-icons text-[16px]">qr_code_2</span> QR</button> }
                    @if (session.can('READER_CARD_FEES:edit')) {
                      <button type="button" class="btn-secondary !py-1 !px-2 mr-1" (click)="cash(f)">{{ 'CARD_FEES.CASH' | translate }}</button>
                      <button type="button" class="btn-secondary !py-1 !px-2 text-red-700" (click)="cancelling.set(f)">{{ 'CARD_FEES.CANCEL' | translate }}</button>
                    }
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" /> }
    </div>

    @if (paying(); as f) {
      <app-payment-dialog sourceService="patron" [sourceId]="f.publicId" (closed)="paying.set(null)" (paid)="load()" />
    }
    @if (cancelling(); as f) {
      <app-reason-dialog title="CARD_FEES.CANCEL_TITLE" message="CARD_FEES.CANCEL_MSG" [params]="{ card: f.cardNo }" (confirmed)="cancel(f, $event)" (cancelled)="cancelling.set(null)" />
    }
  `,
})
export class CardFees implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);
  protected readonly canQr = computed(() => this.session.can('PAYMENT_TRANSACTIONS:add') && (this.session.features()?.modules ?? []).includes('PAYMENT'));

  protected card = '';
  protected amount = 0;
  protected reason = '';
  protected keyword = '';
  protected status: string | null = null;
  protected pageIndex = 1;
  protected pageSize = 20;
  protected readonly page = signal<CrudPage<CardFee> | null>(null);
  protected readonly busy = signal(false);
  protected readonly paying = signal<CardFee | null>(null);
  protected readonly cancelling = signal<CardFee | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected statusKey(status: string): string {
    return status === 'Paid' ? 'CARD_FEES.S_Paid' : status === 'Open' ? 'CARD_FEES.S_Open' : 'CARD_FEES.S_Cancelled';
  }

  protected find(): void {
    this.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load();
  }

  protected async issue(): Promise<void> {
    await this.act(async () => {
      await this.api.addCardFee({ cardNo: this.card.trim(), amount: Number(this.amount), reason: this.reason.trim() });
      this.card = '';
      this.reason = '';
      this.toastr.success('CARD_FEES.ISSUED');
    });
  }

  protected async cash(f: CardFee): Promise<void> {
    await this.act(async () => {
      await this.api.settleCardFeeCash(f.publicId);
      this.toastr.success('CARD_FEES.CASHED');
    });
  }

  protected async cancel(f: CardFee, input: ReasonResult): Promise<void> {
    this.cancelling.set(null);
    await this.act(async () => {
      await this.api.cancelCardFee(f.publicId, input.reason);
      this.toastr.success('CARD_FEES.CANCELLED');
    });
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.page.set(await this.api.searchCardFees({ keyword: this.keyword.trim() || undefined, feeStatus: this.status, pageIndex: this.pageIndex, pageSize: this.pageSize }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async act(run: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await run();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
    await this.load();
  }
}
