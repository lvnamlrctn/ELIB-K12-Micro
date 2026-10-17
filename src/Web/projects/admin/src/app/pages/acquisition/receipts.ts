import { NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal, viewChild } from '@angular/core';
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
import { HasUnsavedChanges, LeaveConfirm, UnsavedConfirm } from '../../shared/unsaved';
import { itemStatus } from '../holdings/items';
import { LABEL_BARCODES_KEY } from '../holdings/labels';
import { BibLookupDialog } from './bib-lookup-dialog';
import { BibMarcNew } from './bib-marc-new';
import { BibSource } from './bib-source';
import { PAYMENT_METHODS, loadCurrencies, today } from './orders';

const EMPTY_OPTIONS: AcquisitionOptions = { sources: [], suppliers: [], budgets: [], funds: [] };

/** Form thông tin đơn ở trang chi tiết (ELIB-LRC: card 4 cột + "Lưu lại"). */
interface ReceiptForm {
  name: string; receivedOn: string; sourceId: number | null; supplierId: number | null; budgetId: number | null;
  fundId: number | null; storeId: number | null; paymentStatus: number; paymentMethod: number; note: string;
}

function toForm(r: Receipt): ReceiptForm {
  return {
    name: r.name ?? '', receivedOn: r.receivedOn, sourceId: r.sourceId, supplierId: r.supplierId, budgetId: r.budgetId, fundId: r.fundId,
    storeId: r.storeId, paymentStatus: r.paymentStatus, paymentMethod: r.paymentMethod, note: r.note ?? '',
  };
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
    <div class="mb-5 flex justify-between items-center gap-2 flex-wrap">
      <h4 class="page-title">{{ 'ACQ_RECEIPT.TITLE' | translate }}</h4>
      <div class="flex gap-2">
        <a routerLink="/ab-receipts/lookup" class="btn-secondary"><span class="material-icons text-[18px]">manage_search</span> {{ 'ACQ_LOOKUP.TITLE' | translate }}</a>
        @if (session.can('AB_RECEIPTS:add')) {
          <button class="btn-primary" (click)="startCreate()"><span class="material-icons text-[18px]">add</span> {{ 'ACQ_RECEIPT.ADD' | translate }}</button>
        }
      </div>
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
                <td class="td"><a [routerLink]="['/ab-receipts', r.publicId]" class="text-blue-700 hover:underline">{{ r.name || ('ACQ_RECEIPT.NO_NAME' | translate) }}</a>
                  <div class="text-[11px] text-gray-400">{{ r.sourceName ?? '' }}</div></td>
                <td class="td">{{ r.supplierName ?? '—' }}</td>
                <td class="td text-right">{{ r.quantity | lnumber }}<div class="text-[11px] text-gray-400">{{ 'ACQ.LINES_N' | translate: { count: r.lineCount } }}</div></td>
                <td class="td text-right">{{ r.total | lnumber: '1.0-0' }}</td>
                <td class="td text-center"><span [class]="statusOf(r).badge">{{ statusOf(r).label | translate }}</span>
                  <div class="mt-1"><span [class]="r.paymentStatus === 2 ? 'badge-on' : 'badge-off'">{{ (r.paymentStatus === 2 ? 'ACQ.PS_PAID' : 'ACQ.PS_UNPAID') | translate }}</span></div></td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    @if (!r.completedAt && session.can('AB_RECEIPTS:edit')) {
                      <a class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate" [routerLink]="['/ab-receipts', r.publicId]"><span class="material-icons text-[18px]">edit</span></a>
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

    @if (creating(); as c) {
      <!-- Như ELIB-LRC: tạo đơn chỉ cần tên + ngày nhận, các thông tin còn lại và tài liệu nhập ở trang chi tiết. -->
      <app-modal [title]="'ACQ_RECEIPT.CREATE_TITLE' | translate" widthClass="max-w-md" (closed)="creating.set(null)">
        <form id="rc-form" class="space-y-4" (ngSubmit)="create(c)">
          <div><label for="rc-name" class="form-label">{{ 'ACQ_RECEIPT.NAME' | translate }} <span class="text-red-500">*</span></label>
            <input id="rc-name" name="name" class="input" maxlength="250" required [(ngModel)]="c.name" [class.!border-red-400]="c.touched && !c.name.trim()" /></div>
          <div><label for="rc-on" class="form-label">{{ 'ACQ_RECEIPT.RECEIVED_ON' | translate }}</label>
            <input id="rc-on" name="on" type="date" class="input" [(ngModel)]="c.receivedOn" /></div>
          <p class="text-xs text-gray-500">{{ 'ACQ_RECEIPT.CREATE_HINT' | translate }}</p>
        </form>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="creating.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="rc-form" class="btn-primary" [disabled]="busy()">
            {{ 'ACQ_RECEIPT.NEXT' | translate }} <span class="material-icons text-[18px]" [class.animate-spin]="busy()">{{ busy() ? 'autorenew' : 'arrow_forward' }}</span></button>
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
  private readonly router = inject(Router);
  protected readonly session = inject(Session);
  private readonly client = this.api.crud<Receipt>('receipts', 'acquisition');

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
  protected readonly creating = signal<{ name: string; receivedOn: string; touched: boolean } | null>(null);
  protected readonly deleting = signal<Receipt | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.acquisitionOptions().then((o) => this.options.set(o), () => undefined);
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

  protected startCreate(): void {
    this.creating.set({ name: '', receivedOn: today(), touched: false });
  }

  /** "Tiếp theo" (ELIB-LRC): tạo đơn với tên + ngày nhận (trống = hôm nay) rồi mở trang chi tiết để điền phần còn lại. */
  protected async create(c: { name: string; receivedOn: string; touched: boolean }): Promise<void> {
    c.touched = true;
    if (!c.name.trim()) {
      this.toastr.warning('ACQ_RECEIPT.NAME_REQUIRED');
      return;
    }
    this.busy.set(true);
    try {
      const created = await this.client.add({ name: c.name.trim(), receivedOn: c.receivedOn || today(), paymentStatus: 0, paymentMethod: 0 });
      this.creating.set(null);
      void this.router.navigate(['/ab-receipts', created.publicId]);
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

/** Các nút thêm tài liệu kiểu ELIB-LRC. */
type AddMode = 'lookup' | 'new' | 'z3950' | 'marc' | 'order';

const ADD_TITLES: Record<AddMode, string> = {
  lookup: 'ACQ_RECEIPT.ADD_LOOKUP', new: 'ACQ_RECEIPT.ADD_SINGLE', z3950: 'ACQ_RECEIPT.ADD_Z3950', marc: 'ACQ_RECEIPT.ADD_MARC',
  order: 'ACQ_RECEIPT.ADD_FROM_ORDER_BTN',
};

/** bib null = đang chọn biểu ghi; có bib = bước thông tin dòng (số lượng, đơn giá, loại tiền, tỷ giá). */
interface AddLineState { mode: AddMode; bib: AcqBib | null; quantity: number; price: number | null; currency: string; rate: number; }

interface RegisterForm {
  line: ReceiptLine; mode: 'batch' | 'single'; prefix: string; digits: number; startNumber: number | null; barcode: string;
  storeId: number | null;
}

/**
 * Một phiếu nhận: thêm dòng (chọn biểu ghi hoặc từ đơn đặt), đăng ký ĐKCB cho từng dòng — không vượt số bản nhận của dòng (phân hệ Kho
 * khoá số lượng), xoá ĐKCB đăng ký nhầm, hoàn tất (khoá phiếu — giao nhận mới chọn được đơn này) / mở lại.
 */
@Component({
  selector: 'app-acq-receipt-detail',
  imports: [FormsModule, RouterLink, LNumberPipe, Loading, Paginator, Modal, TranslatePipe, BibSource, BibLookupDialog, BibMarcNew, NgTemplateOutlet, UnsavedConfirm],
  template: `
    <div class="mb-4 flex flex-wrap gap-3 justify-between items-center">
      <div class="flex items-center gap-3">
        <a routerLink="/ab-receipts" class="btn-secondary !py-1.5"><span class="material-icons text-[18px]">arrow_back</span> {{ 'ACQ_RECEIPT.BACK' | translate }}</a>
        <h4 class="page-title !mb-0">{{ 'ACQ_RECEIPT.DETAIL_TITLE' | translate }}
          @if (receipt(); as r) { <span [class]="statusOf(r).badge + ' align-middle ml-1'">{{ statusOf(r).label | translate }}</span> }</h4>
      </div>
      @if (receipt(); as r) {
        <div class="flex gap-2 flex-wrap">
          <a routerLink="/ab-receipts/lookup" class="btn-secondary"><span class="material-icons text-[18px]">manage_search</span> {{ 'ACQ_LOOKUP.TITLE' | translate }}</a>
          @if (session.can('AB_RECEIPTS:edit')) {
            <button class="btn-primary" [disabled]="busy()" (click)="saveHead()"><span class="material-icons text-[18px]">save</span> {{ 'ACQ_RECEIPT.SAVE' | translate }}</button>
          }
          @if (!r.completedAt && session.can('AB_RECEIPTS:edit')) {
            <button class="btn-secondary" [disabled]="busy() || !lines().length" (click)="complete()">
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

    @if (receipt(); as r) {
      @if (head; as h) {
        <!-- Thông tin đơn (ELIB-LRC: card 4 cột). Đơn đã hoàn tất: chỉ sửa được thanh toán. -->
        <div class="panel">
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <fieldset class="contents" [disabled]="!editable()">
              <div><label for="rh-code" class="form-label">{{ 'ACQ_RECEIPT.CODE' | translate }}</label><input id="rh-code" class="input bg-gray-50 font-mono" [value]="'#' + r.id" readonly /></div>
              <div class="sm:col-span-2"><label for="rh-name" class="form-label">{{ 'ACQ_RECEIPT.NAME' | translate }} <span class="text-red-500">*</span></label>
                <input id="rh-name" class="input" maxlength="250" [(ngModel)]="h.name" (ngModelChange)="dirty = true" [class.!border-red-400]="!h.name.trim()" /></div>
              <div><label for="rh-on" class="form-label">{{ 'ACQ_RECEIPT.RECEIVED_ON' | translate }}</label>
                <input id="rh-on" type="date" class="input" [(ngModel)]="h.receivedOn" (ngModelChange)="dirty = true" /></div>
              <div><label for="rh-src" class="form-label">{{ 'ACQ.SOURCE' | translate }}</label>
                <select id="rh-src" class="input" [(ngModel)]="h.sourceId" (ngModelChange)="dirty = true"><option [ngValue]="null">{{ 'ACQ_RECEIPT.CHOOSE' | translate }}</option>
                  @for (o of options().sources; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
              <div><label for="rh-fund" class="form-label">{{ 'ACQ_RECEIPT.FUND' | translate }}</label>
                <select id="rh-fund" class="input" [(ngModel)]="h.fundId" (ngModelChange)="dirty = true"><option [ngValue]="null">{{ 'ACQ_RECEIPT.CHOOSE' | translate }}</option>
                  @for (o of options().funds; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
              <div><label for="rh-bud" class="form-label">{{ 'ACQ.BUDGET' | translate }}</label>
                <select id="rh-bud" class="input" [(ngModel)]="h.budgetId" (ngModelChange)="dirty = true"><option [ngValue]="null">{{ 'ACQ_RECEIPT.CHOOSE' | translate }}</option>
                  @for (o of options().budgets; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
              <div><label for="rh-sup" class="form-label">{{ 'ACQ.SUPPLIER' | translate }}</label>
                <select id="rh-sup" class="input" [(ngModel)]="h.supplierId" (ngModelChange)="dirty = true"><option [ngValue]="null">{{ 'ACQ_RECEIPT.CHOOSE' | translate }}</option>
                  @for (o of options().suppliers; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
              <div><label for="rh-store" class="form-label">{{ 'ACQ_RECEIPT.STORE' | translate }}</label>
                <select id="rh-store" class="input" [(ngModel)]="h.storeId" (ngModelChange)="dirty = true"><option [ngValue]="null">{{ 'ACQ_RECEIPT.CHOOSE' | translate }}</option>
                  @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
            </fieldset>
            <fieldset class="contents" [disabled]="!session.can('AB_RECEIPTS:edit')">
              <div><label for="rh-pm" class="form-label">{{ 'ACQ.PAYMENT_METHOD' | translate }}</label>
                <select id="rh-pm" class="input" [(ngModel)]="h.paymentMethod" (ngModelChange)="dirty = true">@for (m of paymentMethods; track m.code) { <option [ngValue]="m.code">{{ m.name | translate }}</option> }</select></div>
              <div><label for="rh-ps" class="form-label">{{ 'ACQ.PAYMENT_STATUS' | translate }}</label>
                <select id="rh-ps" class="input" [(ngModel)]="h.paymentStatus" (ngModelChange)="dirty = true">@for (s of paymentStatuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }</select></div>
            </fieldset>
            <fieldset class="contents" [disabled]="!editable()">
              <div class="sm:col-span-2"><label for="rh-note" class="form-label">{{ 'COMMON.NOTE' | translate }}</label>
                <input id="rh-note" class="input" maxlength="500" [(ngModel)]="h.note" (ngModelChange)="dirty = true" /></div>
            </fieldset>
            <div class="sm:col-span-2 flex items-end text-sm text-gray-600 pb-2">{{ 'ACQ.TOTAL' | translate }}: <b class="ml-1">{{ r.total | lnumber: '1.0-0' }}</b>
              <span class="ml-3 text-gray-400">{{ 'ACQ.LINES_N' | translate: { count: r.lineCount } }}</span></div>
          </div>
        </div>
      }
    }

    @if (adding(); as a) {
      <app-modal [title]="addTitle(a.mode) | translate" [widthClass]="a.mode === 'lookup' || a.mode === 'order' || (a.mode === 'new' && !a.bib) ? 'max-w-6xl' : 'max-w-3xl'" (closed)="closeAdding()">
        @if (a.bib; as b) {
          <!-- Bước 2 (ELIB-LRC): số lượng, đơn giá, loại tiền, tỷ giá rồi "Thêm mới". -->
          <div class="rounded-lg bg-blue-50 border border-blue-100 px-3 py-2 mb-4">
            <div class="font-medium text-gray-800">{{ b.title }}</div>
            <div class="text-[12px] text-gray-500">MFN {{ b.mfn }}@if (b.author) { · {{ b.author }} }@if (b.publishYear) { · {{ b.publishYear }} }</div>
            @if (lineOf(b); as existing) { <div class="text-[12px] text-amber-700 mt-1">{{ 'ACQ_RECEIPT.WILL_OVERWRITE' | translate: { qty: existing.quantity } }}</div> }
          </div>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
            <ng-container *ngTemplateOutlet="lineFields; context: { $implicit: a }" />
          </div>
        } @else {
          @switch (a.mode) {
            @case ('lookup') { <app-acq-bib-lookup (picked)="a.bib = $event" /> }
            @case ('new') {
              <!-- Thêm sách lẻ (ELIB-LRC): biên mục MARC + thông tin dòng trên cùng một màn, "Thêm mới" tạo biểu ghi rồi thêm dòng. -->
              <app-acq-bib-marc-new (created)="a.bib = $event; addLine(a)">
                <ng-container *ngTemplateOutlet="lineFields; context: { $implicit: a }" />
              </app-acq-bib-marc-new>
            }
            @case ('z3950') { <app-acq-bib-source inputId="ra-z" [only]="['z3950']" [selected]="null" (picked)="a.bib = $event" /> }
            @case ('marc') { <app-acq-bib-source inputId="ra-marc" [only]="['file', 'paste']" [selected]="null" (picked)="a.bib = $event" /> }
            @case ('order') {
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
                    <td class="td">{{ o.title }}<div class="text-[11px] text-gray-400">{{ o.orderName }}@if (o.author) { · {{ o.author }} }</div>
                      @if (orderLineExisting(o); as existing) {
                        <div class="text-[11px] text-amber-700">{{ 'ACQ_RECEIPT.WILL_OVERWRITE' | translate: { qty: existing.quantity } }}</div>
                      }</td>
                    <td class="td text-right">{{ o.remaining | lnumber }} / {{ o.quantity | lnumber }}</td>
                    <td class="td">
                      @if (o.mfn) { <span class="font-mono text-[13px]">{{ o.mfn }}</span> }
                      @else { <input type="number" class="input py-1" [placeholder]="'ACQ_RECEIPT.MFN_PH' | translate" [attr.aria-label]="'MFN ' + o.title" [(ngModel)]="pickMfn[o.orderLinePublicId]" /> }
                    </td>
                    <td class="td"><input type="number" min="1" class="input py-1" [attr.aria-label]="'ACQ_RECEIPT.COPIES' | translate"
                      [placeholder]="'' + defaultOrderQty(o)" [(ngModel)]="pickQty[o.orderLinePublicId]" /></td>
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
          }
        }
        <ng-container footer>
          @if (a.bib && a.mode !== 'order') {
            <button type="button" class="btn-secondary mr-auto" (click)="a.bib = null">{{ 'ACQ_RECEIPT.PICK_OTHER' | translate }}</button>
          }
          <button type="button" class="btn-secondary" (click)="closeAdding()">{{ 'COMMON.CANCEL' | translate }}</button>
          @if (a.bib && a.mode !== 'order') {
            <button type="button" class="btn-primary" [disabled]="busy()" (click)="addLine(a)"><span class="material-icons text-[18px]">playlist_add</span> {{ 'ACQ_RECEIPT.ADD_NEW' | translate }}</button>
          } @else if (a.mode === 'new') {
            <button type="button" class="btn-primary" [disabled]="busy() || marcNew()?.busy()" (click)="marcNew()?.submit()">
              <span class="material-icons text-[18px]" [class.animate-spin]="marcNew()?.busy()">{{ marcNew()?.busy() ? 'autorenew' : 'playlist_add' }}</span> {{ 'ACQ_RECEIPT.ADD_NEW' | translate }}</button>
          }
        </ng-container>
      </app-modal>
    }

    <!-- Thông tin dòng dùng chung: bước 2 sau khi chọn biểu ghi và form "Thêm sách lẻ". -->
    <ng-template #lineFields let-a>
      <div><label for="ra-qty" class="form-label">{{ 'ACQ_RECEIPT.COPIES' | translate }} *</label><input id="ra-qty" type="number" min="1" class="input" [(ngModel)]="a.quantity" /></div>
      <div><label for="ra-price" class="form-label">{{ 'ACQ.PRICE' | translate }}</label><input id="ra-price" type="number" min="0" step="any" class="input" [(ngModel)]="a.price" /></div>
      <div><label for="ra-cur" class="form-label">{{ 'ACQ.CURRENCY' | translate }}</label>
        @if (currencies().length) {
          <select id="ra-cur" class="input" [(ngModel)]="a.currency" (ngModelChange)="onCurrency(a)">
            @for (c of currencies(); track c.code) { <option [ngValue]="c.code">{{ c.code }}</option> }
          </select>
        } @else { <input id="ra-cur" class="input uppercase" maxlength="10" [(ngModel)]="a.currency" /> }</div>
      <div><label for="ra-rate" class="form-label">{{ 'ACQ.RATE' | translate }}</label><input id="ra-rate" type="number" min="0" step="any" class="input" [(ngModel)]="a.rate" /></div>
    </ng-template>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative mb-5">
      @if (loading()) { <app-loading /> }
      <div class="px-5 py-3 border-b border-gray-100 flex flex-wrap items-center gap-2">
        <span class="font-semibold text-gray-800 mr-auto">{{ 'ACQ_RECEIPT.LINES_TITLE' | translate }} · {{ 'ACQ.LINES_N' | translate: { count: lines().length } }}</span>
        <!-- Thêm tài liệu: hàng nút như ELIB-LRC. -->
        @if (editable()) {
          <button type="button" class="btn-primary !py-1.5 !px-3" (click)="openAdd('lookup')"><span class="material-icons text-[18px]">manage_search</span> {{ 'ACQ_RECEIPT.ADD_LOOKUP' | translate }}</button>
          @if (session.can('CATALOG_BIBS:add')) {
            <button type="button" class="btn-secondary !py-1.5 !px-3" (click)="openAdd('new')"><span class="material-icons text-[18px]">add_circle</span> {{ 'ACQ_RECEIPT.ADD_SINGLE' | translate }}</button>
            <button type="button" class="btn-secondary !py-1.5 !px-3" (click)="openAdd('z3950')"><span class="material-icons text-[18px]">travel_explore</span> {{ 'ACQ_RECEIPT.ADD_Z3950' | translate }}</button>
            <button type="button" class="btn-secondary !py-1.5 !px-3" (click)="openAdd('marc')"><span class="material-icons text-[18px]">upload_file</span> {{ 'ACQ_RECEIPT.ADD_MARC' | translate }}</button>
          }
          <button type="button" class="btn-secondary !py-1.5 !px-3" (click)="openAdd('order')"><span class="material-icons text-[18px]">shopping_cart</span> {{ 'ACQ_RECEIPT.ADD_FROM_ORDER_BTN' | translate }}</button>
        }
      </div>
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
                      @if (l.registered !== null) {
                        <!-- Đăng ký cá biệt (ELIB-LRC: nút biểu tượng qr_code màu chàm); đủ số bản của dòng thì khoá. -->
                        <button class="icon-btn text-indigo-600 hover:bg-indigo-50 disabled:opacity-30 disabled:cursor-not-allowed" [disabled]="l.registered >= l.quantity"
                                [title]="(l.registered >= l.quantity ? 'ACQ_RECEIPT.REGISTER_FULL' : 'ACQ_RECEIPT.REGISTER') | translate" (click)="openRegister(l)">
                          <span class="material-icons text-[18px]">qr_code</span></button>
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
        <!-- ELIB-LRC: MFN · số lượng theo đơn nhận · còn lại; không đăng ký quá số bản của dòng. -->
        <div class="grid grid-cols-3 gap-3 mb-3 bg-gray-50 rounded-lg border border-gray-200 p-3">
          <div><div class="text-[11px] font-semibold text-gray-500 uppercase">MFN</div><div class="text-sm font-mono">{{ g.line.mfn }}</div></div>
          <div><div class="text-[11px] font-semibold text-gray-500 uppercase">{{ 'ACQ_RECEIPT.COPIES' | translate }}</div><div class="text-sm">{{ g.line.quantity | lnumber }}</div></div>
          <div><div class="text-[11px] font-semibold text-gray-500 uppercase">{{ 'ACQ_RECEIPT.REMAINING' | translate }}</div>
            <div class="text-sm font-semibold" [class]="remainingOf(g.line) > 0 ? 'text-emerald-600' : 'text-red-600'">{{ remainingOf(g.line) | lnumber }}</div></div>
          <div class="col-span-3 text-[12px] text-gray-600">{{ g.line.title }}@if (g.line.author) { — {{ g.line.author }} }</div>
        </div>
        @if (remainingOf(g.line) <= 0) { <div class="alert-info !block mb-3 text-sm">{{ 'ACQ_RECEIPT.REGISTER_FULL' | translate }}</div> }
        <div class="flex gap-2 mb-3">
          <button type="button" [class]="g.mode === 'batch' ? 'btn-primary' : 'btn-secondary'" (click)="g.mode = 'batch'">{{ 'ACQ_RECEIPT.MODE_BATCH' | translate }}</button>
          <button type="button" [class]="g.mode === 'single' ? 'btn-primary' : 'btn-secondary'" (click)="g.mode = 'single'">{{ 'ACQ_RECEIPT.MODE_SINGLE' | translate }}</button>
        </div>
        <div class="grid grid-cols-2 gap-3">
          @if (g.mode === 'batch') {
            <div><label for="rg-pre" class="form-label">{{ 'ACQ_RECEIPT.PREFIX' | translate }}</label><input id="rg-pre" class="input font-mono uppercase" maxlength="20" [(ngModel)]="g.prefix" (change)="previewNext(g)" /></div>
            <div><label for="rg-dig" class="form-label">{{ 'ACQ_RECEIPT.DIGITS' | translate }}</label><input id="rg-dig" type="number" min="1" max="18" class="input" [(ngModel)]="g.digits" (change)="previewNext(g)" /></div>
            <div><label for="rg-qty" class="form-label">{{ 'ACQ_RECEIPT.COPIES' | translate }} *</label>
              <input id="rg-qty" type="number" class="input bg-gray-100 text-gray-600 cursor-not-allowed" readonly [value]="remainingOf(g.line)" />
              <p class="text-[11px] text-gray-400 mt-0.5">{{ 'ACQ_RECEIPT.QTY_LOCKED_NOTE' | translate }}</p></div>
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
          <button type="button" class="btn-primary" [disabled]="busy() || remainingOf(g.line) <= 0" (click)="submitRegister(g)">
            <span class="material-icons text-[18px]">qr_code</span> {{ 'ACQ_RECEIPT.REGISTER' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    <app-unsaved-confirm [state]="leave" />
  `,
})
export class ReceiptDetail implements OnInit, HasUnsavedChanges {
  readonly id = input.required<string>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly paymentStatuses = PAYMENT_STATUSES;
  protected readonly paymentMethods = PAYMENT_METHODS;
  /** Form thông tin đơn (bản nháp — "Lưu lại" mới ghi); dirty = có thay đổi chưa lưu (hỏi lại khi rời trang). */
  protected head: ReceiptForm | null = null;
  protected dirty = false;
  protected readonly leave = new LeaveConfirm();
  protected readonly options = signal<AcquisitionOptions>(EMPTY_OPTIONS);
  /** Hộp thoại thêm tài liệu đang mở (một trong các nút kiểu ELIB-LRC) và bước "thông tin dòng" khi đã có biểu ghi. */
  protected readonly adding = signal<AddLineState | null>(null);
  protected readonly marcNew = viewChild(BibMarcNew);
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
    this.api.acquisitionOptions().then((o) => this.options.set(o), () => undefined);
    void loadCurrencies(this.api).then((c) => this.currencies.set(c));
    await this.load();
  }

  hasUnsavedChanges(): boolean {
    return this.dirty || this.marcDirty();
  }

  /** "Thêm sách lẻ" đang soạn dở biểu ghi MARC (chưa tạo ở catalog). */
  private marcDirty(): boolean {
    const a = this.adding();
    return !!a && a.mode === 'new' && !a.bib && !!this.marcNew()?.isDirty();
  }

  /** Đóng hộp thoại thêm tài liệu; đang soạn dở biểu ghi MARC thì hỏi lại (ELIB-LRC). */
  protected async closeAdding(): Promise<void> {
    if (this.marcDirty() && !(await this.leave.ask())) return;
    this.adding.set(null);
  }

  confirmLeave(): Promise<boolean> {
    return this.leave.ask();
  }

  /**
   * "Lưu lại" (ELIB-LRC): đơn đang lập ghi toàn bộ thông tin đơn; đơn đã hoàn tất (khoá) chỉ ghi thanh toán — chốt thanh toán không cần
   * mở lại đơn.
   */
  protected async saveHead(): Promise<void> {
    const h = this.head;
    const r = this.receipt();
    if (!h || !r) return;
    if (r.completedAt) {
      await this.run(async () => {
        await this.api.setReceiptPayment(this.id(), { paymentStatus: Number(h.paymentStatus), paymentMethod: Number(h.paymentMethod) });
        this.dirty = false;
        this.toastr.success('COMMON.SAVE_SUCCESS');
      });
      return;
    }
    if (!h.name.trim()) {
      this.toastr.warning('ACQ_RECEIPT.NAME_REQUIRED');
      return;
    }
    await this.run(async () => {
      await this.api.crud<Receipt>('receipts', 'acquisition').update(this.id(), {
        receivedOn: h.receivedOn || today(), name: h.name.trim(), sourceId: h.sourceId, supplierId: h.supplierId, budgetId: h.budgetId,
        fundId: h.fundId, storeId: h.storeId, paymentStatus: Number(h.paymentStatus), paymentMethod: Number(h.paymentMethod), note: h.note.trim() || null,
      });
      this.dirty = false;
      this.toastr.success('COMMON.SAVE_SUCCESS');
    });
  }

  protected openAdd(mode: AddMode): void {
    const last = this.adding();
    this.adding.set({ mode, bib: null, quantity: 1, price: 0, currency: last?.currency ?? 'VND', rate: last?.rate ?? 1 });
    if (mode === 'order') this.findReceivable();
  }

  protected addTitle(mode: AddMode): string {
    return ADD_TITLES[mode];
  }

  /** Dòng đã có của biểu ghi này — chọn lại thì ghi đè (như ELIB-LRC). */
  protected lineOf(b: AcqBib): ReceiptLine | null {
    return this.lines().find((l) => l.bibPublicId === b.bibPublicId) ?? null;
  }

  protected statusOf(r: Receipt) {
    return receiptStatus(r);
  }

  /** Đổi tiền tệ → tỷ giá gợi ý theo danh mục tiền tệ của đơn vị (sửa tay được). */
  protected onCurrency(a: AddLineState): void {
    const c = this.currencies().find((x) => x.code === a.currency);
    if (c) a.rate = c.exchangeRate;
  }

  protected status(i: ReceiptItem) {
    return itemStatus(i.status, i.onLoan);
  }

  protected titleOf(i: ReceiptItem): string {
    return this.lines().find((l) => l.publicId === i.linePublicId)?.title ?? `MFN ${i.mfn}`;
  }

  /** "Thêm mới": thêm dòng (biểu ghi đã có trong đơn thì ghi đè dòng đó), đóng hộp thoại. */
  protected async addLine(a: AddLineState): Promise<void> {
    if (!a.bib) return;
    const overwrite = !!this.lineOf(a.bib);
    await this.saveLine({
      mfn: a.bib.mfn, quantity: Number(a.quantity) || 0, price: a.price === null || (a.price as unknown) === '' ? null : Number(a.price),
      currency: a.currency.trim() || null, rate: a.rate === null || (a.rate as unknown) === '' ? 1 : Number(a.rate),
    }, () => {
      this.adding.set(null);
      this.toastr.success(overwrite ? 'ACQ_RECEIPT.LINE_OVERWRITTEN' : 'ACQ_RECEIPT.LINE_ADDED');
    });
  }

  /** Dòng đã có trong phiếu cho biểu ghi của dòng đặt — nhận lại thì ghi đè dòng đó (số bản là tổng mới, không cộng dồn). */
  protected orderLineExisting(o: ReceivableLine): ReceiptLine | null {
    return this.lines().find((l) => (o.bibPublicId && l.bibPublicId === o.bibPublicId) || (o.mfn !== null && l.mfn === o.mfn)) ?? null;
  }

  /** Số bản mặc định: số còn chờ nhận; đã có dòng trong phiếu thì cộng số bản của dòng đó để ghi đè không làm giảm. */
  protected defaultOrderQty(o: ReceivableLine): number {
    return (this.orderLineExisting(o)?.quantity ?? 0) + o.remaining;
  }

  protected async addFromOrder(o: ReceivableLine): Promise<void> {
    const qty = this.pickQty[o.orderLinePublicId];
    await this.saveLine({
      orderLinePublicId: o.orderLinePublicId, mfn: o.mfn ?? this.pickMfn[o.orderLinePublicId] ?? null,
      quantity: Number(qty === null || qty === undefined || (qty as unknown) === '' ? this.defaultOrderQty(o) : qty) || 0,
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
      line: l, mode: 'batch', prefix: '', digits: 6, startNumber: null, barcode: '',
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

  /** Số bản còn được đăng ký của dòng (số lượng theo đơn nhận trừ ĐKCB đã đăng ký). */
  protected remainingOf(l: ReceiptLine): number {
    return Math.max(0, l.quantity - (l.registered ?? 0));
  }

  /** Đăng ký theo lô luôn đúng số còn lại (ELIB-LRC khoá ô số lượng) — không thể vượt số bản của đơn nhận. */
  protected async submitRegister(g: RegisterForm): Promise<void> {
    const remaining = this.remainingOf(g.line);
    if (remaining <= 0) {
      this.toastr.warning('ACQ_RECEIPT.REGISTER_FULL');
      return;
    }
    this.busy.set(true);
    try {
      const done = await this.api.registerReceiptItems(g.line.publicId, g.mode === 'single'
        ? { barcode: g.barcode.trim(), storeId: g.storeId }
        : { quantity: remaining, prefix: g.prefix.trim() || null, digits: Number(g.digits) || 6, startNumber: g.startNumber || null, storeId: g.storeId });
      this.registered.set(done);
      this.toastr.success('ACQ_RECEIPT.REGISTERED_N', { count: done.length });
      await this.load();
      const line = this.lines().find((l) => l.publicId === g.line.publicId);
      if (line) {
        g.line = line;
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
      // Giữ bản nháp đang sửa dở khi tải lại sau thêm dòng / đăng ký ĐKCB.
      if (!this.dirty) this.head = toForm(receipt);
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
