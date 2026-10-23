import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import {
  AcqAccessionRow, AcqAllocation, AcqListPage, AcqLookupRow, AcqNewBookRow, AcqReportRequest, AcquisitionOptions, Api, CrudPage, StoreOption, errorMessage,
} from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { ACQ_REPORTS, AcqReportActions, AcqReportKind } from './acq-report-actions';
import { DateInput } from '../../shared/date-input';

type Tab = AcqReportKind;

const TABS = ACQ_REPORTS.map((r) => ({ tab: r.kind, label: r.label }));

const EMPTY_OPTIONS: AcquisitionOptions = { sources: [], suppliers: [], budgets: [], funds: [] };

const emptyRequest = (): AcqReportRequest => ({
  from: '', to: '', storeId: null, supplierId: null, sourceId: null, fundId: null, receiptCodeFrom: null, receiptCodeTo: null, receiptName: '', completed: null, createdFrom: '', createdTo: '',
  mfnFrom: null, mfnTo: null, title: '', author: '', publisher: '', publishYear: '', pageIndex: 1, pageSize: 20,
});

/**
 * Báo cáo bổ sung + tra cứu đơn nhận theo dòng (monolith: /admin/ab-receipts-lookup và AcquisitionReport, quyền AB_RECEIPTS xem):
 * danh sách tài liệu bổ sung, tra cứu đơn nhận theo dòng, phân bổ kho, sổ ĐKCB, thư mục sách mới — mỗi báo cáo xuất được Excel. Khoảng ngày
 * lọc theo ngày nhận của phiếu (thư mục sách mới: ngày biên mục).
 */
