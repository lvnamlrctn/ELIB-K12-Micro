import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AcqBib, AcquisitionOptions, Api, CrudPage, Currency, OrderLine, PurchaseOrder, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';
import { BibPicker } from './bib-picker';
import { DateInput } from '../../shared/date-input';

/** Tình trạng đơn đặt: 1 mới lập, 2 đã gửi đặt, 3 đã huỷ. */
export const ORDER_STATUSES: { code: number; name: string; badge: string }[] = [
  { code: 1, name: 'ACQ_ORDER.STATUS_DRAFT', badge: 'badge-info' },
  { code: 2, name: 'ACQ_ORDER.STATUS_SENT', badge: 'badge-on' },
  { code: 3, name: 'ACQ_ORDER.STATUS_CANCELLED', badge: 'badge-off' },
];

export function orderStatus(code: number) {
  return ORDER_STATUSES.find((s) => s.code === code) ?? ORDER_STATUSES[0];
}

/** Phương thức thanh toán: 0 tiền mặt, 1 chuyển khoản. */
export const PAYMENT_METHODS: { code: number; name: string }[] = [
  { code: 0, name: 'ACQ.PM_CASH' },
  { code: 1, name: 'ACQ.PM_TRANSFER' },
];

/** Tiền tệ đang dùng (kèm tỷ giá gợi ý). Không có quyền xem / lỗi → rỗng: ô tiền tệ chuyển sang nhập tay. */
export async function loadCurrencies(api: Api): Promise<Currency[]> {
  try {
    return await api.currencyOptions();
  } catch {
    return [];
  }
}

const EMPTY_OPTIONS: AcquisitionOptions = { sources: [], suppliers: [], budgets: [], funds: [] };

interface OrderForm {
  publicId: string | null; name: string; orderedOn: string; dueOn: string; sourceId: number | null; supplierId: number | null;
  budgetId: number | null; fundId: number | null; status: number; paymentMethod: number; note: string;
}

