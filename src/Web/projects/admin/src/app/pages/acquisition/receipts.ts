import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  AcqBib, AcquisitionOptions, Api, CrudPage, Currency, Receipt, ReceiptItem, ReceiptLine, ReceivableLine, RegisteredItem, StoreOption, errorMessage,
} from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';
import { itemStatus } from '../holdings/items';
import { LABEL_BARCODES_KEY } from '../holdings/labels';
import { BibSource } from './bib-source';
import { PAYMENT_METHODS, loadCurrencies, today } from './orders';

const EMPTY_OPTIONS: AcquisitionOptions = { sources: [], suppliers: [], budgets: [], funds: [] };

interface ReceiptForm {
  publicId: string | null; name: string; receivedOn: string; sourceId: number | null; supplierId: number | null; budgetId: number | null;
  fundId: number | null; storeId: number | null; paymentStatus: number; paymentMethod: number; note: string;
}

/** Trạng thái thanh toán: 0 chưa thanh toán, 2 đã thanh toán. */
const PAYMENT_STATUSES: { code: number; name: string }[] = [
  { code: 0, name: 'ACQ.PS_UNPAID' },
  { code: 2, name: 'ACQ.PS_PAID' },
];

function receiptStatus(r: Receipt): { label: string; badge: string } {
  return r.completedAt ? { label: 'ACQ_RECEIPT.COMPLETED', badge: 'badge-on' } : { label: 'ACQ_RECEIPT.DRAFT', badge: 'badge-info' };
}

