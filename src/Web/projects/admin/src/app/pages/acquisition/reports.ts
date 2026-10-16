import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  AcqAccessionRow, AcqAllocation, AcqListPage, AcqLookupRow, AcqNewBookRow, AcqReportName, AcqReportRequest, AcquisitionOptions, Api, CrudPage, StoreOption,
  errorMessage, saveFile,
} from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { LABEL_BARCODES_KEY } from '../holdings/labels';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { ACQ_PRINT_KEY, ACQ_PRINT_MAX_ROWS, AcqPrintJob } from './report-print';

type Tab = 'list' | 'lookup' | 'allocation' | 'register' | 'newbooks';

const TABS: { tab: Tab; label: string; report: AcqReportName; file: string }[] = [
  { tab: 'list', label: 'ACQ_REPORT.TAB_LIST', report: 'AcquisitionList', file: 'danh-sach-bo-sung.xlsx' },
  { tab: 'lookup', label: 'ACQ_REPORT.TAB_LOOKUP', report: 'LookupLines', file: 'tra-cuu-don-nhan.xlsx' },
  { tab: 'allocation', label: 'ACQ_REPORT.TAB_ALLOCATION', report: 'StoreAllocation', file: 'phan-bo-kho.xlsx' },
  { tab: 'register', label: 'ACQ_REPORT.TAB_REGISTER', report: 'AccessionRegister', file: 'so-dang-ky-ca-biet.xlsx' },
  { tab: 'newbooks', label: 'ACQ_REPORT.TAB_NEWBOOKS', report: 'NewBooks', file: 'thu-muc-sach-moi.xlsx' },
];

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
  imports: [FormsModule, LDatePipe, LNumberPipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'ACQ_REPORT.TITLE' | translate }}</h4></div>
    <div class="flex flex-wrap gap-2 mb-4">
      @for (t of tabs; track t.tab) {
        <button type="button" [class]="tab() === t.tab ? 'btn-primary' : 'btn-secondary'" (click)="setTab(t.tab)">{{ t.label | translate }}</button>
      }
    </div>

    <div class="panel grid grid-cols-2 md:grid-cols-4 gap-3 items-end">
      <div><label for="rp-f" class="field-label">{{ (tab() === 'newbooks' ? 'ACQ_REPORT.CATALOGED_FROM' : 'STORE_STATS.RECEIVED_FROM') | translate }}</label>
        <input id="rp-f" type="date" class="input" [(ngModel)]="request.from" /></div>
      <div><label for="rp-t" class="field-label">{{ (tab() === 'newbooks' ? 'ACQ_REPORT.CATALOGED_TO' : 'STORE_STATS.RECEIVED_TO') | translate }}</label>
        <input id="rp-t" type="date" class="input" [(ngModel)]="request.to" /></div>
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
        <div><label for="rp-crf" class="field-label">{{ 'ACQ_RECEIPT.CREATED_FROM' | translate }}</label><input id="rp-crf" type="date" class="input" [(ngModel)]="request.createdFrom" /></div>
        <div><label for="rp-crt" class="field-label">{{ 'ACQ_RECEIPT.CREATED_TO' | translate }}</label><input id="rp-crt" type="date" class="input" [(ngModel)]="request.createdTo" /></div>
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
  private readonly router = inject(Router);
  protected readonly session = inject(Session);
  private readonly i18n = inject(TranslateService);

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
    const t = TABS.find((x) => x.tab === this.tab())!;
    this.busy.set(true);
    try {
      saveFile(await this.api.exportAcqReport(t.report, this.body()), t.file);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Lấy hết các trang của báo cáo đang xem (trần ACQ_PRINT_MAX_ROWS dòng) rồi mở trang in theo mẫu giấy. */
  protected async print(): Promise<void> {
    const t = TABS.find((x) => x.tab === this.tab())!;
    this.busy.set(true);
    try {
      const job = await this.buildPrintJob(t);
      if (!job.rows.length) {
        this.toastr.warning('ACQ_REPORT.NOTHING_TO_PRINT');
        return;
      }
      try {
        sessionStorage.setItem(ACQ_PRINT_KEY, JSON.stringify(job));
      } catch {
        this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
        return;
      }
      void this.router.navigateByUrl('/print/acq-report');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** In nhãn cho các ĐKCB của sổ đăng ký cá biệt đang lọc (tối đa ACQ_PRINT_MAX_ROWS dòng). */
  protected async printLabels(): Promise<void> {
    this.busy.set(true);
    try {
      const all = await this.allPages<AcqAccessionRow>('AccessionRegister', (p) => p.totalCount ?? 0);
      if (all.truncated) this.toastr.warning('ACQ_REPORT.LABELS_TRUNCATED', { max: ACQ_PRINT_MAX_ROWS });
      const barcodes = all.rows.map((x) => x.barcode);
      if (!barcodes.length) {
        this.toastr.warning('ACQ_REPORT.NOTHING_TO_PRINT');
        return;
      }
      try {
        sessionStorage.setItem(LABEL_BARCODES_KEY, JSON.stringify(barcodes));
      } catch {
        this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
        return;
      }
      void this.router.navigateByUrl('/print-labels');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async allPages<R>(name: AcqReportName, total: (p: { items: R[]; total?: number; totalCount?: number }) => number): Promise<{ rows: R[]; truncated: boolean; first: { items: R[] } }> {
    const size = 500;
    const rows: R[] = [];
    let first: { items: R[] } | null = null;
    for (let page = 1; ; page++) {
      const p = await this.api.acqReport<{ items: R[]; total?: number; totalCount?: number }>(name, { ...this.body(), pageIndex: page, pageSize: size });
      first ??= p;
      rows.push(...p.items);
      if (!p.items.length || rows.length >= total(p) || rows.length >= ACQ_PRINT_MAX_ROWS) break;
    }
    return { rows: rows.slice(0, ACQ_PRINT_MAX_ROWS), truncated: total(first!) > ACQ_PRINT_MAX_ROWS, first: first! };
  }

  private async buildPrintJob(t: { tab: Tab; label: string; report: AcqReportName }): Promise<AcqPrintJob> {
    const tr = (k: string) => this.i18n.instant(k) as string;
    const num = (v: number, digits = 0) => v.toLocaleString('vi-VN', { maximumFractionDigits: digits });
    const date = (v: string | null) => (v ? v.split('-').reverse().join('/') : '');
    const r = this.request;
    const range = (a: string, b: string) => (a && b ? `Từ ngày ${date(a)} đến ngày ${date(b)}` : a ? `Từ ngày ${date(a)}` : b ? `Đến ngày ${date(b)}` : '');
    const base = { title: tr(t.label), subtitle: range(r.from ?? '', r.to ?? ''), total: null as string[] | null, truncated: false };
    switch (t.tab) {
      case 'list': {
        const all = await this.allPages<AcqListPage['items'][number]>(t.report, (p) => p.total ?? 0);
        const page = all.first as unknown as AcqListPage;
        return {
          ...base, truncated: all.truncated,
          headers: [tr('ACQ_REPORT.RECEIPT'), tr('ACQ_RECEIPT.RECEIVED_ON'), tr('ITEMS_PAGE.TITLE_COL'), tr('ACQ.QUANTITY'), tr('ACQ.PRICE'), tr('ACQ.AMOUNT')],
          rows: all.rows.map((x) => ['#' + x.receiptId, date(x.receivedOn), `${x.title} (MFN ${x.mfn})`, num(x.quantity), x.price === null ? '' : num(x.price, 2) + ' ' + (x.currency ?? ''), num(x.total)]),
          total: [tr('STORE_STATS.TOTAL'), '', '', num(page.quantity), '', num(page.amount)], right: [3, 4, 5],
        };
      }
      case 'lookup': {
        const all = await this.allPages<AcqLookupRow>(t.report, (p) => p.totalCount ?? 0);
        return {
          ...base, truncated: all.truncated, right: [],
          headers: ['MFN', tr('ACQ_REPORT.RECEIPT'), tr('ACQ_REPORT.DOCUMENT'), tr('ACQ_RECEIPT.RECEIVED_ON'), tr('ACQ.SOURCE'), tr('ACQ_RECEIPT.STORE')],
          rows: all.rows.map((x) => [String(x.mfn), '#' + x.receiptId, x.isbd, date(x.receivedOn), x.sourceName ?? '', this.storeName(x)]),
        };
      }
      case 'allocation': {
        const a = await this.api.acqReport<AcqAllocation>(t.report, this.body());
        return {
          ...base, truncated: false, right: [1], headers: [tr('ITEMS_PAGE.STORE'), tr('ACQ_REPORT.COPIES')],
          rows: a.items.map((x) => [x.storeName ?? tr('ACQ_REPORT.NO_STORE'), num(x.copies)]), total: [tr('STORE_STATS.TOTAL'), num(a.total)],
        };
      }
      case 'register': {
        const all = await this.allPages<AcqAccessionRow>(t.report, (p) => p.totalCount ?? 0);
        return {
          ...base, truncated: all.truncated, right: [5],
          headers: [tr('ITEMS_PAGE.BARCODE'), tr('ITEMS_PAGE.TITLE_COL'), tr('ITEMS_PAGE.AUTHOR'), tr('ITEMS_PAGE.STORE'), tr('ACQ_RECEIPT.RECEIVED_ON'), tr('ACQ.PRICE')],
          rows: all.rows.map((x) => [x.barcode, `${x.title ?? ''} (MFN ${x.mfn})`, x.author ?? '', x.storeName ?? '', date(x.date), x.price === null ? '' : num(x.price, 2) + ' ' + (x.currency ?? '')]),
        };
      }
      case 'newbooks': {
        const all = await this.allPages<AcqNewBookRow>(t.report, (p) => p.totalCount ?? 0);
        return {
          ...base, truncated: all.truncated, right: [],
          headers: ['MFN', tr('ITEMS_PAGE.TITLE_COL'), tr('ITEMS_PAGE.AUTHOR'), tr('ACQ.PUBLISHER'), tr('ACQ.YEAR'), tr('ACQ_REPORT.CATALOGED_ON')],
          rows: all.rows.map((x) => [String(x.mfn), x.title, x.author ?? '', x.publisher ?? '', x.publishYear ?? '', date(x.catalogedOn)]),
        };
      }
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