/** Đơn đặt bổ sung (monolith: /admin/ab-orders, quyền AB_ORDERS): danh sách, thêm/sửa/xoá đầu đơn; mở đơn để lập dòng đặt. */
@Component({
  selector: 'app-acq-orders',
  imports: [DateInput, FormsModule, RouterLink, LDatePipe, LNumberPipe, Loading, Paginator, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5 flex justify-between items-center">
      <h4 class="page-title">{{ 'ACQ_ORDER.TITLE' | translate }}</h4>
      @if (session.can('AB_ORDERS:add')) {
        <button class="btn-primary" (click)="edit(null)"><span class="material-icons text-[18px]">add</span> {{ 'ACQ_ORDER.ADD' | translate }}</button>
      }
    </div>
    <div class="panel flex flex-wrap gap-3 items-end">
      <div class="flex-1 min-w-[220px]">
        <label for="ao-kw" class="field-label">{{ 'ACQ_ORDER.SEARCH_LABEL' | translate }}</label>
        <input id="ao-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="find()" />
      </div>
      <div class="w-48">
        <label for="ao-st" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
        <select id="ao-st" class="input" [(ngModel)]="status" (change)="find()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }
        </select>
      </div>
      <button class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
    </div>
    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-20">{{ 'ACQ.NO' | translate }}</th><th class="th">{{ 'ACQ_ORDER.NAME' | translate }}</th>
            <th class="th w-48">{{ 'ACQ.SUPPLIER' | translate }}</th><th class="th w-28">{{ 'ACQ_ORDER.ORDERED_ON' | translate }}</th>
            <th class="th w-32 !text-right">{{ 'ACQ_ORDER.RECEIVED_OF' | translate }}</th><th class="th w-36 !text-right">{{ 'ACQ.TOTAL' | translate }}</th>
            <th class="th w-32 !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (o of page()?.items ?? []; track o.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td"><a [routerLink]="['/ab-orders', o.publicId]" class="font-mono font-medium text-blue-700 hover:underline">#{{ o.id }}</a></td>
                <td class="td">{{ o.name }}<div class="text-[11px] text-gray-400">{{ 'ACQ.LINES_N' | translate: { count: o.lineCount } }}</div></td>
                <td class="td">{{ o.supplierName ?? '—' }}</td>
                <td class="td">{{ o.orderedOn ? (o.orderedOn | ldate: 'dd/MM/yyyy') : '' }}</td>
                <td class="td text-right">{{ o.received | lnumber }} / {{ o.quantity | lnumber }}</td>
                <td class="td text-right">{{ o.total | lnumber: '1.0-0' }}</td>
                <td class="td text-center"><span [class]="statusOf(o.status).badge">{{ statusOf(o.status).name | translate }}</span></td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    @if (session.can('AB_ORDERS:edit')) {
                      <button class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate" (click)="edit(o)"><span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (session.can('AB_ORDERS:delete') && o.received === 0) {
                      <button class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(o)"><span class="material-icons text-[18px]">delete</span></button>
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
      <app-modal [title]="(f.publicId ? 'COMMON.MODAL_EDIT_TITLE' : 'ACQ_ORDER.ADD') | translate" widthClass="max-w-2xl" (closed)="form.set(null)">
        <div class="grid grid-cols-2 gap-4">
          <div class="col-span-2"><label for="of-name" class="form-label">{{ 'ACQ_ORDER.NAME' | translate }} *</label><input id="of-name" class="input" maxlength="250" [(ngModel)]="f.name" /></div>
          <div><label for="of-od" class="form-label">{{ 'ACQ_ORDER.ORDERED_ON' | translate }}</label><app-date-input fieldId="of-od" [(ngModel)]="f.orderedOn"></app-date-input></div>
          <div><label for="of-dd" class="form-label">{{ 'ACQ_ORDER.DUE_ON' | translate }}</label><app-date-input fieldId="of-dd" [(ngModel)]="f.dueOn"></app-date-input></div>
          <div><label for="of-src" class="form-label">{{ 'ACQ.SOURCE' | translate }}</label>
            <select id="of-src" class="input" [(ngModel)]="f.sourceId"><option [ngValue]="null">—</option>@for (o of options().sources; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label for="of-sup" class="form-label">{{ 'ACQ.SUPPLIER' | translate }}</label>
            <select id="of-sup" class="input" [(ngModel)]="f.supplierId"><option [ngValue]="null">—</option>@for (o of options().suppliers; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label for="of-bud" class="form-label">{{ 'ACQ.BUDGET' | translate }}</label>
            <select id="of-bud" class="input" [(ngModel)]="f.budgetId"><option [ngValue]="null">—</option>@for (o of options().budgets; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label for="of-fund" class="form-label">{{ 'ACQ.FUND' | translate }}</label>
            <select id="of-fund" class="input" [(ngModel)]="f.fundId"><option [ngValue]="null">—</option>@for (o of options().funds; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
          <div><label for="of-st" class="form-label">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="of-st" class="input" [(ngModel)]="f.status">@for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }</select></div>
          <div><label for="of-pm" class="form-label">{{ 'ACQ.PAYMENT_METHOD' | translate }}</label>
            <select id="of-pm" class="input" [(ngModel)]="f.paymentMethod">@for (m of paymentMethods; track m.code) { <option [ngValue]="m.code">{{ m.name | translate }}</option> }</select></div>
          <div class="col-span-2"><label for="of-note" class="form-label">{{ 'COMMON.NOTE' | translate }}</label><input id="of-note" class="input" maxlength="500" [(ngModel)]="f.note" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="save(f)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as o) {
      <app-confirm-delete [busy]="busy()" message="ACQ_ORDER.DELETE_CONFIRM" [messageParams]="{ no: o.id }" (confirmed)="remove(o)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Orders implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);
  private readonly client = this.api.crud<PurchaseOrder>('orders', 'acquisition');

  protected readonly statuses = ORDER_STATUSES;
  protected readonly paymentMethods = PAYMENT_METHODS;
  protected keyword = '';
  protected status: number | null = null;
  protected pageIndex = 1;
  protected pageSize = 10;
  protected readonly page = signal<CrudPage<PurchaseOrder> | null>(null);
  protected readonly options = signal<AcquisitionOptions>(EMPTY_OPTIONS);
  protected readonly form = signal<OrderForm | null>(null);
  protected readonly deleting = signal<PurchaseOrder | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.acquisitionOptions().then((o) => this.options.set(o), () => undefined);
  }

  protected statusOf(code: number) {
    return orderStatus(code);
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

  protected edit(o: PurchaseOrder | null): void {
    this.form.set({
      publicId: o?.publicId ?? null, name: o?.name ?? '', orderedOn: o?.orderedOn ?? today(), dueOn: o?.dueOn ?? '', sourceId: o?.sourceId ?? null,
      supplierId: o?.supplierId ?? null, budgetId: o?.budgetId ?? null, fundId: o?.fundId ?? null, status: o?.status ?? 1, paymentMethod: o?.paymentMethod ?? 0, note: o?.note ?? '',
    });
  }

  protected async save(f: OrderForm): Promise<void> {
    if (!f.name.trim()) {
      this.toastr.warning('ACQ_ORDER.NAME_REQUIRED');
      return;
    }
    const body = {
      name: f.name.trim(), orderedOn: f.orderedOn || null, dueOn: f.dueOn || null, sourceId: f.sourceId, supplierId: f.supplierId, budgetId: f.budgetId,
      fundId: f.fundId, status: f.status, paymentMethod: f.paymentMethod, note: f.note.trim() || null,
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

  protected async remove(o: PurchaseOrder): Promise<void> {
    this.busy.set(true);
    try {
      await this.client.delete(o.publicId);
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
        keyword: this.keyword.trim() || undefined, orderStatus: this.status, pageIndex: this.pageIndex, pageSize: this.pageSize,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}

interface LineForm {
  linePublicId: string | null; bib: AcqBib | null; title: string; author: string; publisher: string; publishYear: string; isbn: string;
  quantity: number; price: number | null; currency: string; rate: number; cancelReason: string;
}

const emptyLine = (): LineForm => ({
  linePublicId: null, bib: null, title: '', author: '', publisher: '', publishYear: '', isbn: '', quantity: 1, price: null, currency: 'VND', rate: 1, cancelReason: '',
});

/**
 * Một đơn đặt: lập dòng đặt — chọn biểu ghi đã có (MFN) hoặc nhập mô tả tài liệu chưa biên mục; xem số đã nhận / còn chờ nhận theo các
 * phiếu nhận. Đơn đã huỷ không sửa dòng.
 */
@Component({
  selector: 'app-acq-order-detail',
  imports: [FormsModule, RouterLink, LDatePipe, LNumberPipe, Loading, TranslatePipe, BibPicker],
  template: `
    <div class="mb-5">
      <a routerLink="/ab-orders" class="text-sm text-blue-600 hover:underline">← {{ 'ACQ_ORDER.TITLE' | translate }}</a>
      @if (order(); as o) {
        <h4 class="page-title">#{{ o.id }} {{ o.name }} <span [class]="statusOf(o.status).badge + ' align-middle'">{{ statusOf(o.status).name | translate }}</span></h4>
        <div class="text-sm text-gray-500">
          {{ o.supplierName ?? '—' }}@if (o.orderedOn) { · {{ 'ACQ_ORDER.ORDERED_ON' | translate }} {{ o.orderedOn | ldate: 'dd/MM/yyyy' }} }
          @if (o.dueOn) { · {{ 'ACQ_ORDER.DUE_ON' | translate }} {{ o.dueOn | ldate: 'dd/MM/yyyy' }} }
          · {{ 'ACQ_ORDER.RECEIVED_OF' | translate }}: {{ o.received | lnumber }} / {{ o.quantity | lnumber }}
          · {{ 'ACQ.TOTAL' | translate }}: {{ o.total | lnumber: '1.0-0' }} · {{ paymentOf(o.paymentMethod) | translate }}
        </div>
      }
    </div>

    @if (editable()) {
      <div class="panel">
        <div class="font-semibold text-gray-800 mb-3">{{ (line.linePublicId ? 'ACQ_ORDER.EDIT_LINE' : 'ACQ_ORDER.ADD_LINE') | translate }}</div>
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <div>
            <label for="ol-bib" class="form-label">{{ 'ACQ.BIB' | translate }}</label>
            <app-acq-bib-picker inputId="ol-bib" [selected]="line.bib" (picked)="line.bib = $event" />
            <p class="text-[11px] text-gray-400 mt-1">{{ 'ACQ_ORDER.BIB_HINT' | translate }}</p>
          </div>
          @if (!line.bib) {
            <div class="grid grid-cols-2 gap-3">
              <div class="col-span-2"><label for="ol-title" class="form-label">{{ 'ITEMS_PAGE.TITLE_COL' | translate }} *</label><input id="ol-title" class="input" maxlength="1000" [(ngModel)]="line.title" /></div>
              <div><label for="ol-author" class="form-label">{{ 'ITEMS_PAGE.AUTHOR' | translate }}</label><input id="ol-author" class="input" maxlength="500" [(ngModel)]="line.author" /></div>
              <div><label for="ol-pub" class="form-label">{{ 'ACQ.PUBLISHER' | translate }}</label><input id="ol-pub" class="input" maxlength="500" [(ngModel)]="line.publisher" /></div>
              <div><label for="ol-year" class="form-label">{{ 'ACQ.YEAR' | translate }}</label><input id="ol-year" class="input" maxlength="4" [(ngModel)]="line.publishYear" /></div>
              <div><label for="ol-isbn" class="form-label">ISBN</label><input id="ol-isbn" class="input font-mono" maxlength="100" [(ngModel)]="line.isbn" /></div>
            </div>
          }
        </div>
        <div class="grid grid-cols-2 md:grid-cols-6 gap-3 mt-4 items-end">
          <div><label for="ol-qty" class="form-label">{{ 'ACQ.QUANTITY' | translate }} *</label><input id="ol-qty" type="number" min="1" class="input" [(ngModel)]="line.quantity" /></div>
          <div><label for="ol-price" class="form-label">{{ 'ACQ.PRICE' | translate }}</label><input id="ol-price" type="number" min="0" step="any" class="input" [(ngModel)]="line.price" /></div>
          <div><label for="ol-cur" class="form-label">{{ 'ACQ.CURRENCY' | translate }}</label>
            @if (currencies().length) {
              <select id="ol-cur" class="input" [(ngModel)]="line.currency" (ngModelChange)="onCurrency(line)">
                @for (c of currencies(); track c.code) { <option [ngValue]="c.code">{{ c.code }}</option> }
              </select>
            } @else { <input id="ol-cur" class="input uppercase" maxlength="10" [(ngModel)]="line.currency" /> }</div>
          <div><label for="ol-rate" class="form-label">{{ 'ACQ.RATE' | translate }}</label><input id="ol-rate" type="number" min="0" step="any" class="input" [(ngModel)]="line.rate" /></div>
          <div><label for="ol-cancel" class="form-label">{{ 'ACQ_ORDER.CANCEL_REASON' | translate }}</label><input id="ol-cancel" class="input" maxlength="500" [(ngModel)]="line.cancelReason" /></div>
          <div class="flex gap-2 justify-end">
            @if (line.linePublicId) { <button type="button" class="btn-secondary" (click)="resetLine()">{{ 'COMMON.CANCEL' | translate }}</button> }
            <button type="button" class="btn-primary" [disabled]="busy()" (click)="saveLine()"><span class="material-icons text-[18px]">save</span> {{ 'COMMON.SAVE' | translate }}</button>
          </div>
        </div>
      </div>
    }

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="px-5 py-3 border-b border-gray-100 font-semibold text-gray-800">{{ 'ACQ.LINES_N' | translate: { count: lines().length } }}</div>
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th><th class="th w-36">ISBN</th>
            <th class="th w-20 !text-right">{{ 'ACQ.QUANTITY' | translate }}</th><th class="th w-32 !text-right">{{ 'ACQ.PRICE' | translate }}</th>
            <th class="th w-32 !text-right">{{ 'ACQ.AMOUNT' | translate }}</th>
            <th class="th w-24 !text-right">{{ 'ACQ_ORDER.RECEIVED' | translate }}</th><th class="th w-24 !text-right">{{ 'ACQ_ORDER.REMAINING' | translate }}</th>
            <th class="th w-24"></th>
          </tr></thead>
          <tbody>
            @for (l of lines(); track l.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ l.title }}
                  <div class="text-[11px] text-gray-400">@if (l.mfn) { MFN {{ l.mfn }} } @else { {{ 'ACQ_ORDER.NOT_CATALOGUED' | translate }} }@if (l.author) { · {{ l.author }} }</div>
                  @if (l.cancelReason) { <div class="text-[11px] text-red-600">{{ 'ACQ_ORDER.CANCELLED_LINE' | translate: { reason: l.cancelReason } }}</div> }</td>
                <td class="td font-mono text-[12px]">{{ l.isbn ?? '' }}</td>
                <td class="td text-right">{{ l.quantity | lnumber }}</td>
                <td class="td text-right">@if (l.price !== null) { {{ l.price | lnumber: '1.0-2' }} <span class="text-[11px] text-gray-400">{{ l.currency }}</span> }
                  @if (l.rate !== 1) { <div class="text-[11px] text-gray-400">× {{ l.rate | lnumber: '1.0-4' }}</div> }</td>
                <td class="td text-right">@if (l.price !== null) { {{ l.quantity * l.price * l.rate | lnumber: '1.0-0' }} }</td>
                <td class="td text-right">{{ l.received | lnumber }}</td>
                <td class="td text-right font-medium" [class.text-green-700]="l.remaining === 0">{{ l.remaining | lnumber }}</td>
                <td class="td">
                  @if (editable()) {
                    <div class="flex justify-center gap-1">
                      <button class="icon-btn text-gray-400 hover:text-blue-600" [title]="'COMMON.EDIT' | translate" (click)="editLine(l)"><span class="material-icons text-[18px]">edit</span></button>
                      @if (l.received === 0) {
                        <button class="icon-btn text-gray-400 hover:text-red-600" [title]="'COMMON.DELETE' | translate" (click)="removeLine(l)"><span class="material-icons text-[18px]">close</span></button>
                      }
                    </div>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="py-8 text-center text-gray-500">{{ 'ACQ_ORDER.NO_LINES' | translate }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class OrderDetail implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected line: LineForm = emptyLine();
  protected readonly order = signal<PurchaseOrder | null>(null);
  protected readonly currencies = signal<Currency[]>([]);
  protected readonly lines = signal<OrderLine[]>([]);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly editable = computed(() => {
    const o = this.order();
    return !!o && o.status !== 3 && this.session.can('AB_ORDERS:edit');
  });

  async ngOnInit(): Promise<void> {
    void loadCurrencies(this.api).then((c) => this.currencies.set(c));
    await this.load();
  }

  protected statusOf(code: number) {
    return orderStatus(code);
  }

  protected paymentOf(code: number): string {
    return (PAYMENT_METHODS.find((m) => m.code === code) ?? PAYMENT_METHODS[0]).name;
  }

  /** Đổi tiền tệ → tỷ giá gợi ý theo danh mục tiền tệ của đơn vị (sửa tay được). */
  protected onCurrency(l: LineForm): void {
    const c = this.currencies().find((x) => x.code === l.currency);
    if (c) l.rate = c.exchangeRate;
  }

  protected editLine(l: OrderLine): void {
    this.line = {
      linePublicId: l.publicId,
      bib: l.mfn && l.bibPublicId ? { bibPublicId: l.bibPublicId, mfn: l.mfn, title: l.title, author: l.author, publisher: l.publisher, publishYear: l.publishYear, isbns: l.isbn } : null,
      title: l.title, author: l.author ?? '', publisher: l.publisher ?? '', publishYear: l.publishYear ?? '', isbn: l.isbn ?? '',
      quantity: l.quantity, price: l.price, currency: l.currency ?? '', rate: l.rate, cancelReason: l.cancelReason ?? '',
    };
  }

  protected resetLine(): void {
    this.line = emptyLine();
  }

  protected async saveLine(): Promise<void> {
    const l = this.line;
    const text = (v: string) => v.trim() || null;
    this.busy.set(true);
    try {
      await this.api.saveOrderLine(this.id(), {
        linePublicId: l.linePublicId, mfn: l.bib?.mfn ?? null, title: l.bib ? null : text(l.title), author: l.bib ? null : text(l.author),
        publisher: l.bib ? null : text(l.publisher), publishYear: l.bib ? null : text(l.publishYear), isbn: text(l.isbn),
        quantity: Number(l.quantity) || 0, price: l.price === null || (l.price as unknown) === '' ? null : Number(l.price),
        currency: text(l.currency), rate: l.rate === null || (l.rate as unknown) === '' ? 1 : Number(l.rate), cancelReason: text(l.cancelReason),
      });
      this.toastr.success('COMMON.SAVE_SUCCESS');
      this.resetLine();
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async removeLine(l: OrderLine): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.removeOrderLine(l.publicId);
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
      const [order, lines] = await Promise.all([this.api.crud<PurchaseOrder>('orders', 'acquisition').get(this.id()), this.api.orderLines(this.id())]);
      this.order.set(order);
      this.lines.set(lines);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}

/** Ngày hôm nay yyyy-MM-dd theo giờ máy. */
export function today(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