@Component({
  selector: 'app-acq-reports',
  imports: [DateInput, FormsModule, LDatePipe, LNumberPipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'ACQ_REPORT.TITLE' | translate }}</h4></div>
    <div class="flex flex-wrap gap-2 mb-4">
      @for (t of tabs; track t.tab) {
        <button type="button" [class]="tab() === t.tab ? 'btn-primary' : 'btn-secondary'" (click)="setTab(t.tab)">{{ t.label | translate }}</button>
      }
    </div>

    <div class="panel grid grid-cols-2 md:grid-cols-4 gap-3 items-end">
      <div><label for="rp-f" class="field-label">{{ (tab() === 'newbooks' ? 'ACQ_REPORT.CATALOGED_FROM' : 'STORE_STATS.RECEIVED_FROM') | translate }}</label>
        <app-date-input fieldId="rp-f" [(ngModel)]="request.from"></app-date-input></div>
      <div><label for="rp-t" class="field-label">{{ (tab() === 'newbooks' ? 'ACQ_REPORT.CATALOGED_TO' : 'STORE_STATS.RECEIVED_TO') | translate }}</label>
        <app-date-input fieldId="rp-t" [(ngModel)]="request.to"></app-date-input></div>
      @if (tab() !== 'newbooks') {
        <div><label for="rp-s" class="field-label">{{ (tab() === 'allocation' || tab() === 'register' ? 'ITEMS_PAGE.STORE' : 'ACQ_RECEIPT.STORE') | translate }}</label>
          <select id="rp-s" class="input" [(ngModel)]="request.storeId"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
        <div><label for="rp-sup" class="field-label">{{ 'ACQ.SUPPLIER' | translate }}</label>
          <select id="rp-sup" class="input" [(ngModel)]="request.supplierId"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (o of options().suppliers; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
        <div><label for="rp-src" class="field-label">{{ 'ACQ.SOURCE' | translate }}</label>
          <select id="rp-src" class="input" [(ngModel)]="request.sourceId"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (o of options().sources; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
        <div><label for="rp-fund" class="field-label">{{ 'ACQ.FUND' | translate }}</label>
          <select id="rp-fund" class="input" [(ngModel)]="request.fundId"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (o of options().funds; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
        <div><label for="rp-crf" class="field-label">{{ 'ACQ_RECEIPT.CREATED_FROM' | translate }}</label><app-date-input fieldId="rp-crf" [(ngModel)]="request.createdFrom"></app-date-input></div>
        <div><label for="rp-crt" class="field-label">{{ 'ACQ_RECEIPT.CREATED_TO' | translate }}</label><app-date-input fieldId="rp-crt" [(ngModel)]="request.createdTo"></app-date-input></div>
        <div><label for="rp-cf" class="field-label">{{ 'ACQ_REPORT.CODE_FROM' | translate }}</label><input id="rp-cf" type="number" min="1" class="input" [(ngModel)]="request.receiptCodeFrom" /></div>
        <div><label for="rp-ct" class="field-label">{{ 'ACQ_REPORT.CODE_TO' | translate }}</label><input id="rp-ct" type="number" min="1" class="input" [(ngModel)]="request.receiptCodeTo" /></div>
        <div><label for="rp-st" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="rp-st" class="input" [(ngModel)]="request.completed"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option [ngValue]="false">{{ 'ACQ_RECEIPT.DRAFT' | translate }}</option><option [ngValue]="true">{{ 'ACQ_RECEIPT.COMPLETED' | translate }}</option></select></div>
        <div><label for="rp-ti" class="field-label">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</label><input id="rp-ti" class="input" [(ngModel)]="request.title" (keydown.enter)="find()" /></div>
        <div><label for="rp-au" class="field-label">{{ 'ITEMS_PAGE.AUTHOR' | translate }}</label><input id="rp-au" class="input" [(ngModel)]="request.author" (keydown.enter)="find()" /></div>
        <div><label for="rp-mf" class="field-label">{{ 'ACQ_REPORT.MFN_FROM' | translate }}</label><input id="rp-mf" type="number" min="1" class="input" [(ngModel)]="request.mfnFrom" /></div>
        <div><label for="rp-mt" class="field-label">{{ 'ACQ_REPORT.MFN_TO' | translate }}</label><input id="rp-mt" type="number" min="1" class="input" [(ngModel)]="request.mfnTo" /></div>
      }
      <div class="col-span-2 md:col-span-4 flex gap-2 justify-end">
        @if (tab() === 'register' && (session.can('PRINT_LABEL:view') || session.can('CATALOG_BIBS:view'))) {
          <button class="btn-secondary" [disabled]="busy()" (click)="printLabels()"><span class="material-icons text-[18px]">label</span> {{ 'ACQ_RECEIPT.PRINT_LABELS' | translate }}</button>
        }
        @if (session.can('PRINT_LABEL:view') || session.can('CATALOG_BIBS:view')) {
          <button class="btn-secondary" [disabled]="busy()" (click)="printClassLabels()"><span class="material-icons text-[18px]">label</span> {{ 'ACQ_LOOKUP.RT_LABELS_QTY' | translate }}</button>
        }
        <button class="btn-secondary" [disabled]="busy()" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'ACQ_REPORT.PRINT' | translate }}</button>
        <button class="btn-secondary" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button class="btn-primary" [disabled]="busy()" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
      <p class="col-span-2 md:col-span-4 text-[11px] text-gray-400 -mt-1">{{ (tab() === 'newbooks' ? 'ACQ_REPORT.NEWBOOKS_HINT' : 'ACQ_REPORT.DATE_HINT') | translate }}</p>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        @switch (tab()) {
          @case ('list') {
            <table class="w-full text-left border-collapse min-w-[900px]">
              <thead><tr>
                <th class="th w-20">{{ 'ACQ.NO' | translate }}</th><th class="th w-28">{{ 'ACQ_RECEIPT.RECEIVED_ON' | translate }}</th>
                <th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th><th class="th w-24 !text-right">{{ 'ACQ.QUANTITY' | translate }}</th>
                <th class="th w-36 !text-right">{{ 'ACQ.PRICE' | translate }}</th><th class="th w-36 !text-right">{{ 'ACQ.AMOUNT' | translate }}</th>
              </tr></thead>
              <tbody>
                @for (r of list()?.items ?? []; track $index) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td font-mono text-[13px]">#{{ r.receiptId }}</td><td class="td">{{ r.receivedOn | ldate: 'dd/MM/yyyy' }}</td>
                    <td class="td">{{ r.title }}<div class="text-[11px] text-gray-400">MFN {{ r.mfn }}@if (r.author) { · {{ r.author }} }</div></td>
                    <td class="td text-right">{{ r.quantity | lnumber }}</td>
                    <td class="td text-right">@if (r.price !== null) { {{ r.price | lnumber: '1.0-2' }} <span class="text-[11px] text-gray-400">{{ r.currency }}</span> }
                      @if (r.rate !== 1) { <div class="text-[11px] text-gray-400">× {{ r.rate | lnumber: '1.0-4' }}</div> }</td>
                    <td class="td text-right">{{ r.total | lnumber: '1.0-0' }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
                }
                @if (list(); as l) {
                  <tr class="bg-gray-50 font-semibold"><td class="td" colspan="3">{{ 'ACQ_REPORT.TOTAL_ALL' | translate: { count: l.total } }}</td>
                    <td class="td text-right">{{ l.quantity | lnumber }}</td><td class="td"></td><td class="td text-right">{{ l.amount | lnumber: '1.0-0' }}</td></tr>
                }
              </tbody>
            </table>
            @if (list(); as l) { <app-paginator [total]="l.total" [pageIndex]="request.pageIndex" [pageSize]="request.pageSize" (pageChange)="onPage($event)" /> }
          }
          @case ('lookup') {
            <table class="w-full text-left border-collapse min-w-[900px]">
              <thead><tr>
                <th class="th w-24">MFN</th><th class="th w-24">{{ 'ACQ_REPORT.RECEIPT' | translate }}</th><th class="th">{{ 'ACQ_REPORT.DOCUMENT' | translate }}</th>
                <th class="th w-28">{{ 'ACQ_RECEIPT.RECEIVED_ON' | translate }}</th><th class="th w-36">{{ 'ACQ.SOURCE' | translate }}</th><th class="th w-40">{{ 'ACQ_RECEIPT.STORE' | translate }}</th>
              </tr></thead>
              <tbody>
                @for (r of lookup()?.items ?? []; track $index) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td font-mono text-[13px]">{{ r.mfn }}</td><td class="td font-mono text-[13px]">#{{ r.receiptId }}</td><td class="td">{{ r.isbd }}</td>
                    <td class="td">{{ r.receivedOn | ldate: 'dd/MM/yyyy' }}</td><td class="td">{{ r.sourceName ?? '' }}</td><td class="td">{{ storeName(r) }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
                }
              </tbody>
            </table>
            @if (lookup(); as l) { <app-paginator [total]="l.totalCount" [pageIndex]="request.pageIndex" [pageSize]="request.pageSize" (pageChange)="onPage($event)" /> }
          }
          @case ('allocation') {
            <table class="w-full text-left border-collapse">
              <thead><tr><th class="th">{{ 'ITEMS_PAGE.STORE' | translate }}</th><th class="th w-40 !text-right">{{ 'ACQ_REPORT.COPIES' | translate }}</th></tr></thead>
              <tbody>
                @for (r of allocation()?.items ?? []; track $index) {
                  <tr class="hover:bg-gray-50/50"><td class="td">{{ r.storeName ?? ('ACQ_REPORT.NO_STORE' | translate) }}</td><td class="td text-right">{{ r.copies | lnumber }}</td></tr>
                } @empty {
                  <tr><td colspan="2" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
                }
                @if (allocation(); as a) {
                  <tr class="bg-gray-50 font-semibold"><td class="td">{{ 'STORE_STATS.TOTAL' | translate }}</td><td class="td text-right">{{ a.total | lnumber }}</td></tr>
                }
              </tbody>
            </table>
          }
          @case ('register') {
            <table class="w-full text-left border-collapse min-w-[900px]">
              <thead><tr>
                <th class="th w-32">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th><th class="th w-40">{{ 'ITEMS_PAGE.AUTHOR' | translate }}</th>
                <th class="th w-36">{{ 'ITEMS_PAGE.STORE' | translate }}</th><th class="th w-32">{{ 'ACQ_RECEIPT.RECEIVED_ON' | translate }}</th><th class="th w-32 !text-right">{{ 'ACQ.PRICE' | translate }}</th>
              </tr></thead>
              <tbody>
                @for (r of register()?.items ?? []; track r.barcode) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td font-mono text-[13px]">{{ r.barcode }}</td>
                    <td class="td">{{ r.title ?? '—' }}<div class="text-[11px] text-gray-400">MFN {{ r.mfn }}@if (r.publisher) { · {{ r.publisher }} }@if (r.publishYear) { , {{ r.publishYear }} }</div></td>
                    <td class="td">{{ r.author ?? '' }}</td><td class="td">{{ r.storeName ?? '—' }}</td>
                    <td class="td">{{ r.date | ldate: 'dd/MM/yyyy' }}@if (r.receiptId) { <div class="text-[11px] text-gray-400">{{ 'STORE_BOOK_REPORT.RECEIPT_NO' | translate: { no: r.receiptId } }}</div> }</td>
                    <td class="td text-right">@if (r.price !== null) { {{ r.price | lnumber: '1.0-2' }} <span class="text-[11px] text-gray-400">{{ r.currency }}</span> }</td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
                }
              </tbody>
            </table>
            @if (register(); as g) { <app-paginator [total]="g.totalCount" [pageIndex]="request.pageIndex" [pageSize]="request.pageSize" (pageChange)="onPage($event)" /> }
          }
          @case ('newbooks') {
            <table class="w-full text-left border-collapse min-w-[760px]">
              <thead><tr>
                <th class="th w-24">MFN</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th><th class="th w-44">{{ 'ITEMS_PAGE.AUTHOR' | translate }}</th>
                <th class="th w-44">{{ 'ACQ.PUBLISHER' | translate }}</th><th class="th w-24">{{ 'ACQ.YEAR' | translate }}</th><th class="th w-32">{{ 'ACQ_REPORT.CATALOGED_ON' | translate }}</th>
              </tr></thead>
              <tbody>
                @for (r of newBooks()?.items ?? []; track r.mfn) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td font-mono text-[13px]">{{ r.mfn }}</td><td class="td">{{ r.title }}</td><td class="td">{{ r.author ?? '' }}</td>
                    <td class="td">{{ r.publisher ?? '' }}</td><td class="td">{{ r.publishYear ?? '' }}</td><td class="td">{{ r.catalogedOn ? (r.catalogedOn | ldate: 'dd/MM/yyyy') : '—' }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
                }
              </tbody>
            </table>
            @if (newBooks(); as n) { <app-paginator [total]="n.totalCount" [pageIndex]="request.pageIndex" [pageSize]="request.pageSize" (pageChange)="onPage($event)" /> }
          }
        }
      </div>
    </div>
  `,
})
export class AcquisitionReports implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly actions = inject(AcqReportActions);
  protected readonly session = inject(Session);

  protected readonly tabs = TABS;
  protected readonly tab = signal<Tab>('list');
  protected request: AcqReportRequest = emptyRequest();
  protected readonly options = signal<AcquisitionOptions>(EMPTY_OPTIONS);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly list = signal<AcqListPage | null>(null);
  protected readonly lookup = signal<CrudPage<AcqLookupRow> | null>(null);
  protected readonly allocation = signal<AcqAllocation | null>(null);
  protected readonly register = signal<CrudPage<AcqAccessionRow> | null>(null);
  protected readonly newBooks = signal<CrudPage<AcqNewBookRow> | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.run();
    this.api.acquisitionOptions().then((o) => this.options.set(o), () => undefined);
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected setTab(tab: Tab): void {
    this.tab.set(tab);
    this.request.pageIndex = 1;
    void this.run();
  }

  protected find(): void {
    this.request.pageIndex = 1;
    void this.run();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.request.pageIndex = e.pageIndex;
    this.request.pageSize = e.pageSize;
    void this.run();
  }

  /** Tên kho của dòng tra cứu (phiếu nhận chỉ giữ id kho — ưu tiên tên service trả, không có thì tra theo danh sách kho). */
  protected storeName(r: AcqLookupRow): string {
    if (r.storeName) return r.storeName;
    const s = r.storeId === null ? null : this.stores().find((x) => x.id === r.storeId);
    return s ? `${s.code} — ${s.name}` : '';
  }

  protected async run(): Promise<void> {
    const body = this.body();
    this.busy.set(true);
    try {
      switch (this.tab()) {
        case 'list': this.list.set(await this.api.acqReport<AcqListPage>('AcquisitionList', body)); break;
        case 'lookup': this.lookup.set(await this.api.acqReport<CrudPage<AcqLookupRow>>('LookupLines', body)); break;
        case 'allocation': this.allocation.set(await this.api.acqReport<AcqAllocation>('StoreAllocation', body)); break;
        case 'register': this.register.set(await this.api.acqReport<CrudPage<AcqAccessionRow>>('AccessionRegister', body)); break;
        case 'newbooks': this.newBooks.set(await this.api.acqReport<CrudPage<AcqNewBookRow>>('NewBooks', body)); break;
      }
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async export(): Promise<void> {
    await this.act(() => this.actions.export(this.tab(), this.body()));
  }

  /** Lấy hết các trang của báo cáo đang xem (trần ACQ_PRINT_MAX_ROWS dòng) rồi mở trang in theo mẫu giấy. */
  protected async print(): Promise<void> {
    await this.act(() => this.actions.print(this.tab(), this.body(), (r) => this.storeName(r)));
  }

  /** In nhãn cho các ĐKCB của sổ đăng ký cá biệt đang lọc (tối đa ACQ_PRINT_MAX_ROWS dòng). */
  protected async printLabels(): Promise<void> {
    await this.act(() => this.actions.printLabels(this.body()));
  }

  /** In nhãn môn loại theo số lượng mỗi biểu ghi khớp bộ lọc — dùng khi chưa đăng ký ĐKCB cụ thể (chưa có mã vạch). */
  protected async printClassLabels(): Promise<void> {
    await this.act(() => this.actions.printClassLabels(this.body()));
  }

  private async act(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
  private body(): AcqReportRequest {
    const text = (v: string | null) => v?.trim() || null;
    const r = this.request;
    return {
      ...r, from: r.from || null, to: r.to || null, createdFrom: r.createdFrom || null, createdTo: r.createdTo || null, receiptName: text(r.receiptName), title: text(r.title), author: text(r.author),
      publisher: text(r.publisher), publishYear: text(r.publishYear),
    };
  }
}