/** Phiếu nhận / đơn nhận (monolith: /admin/ab-receipts, quyền AB_RECEIPTS): danh sách, thêm/sửa/xoá phiếu chưa hoàn tất. */
@Component({
  selector: 'app-acq-receipts',
  imports: [FormsModule, RouterLink, LDatePipe, LNumberPipe, Loading, Paginator, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5 flex justify-between items-center">
      <h4 class="page-title">{{ 'ACQ_RECEIPT.TITLE' | translate }}</h4>
      @if (session.can('AB_RECEIPTS:add')) {
        <button class="btn-primary" (click)="edit(null)"><span class="material-icons text-[18px]">add</span> {{ 'ACQ_RECEIPT.ADD' | translate }}</button>
      }
    </div>
    <div class="panel flex flex-wrap gap-3 items-end">
      <div class="flex-1 min-w-[220px]">
        <label for="ar-kw" class="field-label">{{ 'ACQ_RECEIPT.SEARCH_LABEL' | translate }}</label>
        <input id="ar-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="find()" />
      </div>
      <div class="w-44">
        <label for="ar-st" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
        <select id="ar-st" class="input" [(ngModel)]="completed" (change)="find()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          <option [ngValue]="false">{{ 'ACQ_RECEIPT.DRAFT' | translate }}</option><option [ngValue]="true">{{ 'ACQ_RECEIPT.COMPLETED' | translate }}</option>
        </select>
      </div>
      <label class="flex items-center gap-2 pb-2 text-sm text-gray-700"><input type="checkbox" [(ngModel)]="mine" (change)="find()" /> {{ 'ACQ_RECEIPT.MINE' | translate }}</label>
      <div class="w-48">
        <label for="ar-fund" class="field-label">{{ 'ACQ.FUND' | translate }}</label>
        <select id="ar-fund" class="input" [(ngModel)]="fundId" (change)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (o of options().funds; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select>
      </div>
      <div><label for="ar-cf" class="field-label">{{ 'ACQ_RECEIPT.CREATED_FROM' | translate }}</label><input id="ar-cf" type="date" class="input" [(ngModel)]="createdFrom" /></div>
      <div><label for="ar-ct" class="field-label">{{ 'ACQ_RECEIPT.CREATED_TO' | translate }}</label><input id="ar-ct" type="date" class="input" [(ngModel)]="createdTo" /></div>
      <div><label for="ar-f" class="field-label">{{ 'ACQ_RECEIPT.RECEIVED_FROM' | translate }}</label><input id="ar-f" type="date" class="input" [(ngModel)]="from" /></div>
      <div><label for="ar-t" class="field-label">{{ 'COMMON.TO' | translate }}</label><input id="ar-t" type="date" class="input" [(ngModel)]="to" /></div>
      <button class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
    </div>
    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-20">{{ 'ACQ.NO' | translate }}</th><th class="th w-28">{{ 'ACQ_RECEIPT.RECEIVED_ON' | translate }}</th>
            <th class="th">{{ 'ACQ_RECEIPT.NAME' | translate }}</th><th class="th w-48">{{ 'ACQ.SUPPLIER' | translate }}</th>
            <th class="th w-28 !text-right">{{ 'ACQ.QUANTITY' | translate }}</th><th class="th w-36 !text-right">{{ 'ACQ.TOTAL' | translate }}</th>
            <th class="th w-32 !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (r of page()?.items ?? []; track r.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td"><a [routerLink]="['/ab-receipts', r.publicId]" class="font-mono font-medium text-blue-700 hover:underline">#{{ r.id }}</a></td>
                <td class="td">{{ r.receivedOn | ldate: 'dd/MM/yyyy' }}</td>
                <td class="td">{{ r.name ?? '' }}<div class="text-[11px] text-gray-400">{{ r.sourceName ?? '' }}</div></td>
                <td class="td">{{ r.supplierName ?? '—' }}</td>
                <td class="td text-right">{{ r.quantity | lnumber }}<div class="text-[11px] text-gray-400">{{ 'ACQ.LINES_N' | translate: { count: r.lineCount } }}</div></td>
                <td class="td text-right">{{ r.total | lnumber: '1.0-0' }}</td>
                <td class="td text-center"><span [class]="statusOf(r).badge">{{ statusOf(r).label | translate }}</span>
                  <div class="mt-1"><span [class]="r.paymentStatus === 2 ? 'badge-on' : 'badge-off'">{{ (r.paymentStatus === 2 ? 'ACQ.PS_PAID' : 'ACQ.PS_UNPAID') | translate }}</span></div></td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    @if (!r.completedAt && session.can('AB_RECEIPTS:edit')) {
                      <button class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate" (click)="edit(r)"><span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (!r.completedAt && session.can('AB_RECEIPTS:delete')) {
                      <button class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(r)"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" /> }
    </div>

    @if (form(); as f) {
      <app-modal [title]="(f.publicId ? 'COMMON.MODAL_EDIT_TITLE' : 'ACQ_RECEIPT.ADD') | translate" widthClass="max-w-2xl" (closed)="form.set(null)">
        <div class="grid grid-cols-2 gap-4">
          <div><label for="rf-on" class="form-label">{{ 'ACQ_RECEIPT.RECEIVED_ON' | translate }} *</label><input id="rf-on" type="date" class="input" [(ngModel)]="f.receivedOn" /></div>
          <div><label for="rf-name" class="form-label">{{ 'ACQ_RECEIPT.NAME' | translate }}</label><input id="rf-name" class="input" maxlength="250" [(ngModel)]="f.name" /></div>
          <div><label for="rf-src" class="form-label">{{ 'ACQ.SOURCE' | translate }}</label>
            <select id="rf-src" class="input" [(ngModel)]="f.sourceId"><option [ngValue]="null">—</option>@for (o of options().sources; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label for="rf-sup" class="form-label">{{ 'ACQ.SUPPLIER' | translate }}</label>
            <select id="rf-sup" class="input" [(ngModel)]="f.supplierId"><option [ngValue]="null">—</option>@for (o of options().suppliers; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label for="rf-bud" class="form-label">{{ 'ACQ.BUDGET' | translate }}</label>
            <select id="rf-bud" class="input" [(ngModel)]="f.budgetId"><option [ngValue]="null">—</option>@for (o of options().budgets; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label for="rf-fund" class="form-label">{{ 'ACQ.FUND' | translate }}</label>
            <select id="rf-fund" class="input" [(ngModel)]="f.fundId"><option [ngValue]="null">—</option>@for (o of options().funds; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label for="rf-store" class="form-label">{{ 'ACQ_RECEIPT.STORE' | translate }}</label>
            <select id="rf-store" class="input" [(ngModel)]="f.storeId"><option [ngValue]="null">—</option>@for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
          <div><label for="rf-ps" class="form-label">{{ 'ACQ.PAYMENT_STATUS' | translate }}</label>
            <select id="rf-ps" class="input" [(ngModel)]="f.paymentStatus">@for (s of paymentStatuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }</select></div>
          <div><label for="rf-pm" class="form-label">{{ 'ACQ.PAYMENT_METHOD' | translate }}</label>
            <select id="rf-pm" class="input" [(ngModel)]="f.paymentMethod">@for (m of paymentMethods; track m.code) { <option [ngValue]="m.code">{{ m.name | translate }}</option> }</select></div>
          <div class="col-span-2"><label for="rf-note" class="form-label">{{ 'COMMON.NOTE' | translate }}</label><input id="rf-note" class="input" maxlength="500" [(ngModel)]="f.note" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="save(f)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as r) {
      <app-confirm-delete [busy]="busy()" message="ACQ_RECEIPT.DELETE_CONFIRM" [messageParams]="{ no: r.id }" (confirmed)="remove(r)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Receipts implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);
  private readonly client = this.api.crud<Receipt>('receipts', 'acquisition');

  protected readonly paymentStatuses = PAYMENT_STATUSES;
  protected readonly paymentMethods = PAYMENT_METHODS;
  protected keyword = '';
  protected completed: boolean | null = null;
  protected fundId: number | null = null;
  protected mine = false;
  protected createdFrom = '';
  protected createdTo = '';
  protected from = '';
  protected to = '';
  protected pageIndex = 1;
  protected pageSize = 10;
  protected readonly page = signal<CrudPage<Receipt> | null>(null);
  protected readonly options = signal<AcquisitionOptions>(EMPTY_OPTIONS);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly form = signal<ReceiptForm | null>(null);
  protected readonly deleting = signal<Receipt | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.acquisitionOptions().then((o) => this.options.set(o), () => undefined);
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected statusOf(r: Receipt) {
    return receiptStatus(r);
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

  protected edit(r: Receipt | null): void {
    this.form.set({
      publicId: r?.publicId ?? null, name: r?.name ?? '', receivedOn: r?.receivedOn ?? today(), sourceId: r?.sourceId ?? null,
      supplierId: r?.supplierId ?? null, budgetId: r?.budgetId ?? null, fundId: r?.fundId ?? null, storeId: r?.storeId ?? null,
      paymentStatus: r?.paymentStatus ?? 0, paymentMethod: r?.paymentMethod ?? 0, note: r?.note ?? '',
    });
  }

  protected async save(f: ReceiptForm): Promise<void> {
    if (!f.receivedOn) {
      this.toastr.warning('ACQ_RECEIPT.RECEIVED_ON_REQUIRED');
      return;
    }
    const body = {
      receivedOn: f.receivedOn, name: f.name.trim() || null, sourceId: f.sourceId, supplierId: f.supplierId, budgetId: f.budgetId, fundId: f.fundId,
      storeId: f.storeId, paymentStatus: f.paymentStatus, paymentMethod: f.paymentMethod, note: f.note.trim() || null,
    };
    this.busy.set(true);
    try {
      if (f.publicId) await this.client.update(f.publicId, body);
      else await this.client.add(body);
      this.toastr.success('COMMON.SAVE_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(r: Receipt): Promise<void> {
    this.busy.set(true);
    try {
      await this.client.delete(r.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search({
        keyword: this.keyword.trim() || undefined, completed: this.completed, fundId: this.fundId, mine: this.mine, from: this.from || null, to: this.to || null,
        createdFrom: this.createdFrom || null, createdTo: this.createdTo || null,
        pageIndex: this.pageIndex, pageSize: this.pageSize,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}

interface RegisterForm {
  line: ReceiptLine; mode: 'batch' | 'single'; quantity: number; prefix: string; digits: number; startNumber: number | null; barcode: string;
  storeId: number | null;
}

/**
 * Một phiếu nhận: thêm dòng (chọn biểu ghi hoặc từ đơn đặt), đăng ký ĐKCB cho từng dòng — không vượt số bản nhận của dòng (phân hệ Kho
 * khoá số lượng), xoá ĐKCB đăng ký nhầm, hoàn tất (khoá phiếu — giao nhận mới chọn được đơn này) / mở lại.
 */
@Component({
  selector: 'app-acq-receipt-detail',
  imports: [FormsModule, RouterLink, LDatePipe, LNumberPipe, Loading, Paginator, Modal, TranslatePipe, BibSource],
  template: `
    <div class="mb-5 flex flex-wrap gap-3 justify-between items-center">
      <div>
        <a routerLink="/ab-receipts" class="text-sm text-blue-600 hover:underline">← {{ 'ACQ_RECEIPT.TITLE' | translate }}</a>
        @if (receipt(); as r) {
          <h4 class="page-title">#{{ r.id }} {{ r.name ?? '' }} <span [class]="statusOf(r).badge + ' align-middle'">{{ statusOf(r).label | translate }}</span></h4>
          <div class="text-sm text-gray-500">
            {{ 'ACQ_RECEIPT.RECEIVED_ON' | translate }} {{ r.receivedOn | ldate: 'dd/MM/yyyy' }}
            @if (r.sourceName) { · {{ r.sourceName }} } @if (r.supplierName) { · {{ r.supplierName }} }
            @if (storeName(r.storeId); as s) { · {{ 'ACQ_RECEIPT.STORE' | translate }}: {{ s }} }
            @if (r.note) { · {{ r.note }} }
            · {{ 'ACQ.TOTAL' | translate }}: {{ r.total | lnumber: '1.0-0' }} · {{ paymentStatusName(r.paymentStatus) | translate }} · {{ paymentMethodName(r.paymentMethod) | translate }}
          </div>
        }
      </div>
      @if (receipt(); as r) {
        <div class="flex gap-2">
          @if (!r.completedAt && session.can('AB_RECEIPTS:edit')) {
            <button class="btn-primary" [disabled]="busy() || !lines().length" (click)="complete()">
              <span class="material-icons text-[18px]">lock</span> {{ 'ACQ_RECEIPT.COMPLETE' | translate }}</button>
          }
          @if (r.completedAt && session.can('AB_RECEIPTS:delete')) {
            <button class="btn-secondary" [disabled]="busy()" (click)="reopen()"><span class="material-icons text-[18px]">lock_open</span> {{ 'ACQ_RECEIPT.REOPEN' | translate }}</button>
          }
        </div>
      }
    </div>

    @if (receipt()?.completedAt) { <div class="alert-info !block mb-4 text-sm">{{ 'ACQ_RECEIPT.LOCKED_HINT' | translate }}</div> }
    @if (!itemsAvailable()) { <div class="alert-error !block mb-4 text-sm">{{ 'ACQ_RECEIPT.HOLDINGS_DOWN' | translate }}</div> }
    @if (receipt() && session.can('AB_RECEIPTS:edit')) {
      <div class="panel flex flex-wrap items-end gap-3">
        <div><label for="rd-ps" class="field-label">{{ 'ACQ.PAYMENT_STATUS' | translate }}</label>
          <select id="rd-ps" class="input" [(ngModel)]="payment.status">@for (s of paymentStatuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }</select></div>
        <div><label for="rd-pm" class="field-label">{{ 'ACQ.PAYMENT_METHOD' | translate }}</label>
          <select id="rd-pm" class="input" [(ngModel)]="payment.method">@for (m of paymentMethods; track m.code) { <option [ngValue]="m.code">{{ m.name | translate }}</option> }</select></div>
        <button type="button" class="btn-secondary" [disabled]="busy()" (click)="savePayment()"><span class="material-icons text-[18px]">payments</span> {{ 'COMMON.SAVE' | translate }}</button>
        <span class="text-[11px] text-gray-400 pb-2">{{ 'ACQ_RECEIPT.PAYMENT_HINT' | translate }}</span>
      </div>
    }

    @if (editable()) {
      <div class="panel">
        <div class="flex gap-2 mb-3">
          <button type="button" [class]="tab === 'bib' ? 'btn-primary' : 'btn-secondary'" (click)="tab = 'bib'">{{ 'ACQ_RECEIPT.ADD_BY_BIB' | translate }}</button>
          <button type="button" [class]="tab === 'order' ? 'btn-primary' : 'btn-secondary'" (click)="tab = 'order'; findReceivable()">{{ 'ACQ_RECEIPT.ADD_FROM_ORDER' | translate }}</button>
        </div>
        @if (tab === 'bib') {
          <div class="grid grid-cols-1 lg:grid-cols-5 gap-3 items-end">
            <div class="lg:col-span-2"><label for="rl-bib" class="form-label">{{ 'ACQ.BIB' | translate }} *</label>
              <app-acq-bib-source inputId="rl-bib" [selected]="add.bib" (picked)="add.bib = $event" /></div>
            <div><label for="rl-qty" class="form-label">{{ 'ACQ_RECEIPT.COPIES' | translate }} *</label><input id="rl-qty" type="number" min="1" class="input" [(ngModel)]="add.quantity" /></div>
            <div><label for="rl-price" class="form-label">{{ 'ACQ.PRICE' | translate }}</label>
              <div class="flex gap-1"><input id="rl-price" type="number" min="0" step="any" class="input" [(ngModel)]="add.price" />
                @if (currencies().length) {
                  <select class="input w-24" [attr.aria-label]="'ACQ.CURRENCY' | translate" [(ngModel)]="add.currency" (ngModelChange)="onCurrency()">
                    @for (c of currencies(); track c.code) { <option [ngValue]="c.code">{{ c.code }}</option> }
                  </select>
                } @else { <input class="input w-20 uppercase" maxlength="10" [attr.aria-label]="'ACQ.CURRENCY' | translate" [(ngModel)]="add.currency" /> }
                <input type="number" min="0" step="any" class="input w-24" [attr.aria-label]="'ACQ.RATE' | translate" [title]="'ACQ.RATE' | translate" [(ngModel)]="add.rate" /></div></div>
            <button type="button" class="btn-primary justify-center" [disabled]="busy() || !add.bib" (click)="addLine()">
              <span class="material-icons text-[18px]">playlist_add</span> {{ 'ACQ_RECEIPT.ADD_LINE' | translate }}</button>
          </div>
        } @else {
          <div class="flex flex-wrap gap-2 items-end mb-2">
            <div class="flex-1 min-w-[200px]"><label for="rv-kw" class="field-label">{{ 'ACQ_RECEIPT.RECEIVABLE_SEARCH' | translate }}</label>
              <input id="rv-kw" class="input" [(ngModel)]="receivableKeyword" (keydown.enter)="findReceivable()" /></div>
            <button class="btn-secondary" (click)="findReceivable()"><span class="material-icons text-[18px]">search</span></button>
          </div>
          <div class="max-h-80 overflow-y-auto border border-gray-100 rounded-lg">
            <table class="w-full text-left border-collapse">
              <thead><tr>
                <th class="th w-20">{{ 'ACQ_RECEIPT.ORDER' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th>
                <th class="th w-28 !text-right">{{ 'ACQ_ORDER.REMAINING' | translate }}</th><th class="th w-28">MFN</th><th class="th w-28">{{ 'ACQ_RECEIPT.COPIES' | translate }}</th><th class="th w-24"></th>
              </tr></thead>
              <tbody>
                @for (o of receivable()?.items ?? []; track o.orderLinePublicId) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td font-mono text-[13px]">#{{ o.orderId }}</td>
                    <td class="td">{{ o.title }}<div class="text-[11px] text-gray-400">{{ o.orderName }}@if (o.author) { · {{ o.author }} }</div></td>
                    <td class="td text-right">{{ o.remaining | lnumber }} / {{ o.quantity | lnumber }}</td>
                    <td class="td">
                      @if (o.mfn) { <span class="font-mono text-[13px]">{{ o.mfn }}</span> }
                      @else { <input type="number" class="input py-1" [placeholder]="'ACQ_RECEIPT.MFN_PH' | translate" [attr.aria-label]="'MFN ' + o.title" [(ngModel)]="pickMfn[o.orderLinePublicId]" /> }
                    </td>
                    <td class="td"><input type="number" min="1" [max]="o.remaining" class="input py-1" [attr.aria-label]="'ACQ_RECEIPT.COPIES' | translate" [(ngModel)]="pickQty[o.orderLinePublicId]" /></td>
                    <td class="td"><button type="button" class="btn-primary !py-1" [disabled]="busy()" (click)="addFromOrder(o)">{{ 'ACQ_RECEIPT.RECEIVE' | translate }}</button></td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="py-6 text-center text-gray-500">{{ 'ACQ_RECEIPT.NO_RECEIVABLE' | translate }}</td></tr>
                }
              </tbody>
            </table>
          </div>
          @if (receivable(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="receivablePage" [pageSize]="10" (pageChange)="receivablePage = $event.pageIndex; loadReceivable()" /> }
        }
      </div>
    }

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative mb-5">
      @if (loading()) { <app-loading /> }
      <div class="px-5 py-3 border-b border-gray-100 font-semibold text-gray-800">{{ 'ACQ.LINES_N' | translate: { count: lines().length } }}</div>
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th><th class="th w-32 !text-right">{{ 'ACQ.PRICE' | translate }}</th>
            <th class="th w-32 !text-right">{{ 'ACQ.AMOUNT' | translate }}</th>
            <th class="th w-28 !text-right">{{ 'ACQ_RECEIPT.COPIES' | translate }}</th><th class="th w-40">{{ 'ACQ_RECEIPT.REGISTERED' | translate }}</th>
            <th class="th w-36"></th>
          </tr></thead>
          <tbody>
            @for (l of lines(); track l.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ l.title }}<div class="text-[11px] text-gray-400">MFN {{ l.mfn }}@if (l.author) { · {{ l.author }} }@if (l.orderId) { · {{ 'ACQ_RECEIPT.FROM_ORDER' | translate: { no: l.orderId } }} }
                  @if (session.can('CATALOG_BIBS:edit')) { · <a [routerLink]="['/catalog-bibs', l.mfn]" target="_blank" rel="noopener" class="text-blue-600 hover:underline">{{ 'ACQ_RECEIPT.EDIT_MARC' | translate }}</a> }</div></td>
                <td class="td text-right">@if (l.price !== null) { {{ l.price | lnumber: '1.0-2' }} <span class="text-[11px] text-gray-400">{{ l.currency }}</span> }
                  @if (l.rate !== 1) { <div class="text-[11px] text-gray-400">× {{ l.rate | lnumber: '1.0-4' }}</div> }</td>
                <td class="td text-right">@if (l.price !== null) { {{ l.quantity * l.price * l.rate | lnumber: '1.0-0' }} }</td>
                <td class="td text-right">
                  @if (editingQty() === l.publicId) {
                    <div class="flex gap-1 justify-end"><input type="number" min="1" class="input py-1 w-20" [attr.aria-label]="'ACQ_RECEIPT.COPIES' | translate" [(ngModel)]="qtyDraft" (keydown.enter)="saveQty(l)" />
                      <button class="icon-btn text-green-600" [title]="'COMMON.SAVE' | translate" (click)="saveQty(l)"><span class="material-icons text-[18px]">check</span></button></div>
                  } @else { {{ l.quantity | lnumber }} }
                </td>
                <td class="td">
                  @if (l.registered !== null) {
                    <div class="flex items-center gap-2">
                      <div class="flex-1 h-2 rounded bg-gray-100"><div class="h-2 rounded" [class]="l.registered >= l.quantity ? 'bg-green-500' : 'bg-blue-500'" [style.width.%]="(l.registered * 100) / l.quantity"></div></div>
                      <span class="text-[12px] text-gray-600 whitespace-nowrap">{{ l.registered }}/{{ l.quantity }}</span>
                    </div>
                  } @else { <span class="text-gray-400">—</span> }
                </td>
                <td class="td">
                  @if (editable()) {
                    <div class="flex justify-end gap-1">
                      @if (l.registered !== null && l.registered < l.quantity) {
                        <button class="btn-primary !py-1" (click)="openRegister(l)"><span class="material-icons text-[16px]">qr_code</span> {{ 'ACQ_RECEIPT.REGISTER' | translate }}</button>
                      }
                      <button class="icon-btn text-gray-400 hover:text-blue-600" [title]="'ACQ_RECEIPT.EDIT_QTY' | translate" (click)="editingQty.set(l.publicId); qtyDraft = l.quantity"><span class="material-icons text-[18px]">edit</span></button>
                      @if (!l.registered) {
                        <button class="icon-btn text-gray-400 hover:text-red-600" [title]="'COMMON.DELETE' | translate" (click)="removeLine(l)"><span class="material-icons text-[18px]">close</span></button>
                      }
                    </div>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ 'ACQ_RECEIPT.NO_LINES' | translate }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    @if (items().length) {
      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
        <div class="px-5 py-3 border-b border-gray-100 flex items-center justify-between gap-3">
          <span class="font-semibold text-gray-800">{{ 'ACQ_RECEIPT.ITEMS_N' | translate: { count: items().length } }}</span>
          @if (session.can('PRINT_LABEL:view') || session.can('CATALOG_BIBS:view')) {
            <button type="button" class="btn-secondary !py-1.5 !px-3" (click)="printLabels()"><span class="material-icons text-[18px]">label</span> {{ 'ACQ_RECEIPT.PRINT_LABELS' | translate }}</button>
          }
        </div>
        <div class="overflow-x-auto w-full">
          <table class="w-full text-left border-collapse min-w-[760px]">
            <thead><tr>
              <th class="th w-40">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th>
              <th class="th w-40">{{ 'ITEMS_PAGE.STORE' | translate }}</th><th class="th w-32 !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-16"></th>
            </tr></thead>
            <tbody>
              @for (i of items(); track i.publicId) {
                <tr class="hover:bg-gray-50/50">
                  <td class="td font-mono text-[13px]">{{ i.barcode }}</td>
                  <td class="td">{{ titleOf(i) }}@if (i.delivered) { <div class="text-[11px] text-blue-600">{{ 'ACQ_RECEIPT.DELIVERED' | translate }}</div> }</td>
                  <td class="td">{{ i.storeName ?? '—' }}</td>
                  <td class="td text-center"><span [class]="status(i).badge">{{ status(i).name | translate }}</span></td>
                  <td class="td text-center">
                    @if (editable() && session.can('AB_RECEIPTS:delete') && i.status === 'I' && !i.delivered) {
                      <button class="icon-btn text-gray-400 hover:text-red-600" [title]="'ACQ_RECEIPT.REMOVE_ITEM' | translate" (click)="removeItem(i)"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    }

    @if (register(); as g) {
      <app-modal [title]="'ACQ_RECEIPT.REGISTER_TITLE' | translate: { mfn: g.line.mfn }" widthClass="max-w-lg" (closed)="register.set(null)">
        <div class="text-sm text-gray-600 mb-3">{{ g.line.title }} — {{ 'ACQ_RECEIPT.REMAINING_N' | translate: { count: g.line.quantity - (g.line.registered ?? 0) } }}</div>
        <div class="flex gap-2 mb-3">
          <button type="button" [class]="g.mode === 'batch' ? 'btn-primary' : 'btn-secondary'" (click)="g.mode = 'batch'">{{ 'ACQ_RECEIPT.MODE_BATCH' | translate }}</button>
          <button type="button" [class]="g.mode === 'single' ? 'btn-primary' : 'btn-secondary'" (click)="g.mode = 'single'">{{ 'ACQ_RECEIPT.MODE_SINGLE' | translate }}</button>
        </div>
        <div class="grid grid-cols-2 gap-3">
          @if (g.mode === 'batch') {
            <div><label for="rg-pre" class="form-label">{{ 'ACQ_RECEIPT.PREFIX' | translate }}</label><input id="rg-pre" class="input font-mono uppercase" maxlength="20" [(ngModel)]="g.prefix" (change)="previewNext(g)" /></div>
            <div><label for="rg-dig" class="form-label">{{ 'ACQ_RECEIPT.DIGITS' | translate }}</label><input id="rg-dig" type="number" min="1" max="18" class="input" [(ngModel)]="g.digits" (change)="previewNext(g)" /></div>
            <div><label for="rg-qty" class="form-label">{{ 'ACQ_RECEIPT.COPIES' | translate }} *</label><input id="rg-qty" type="number" min="1" class="input" [(ngModel)]="g.quantity" /></div>
            <div><label for="rg-start" class="form-label">{{ 'ACQ_RECEIPT.START_NUMBER' | translate }}</label><input id="rg-start" type="number" min="1" class="input" [(ngModel)]="g.startNumber" /></div>
            @if (nextBarcode(); as next) { <div class="col-span-2 text-[12px] text-gray-500">{{ 'ACQ_RECEIPT.NEXT_BARCODE' | translate: { barcode: next } }}</div> }
          } @else {
            <div class="col-span-2"><label for="rg-bc" class="form-label">{{ 'ITEMS_PAGE.BARCODE' | translate }} *</label><input id="rg-bc" class="input font-mono" maxlength="50" [(ngModel)]="g.barcode" /></div>
          }
          <div class="col-span-2"><label for="rg-store" class="form-label">{{ 'ITEMS_PAGE.STORE' | translate }}</label>
            <select id="rg-store" class="input" [(ngModel)]="g.storeId"><option [ngValue]="null">—</option>@for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
        </div>
        @if (registered(); as done) {
          <div class="alert-success !block mt-3 text-sm">{{ 'ACQ_RECEIPT.REGISTERED_N' | translate: { count: done.length } }}
            <span class="font-mono text-xs">{{ done[0].barcode }}@if (done.length > 1) { – {{ done[done.length - 1].barcode }} }</span></div>
        }
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="register.set(null)">{{ 'COMMON.CLOSE' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="submitRegister(g)">{{ 'ACQ_RECEIPT.REGISTER' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class ReceiptDetail implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly paymentStatuses = PAYMENT_STATUSES;
  protected readonly paymentMethods = PAYMENT_METHODS;
  protected payment = { status: 0, method: 0 };
  protected tab: 'bib' | 'order' = 'bib';
  protected add: { bib: AcqBib | null; quantity: number; price: number | null; currency: string; rate: number } =
    { bib: null, quantity: 1, price: null, currency: 'VND', rate: 1 };
  protected receivableKeyword = '';
  protected receivablePage = 1;
  protected pickQty: Record<string, number> = {};
  protected pickMfn: Record<string, number | null> = {};
  protected qtyDraft = 1;
  protected readonly receipt = signal<Receipt | null>(null);
  protected readonly currencies = signal<Currency[]>([]);
  protected readonly lines = signal<ReceiptLine[]>([]);
  protected readonly items = signal<ReceiptItem[]>([]);
  protected readonly itemsAvailable = signal(true);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly receivable = signal<CrudPage<ReceivableLine> | null>(null);
  protected readonly editingQty = signal<string | null>(null);
  protected readonly register = signal<RegisterForm | null>(null);
  protected readonly registered = signal<RegisteredItem[] | null>(null);
  protected readonly nextBarcode = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly editable = computed(() => {
    const r = this.receipt();
    return !!r && !r.completedAt && this.session.can('AB_RECEIPTS:edit');
  });

  private readonly router = inject(Router);

  async ngOnInit(): Promise<void> {
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
    void loadCurrencies(this.api).then((c) => this.currencies.set(c));
    await this.load();
  }

  protected statusOf(r: Receipt) {
    return receiptStatus(r);
  }

  protected paymentStatusName(code: number): string {
    return (PAYMENT_STATUSES.find((s) => s.code === code) ?? PAYMENT_STATUSES[0]).name;
  }

  protected paymentMethodName(code: number): string {
    return (PAYMENT_METHODS.find((m) => m.code === code) ?? PAYMENT_METHODS[0]).name;
  }

  /** Đổi tiền tệ → tỷ giá gợi ý theo danh mục tiền tệ của đơn vị (sửa tay được). */
  protected onCurrency(): void {
    const c = this.currencies().find((x) => x.code === this.add.currency);
    if (c) this.add.rate = c.exchangeRate;
  }

  protected storeName(id: number | null): string | null {
    const s = id === null ? null : this.stores().find((x) => x.id === id);
    return s ? `${s.code} — ${s.name}` : null;
  }

  protected status(i: ReceiptItem) {
    return itemStatus(i.status, i.onLoan);
  }

  protected titleOf(i: ReceiptItem): string {
    return this.lines().find((l) => l.publicId === i.linePublicId)?.title ?? `MFN ${i.mfn}`;
  }

  protected async addLine(): Promise<void> {
    if (!this.add.bib) return;
    await this.saveLine({
      mfn: this.add.bib.mfn, quantity: Number(this.add.quantity) || 0, price: this.add.price === null || (this.add.price as unknown) === '' ? null : Number(this.add.price),
      currency: this.add.currency.trim() || null, rate: this.add.rate === null || (this.add.rate as unknown) === '' ? 1 : Number(this.add.rate),
    }, () => (this.add = { bib: null, quantity: 1, price: null, currency: this.add.currency, rate: this.add.rate }));
  }

  protected async addFromOrder(o: ReceivableLine): Promise<void> {
    await this.saveLine({
      orderLinePublicId: o.orderLinePublicId, mfn: o.mfn ?? this.pickMfn[o.orderLinePublicId] ?? null,
      quantity: Number(this.pickQty[o.orderLinePublicId] ?? o.remaining) || 0,
    }, () => void this.loadReceivable());
  }

  protected async saveQty(l: ReceiptLine): Promise<void> {
    await this.saveLine({ linePublicId: l.publicId, quantity: Number(this.qtyDraft) || 0, price: l.price, currency: l.currency, rate: l.rate },
      () => this.editingQty.set(null));
  }

  protected findReceivable(): void {
    this.receivablePage = 1;
    void this.loadReceivable();
  }

  protected async loadReceivable(): Promise<void> {
    try {
      this.receivable.set(await this.api.receivableLines({
        keyword: this.receivableKeyword.trim() || undefined, supplierId: this.receipt()?.supplierId ?? null, pageIndex: this.receivablePage, pageSize: 10,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async removeLine(l: ReceiptLine): Promise<void> {
    await this.run(() => this.api.removeReceiptLine(l.publicId));
  }

  protected openRegister(l: ReceiptLine): void {
    const form: RegisterForm = {
      line: l, mode: 'batch', quantity: l.quantity - (l.registered ?? 0), prefix: '', digits: 6, startNumber: null, barcode: '',
      storeId: this.receipt()?.storeId ?? null,
    };
    this.registered.set(null);
    this.register.set(form);
    void this.previewNext(form);
  }

  protected async previewNext(g: RegisterForm): Promise<void> {
    try {
      this.nextBarcode.set((await this.api.nextBarcode(g.prefix.trim().toUpperCase(), Number(g.digits) || 6)).barcode);
    } catch {
      this.nextBarcode.set(null); // không có quyền xem số tiếp theo — vẫn đăng ký được
    }
  }

  protected async submitRegister(g: RegisterForm): Promise<void> {
    this.busy.set(true);
    try {
      const done = await this.api.registerReceiptItems(g.line.publicId, g.mode === 'single'
        ? { barcode: g.barcode.trim(), storeId: g.storeId }
        : { quantity: Number(g.quantity) || 0, prefix: g.prefix.trim() || null, digits: Number(g.digits) || 6, startNumber: g.startNumber || null, storeId: g.storeId });
      this.registered.set(done);
      this.toastr.success('ACQ_RECEIPT.REGISTERED_N', { count: done.length });
      await this.load();
      const line = this.lines().find((l) => l.publicId === g.line.publicId);
      if (line) {
        g.line = line;
        g.quantity = line.quantity - (line.registered ?? 0);
        g.barcode = '';
        g.startNumber = null;
        void this.previewNext(g);
      }
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Chuyển toàn bộ ĐKCB của phiếu sang màn in nhãn (mã vạch hoặc nhãn môn loại, người dùng chọn ở đó). */
  protected printLabels(): void {
    try {
      sessionStorage.setItem(LABEL_BARCODES_KEY, JSON.stringify(this.items().map((i) => i.barcode)));
    } catch {
      this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
      return;
    }
    void this.router.navigateByUrl('/print-labels');
  }

  protected async removeItem(i: ReceiptItem): Promise<void> {
    await this.run(() => this.api.removeReceiptItem(this.id(), i.publicId));
  }

  protected async complete(): Promise<void> {
    const unregistered = this.lines().filter((l) => l.registered !== null && l.registered < l.quantity).length;
    if (unregistered > 0) this.toastr.warning('ACQ_RECEIPT.COMPLETE_PARTIAL', { count: unregistered });
    await this.run(async () => {
      await this.api.completeReceipt(this.id());
      this.toastr.success('ACQ_RECEIPT.COMPLETED');
    });
  }

  /** Chốt thanh toán: làm được cả khi phiếu đã hoàn tất (không đụng dòng và ĐKCB). */
  protected async savePayment(): Promise<void> {
    await this.run(async () => {
      await this.api.setReceiptPayment(this.id(), { paymentStatus: Number(this.payment.status), paymentMethod: Number(this.payment.method) });
      this.toastr.success('COMMON.SAVE_SUCCESS');
    });
  }

  protected async reopen(): Promise<void> {
    await this.run(() => this.api.reopenReceipt(this.id()));
  }

  private async saveLine(body: Parameters<Api['saveReceiptLine']>[1], after: () => void): Promise<void> {
    await this.run(async () => {
      await this.api.saveReceiptLine(this.id(), body);
      after();
    });
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [receipt, view] = await Promise.all([this.api.crud<Receipt>('receipts', 'acquisition').get(this.id()), this.api.receiptLines(this.id())]);
      this.receipt.set(receipt);
      this.payment = { status: receipt.paymentStatus, method: receipt.paymentMethod };
      this.lines.set(view.lines);
      this.items.set(view.items);
      this.itemsAvailable.set(view.itemsAvailable);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
