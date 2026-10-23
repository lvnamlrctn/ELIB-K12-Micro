import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, DocumentStats, DocumentStatsRequest, StoreBookReport, StoreBookReportRequest, StoreOption, errorMessage, saveFile } from '../../core/api';
import { LDatePipe, Language } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { ACQ_PRINT_MAX_ROWS, openReportPrint, periodLine } from '../acquisition/report-print';
import { itemStatus } from './items';
import { DateInput } from '../../shared/date-input';

const CRITERIA: { value: string; label: string }[] = [
  { value: 'bibtype', label: 'STORE_STATS.BY_TYPE' },
  { value: 'ddc', label: 'STORE_STATS.BY_DDC' },
  { value: 'store', label: 'STORE_STATS.BY_STORE' },
  { value: 'status', label: 'STORE_STATS.BY_STATUS' },
];

/** Thống kê tài liệu kho (monolith: /admin/statistics-document, quyền STAT_DOC) theo loại tài liệu, lớp DDC, kho, trạng thái. */
@Component({
  selector: 'app-store-stats',
  imports: [DateInput, FormsModule, Loading, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'STORE_STATS.TITLE' | translate }}</h4></div>
    <div class="panel grid grid-cols-1 md:grid-cols-5 gap-3 items-end">
      <div>
        <label for="st-c" class="field-label">{{ 'STORE_STATS.CRITERION' | translate }}</label>
        <select id="st-c" class="input" [(ngModel)]="request.criterion" (change)="run()">
          @for (c of criteria; track c.value) { <option [ngValue]="c.value">{{ c.label | translate }}</option> }
        </select>
      </div>
      <div>
        <label for="st-s" class="field-label">{{ 'ITEMS_PAGE.STORE' | translate }}</label>
        <select id="st-s" class="input" [(ngModel)]="request.storeId" (change)="run()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
        </select>
      </div>
      <div><label for="st-f" class="field-label">{{ 'STORE_STATS.RECEIVED_FROM' | translate }}</label><app-date-input fieldId="st-f" [(ngModel)]="request.from"></app-date-input></div>
      <div><label for="st-t" class="field-label">{{ 'STORE_STATS.RECEIVED_TO' | translate }}</label><app-date-input fieldId="st-t" [(ngModel)]="request.to"></app-date-input></div>
      <div class="flex gap-2 justify-end">
        <button class="btn-secondary" [disabled]="busy() || !stats()" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'REPORT_PRINT.PRINT' | translate }}</button>
        <button class="btn-secondary" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button class="btn-primary" [disabled]="busy()" (click)="run()"><span class="material-icons text-[18px]">query_stats</span></button>
      </div>
      <p class="md:col-span-4 text-[11px] text-gray-400 -mt-1">{{ 'STORE_STATS.DATE_HINT' | translate }}</p>
    </div>
    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      <table class="w-full text-left border-collapse">
        <thead><tr>
          <th class="th">{{ 'STORE_STATS.CRITERION' | translate }}</th><th class="th w-40 !text-right">{{ 'STORE_STATS.TITLES' | translate }}</th>
          <th class="th w-40 !text-right">{{ 'STORE_STATS.COPIES' | translate }}</th><th class="th w-64"></th>
        </tr></thead>
        <tbody>
          @if (stats(); as s) {
            @for (r of s.rows; track r.key) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ s.criterion === 'status' ? (status(r.key) | translate) : r.label }}</td>
                <td class="td text-right">{{ r.titles }}</td><td class="td text-right">{{ r.copies }}</td>
                <td class="td"><div class="h-2 rounded bg-blue-500/80" [style.width.%]="s.copies ? (r.copies * 100) / s.copies : 0"></div></td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="py-8 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr>
            }
            <tr class="bg-gray-50 font-semibold"><td class="td">{{ 'STORE_STATS.TOTAL' | translate }}</td><td class="td text-right">{{ s.titles }}</td><td class="td text-right">{{ s.copies }}</td><td class="td"></td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class StoreStats implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly language = inject(Language);

  protected readonly criteria = CRITERIA;
  protected request: DocumentStatsRequest = { criterion: 'bibtype', from: '', to: '', storeId: null };
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly stats = signal<DocumentStats | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.run();
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected status(code: string): string {
    return itemStatus(code).name;
  }

  protected async run(): Promise<void> {
    this.busy.set(true);
    try {
      this.stats.set(await this.api.documentStats(this.body()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** In bảng thống kê theo tiêu chí đang chọn (đã có đủ dòng trên màn, không gọi lại API). */
  protected print(): void {
    const s = this.stats();
    if (!s) return;
    const t = (k: string) => this.language.t(k);
    const crit = t(CRITERIA.find((c) => c.value === s.criterion)?.label ?? 'STORE_STATS.CRITERION');
    const store = this.stores().find((x) => x.id === this.request.storeId);
    const sub = [periodLine(this.request.from, this.request.to), store ? `${t('ITEMS_PAGE.STORE')}: ${store.name}` : ''].filter(Boolean).join(' — ');
    const job = {
      title: `${t('STORE_STATS.TITLE')} (${crit})`, subtitle: sub, headers: [crit, t('STORE_STATS.TITLES'), t('STORE_STATS.COPIES')],
      rows: s.rows.map((r) => [s.criterion === 'status' ? t(this.status(r.key)) : r.label, String(r.titles), String(r.copies)]),
      total: [t('STORE_STATS.TOTAL'), String(s.titles), String(s.copies)], right: [1, 2], truncated: false, backUrl: '/statistics-document', backLabel: 'STORE_STATS.TITLE',
    };
    if (!openReportPrint(this.router, job)) this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
  }

  protected async export(): Promise<void> {
    this.busy.set(true);
    try {
      saveFile(await this.api.exportDocumentStats(this.body()), 'thong-ke-tai-lieu.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private body(): DocumentStatsRequest {
    return { ...this.request, from: this.request.from || null, to: this.request.to || null };
  }
}

/** Báo cáo sách trong kho (monolith: /admin/store-book-report, quyền STORE_BOOK_REPORT): bản chưa mất/thanh lý, tổng theo kho. */
@Component({
  selector: 'app-store-book-report',
  imports: [DateInput, FormsModule, LDatePipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'STORE_BOOK_REPORT.TITLE' | translate }}</h4></div>
    <div class="panel grid grid-cols-1 md:grid-cols-4 gap-3 items-end">
      <div>
        <label for="sb-s" class="field-label">{{ 'ITEMS_PAGE.STORE' | translate }}</label>
        <select id="sb-s" class="input" [(ngModel)]="request.storeId" (change)="run()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
        </select>
      </div>
      <div><label for="sb-f" class="field-label">{{ 'STORE_STATS.RECEIVED_FROM' | translate }}</label><app-date-input fieldId="sb-f" [(ngModel)]="request.from"></app-date-input></div>
      <div><label for="sb-t" class="field-label">{{ 'STORE_STATS.RECEIVED_TO' | translate }}</label><app-date-input fieldId="sb-t" [(ngModel)]="request.to"></app-date-input></div>
      <div class="flex gap-2 justify-end">
        <button class="btn-secondary" [disabled]="busy() || !report()" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'REPORT_PRINT.PRINT' | translate }}</button>
        <button class="btn-secondary" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button class="btn-primary" [disabled]="busy()" (click)="run()"><span class="material-icons text-[18px]">summarize</span></button>
      </div>
      <p class="md:col-span-4 text-[11px] text-gray-400 -mt-1">{{ 'STORE_STATS.DATE_HINT' | translate }}</p>
    </div>

    @if (report(); as r) {
      <div class="grid grid-cols-1 lg:grid-cols-4 gap-5">
        <div class="panel !mb-0 self-start">
          <div class="text-sm text-gray-600">{{ 'STORE_BOOK_REPORT.SUMMARY' | translate: { titles: r.titleCount, copies: r.totalCount } }}</div>
          <table class="w-full text-left border-collapse mt-3">
            <tbody>
              @for (s of r.storeStats; track s.storeId) {
                <tr><td class="td text-[13px]">{{ s.storeName }}</td><td class="td text-right text-[13px]">{{ s.titles }} / {{ s.copies }}</td></tr>
              }
            </tbody>
          </table>
          <p class="text-[11px] text-gray-400 mt-2">{{ 'STORE_BOOK_REPORT.STAT_HINT' | translate }}</p>
        </div>
        <div class="lg:col-span-3 bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
          @if (busy()) { <app-loading /> }
          <div class="overflow-x-auto w-full">
            <table class="w-full text-left border-collapse min-w-[820px]">
              <thead><tr>
                <th class="th w-32">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th>
                <th class="th w-40">{{ 'ITEMS_PAGE.AUTHOR' | translate }}</th><th class="th w-24">DDC</th>
                <th class="th w-32">{{ 'STORE_BOOK_REPORT.RECEIVED_ON' | translate }}</th><th class="th w-36">{{ 'ITEMS_PAGE.STORE' | translate }}</th>
                <th class="th w-28 !text-center">{{ 'COMMON.STATUS' | translate }}</th>
              </tr></thead>
              <tbody>
                @for (b of r.rows; track b.barcode) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td font-mono text-[13px]">{{ b.barcode }}</td><td class="td">{{ b.title ?? '—' }}</td><td class="td">{{ b.author ?? '' }}</td>
                    <td class="td font-mono text-[13px]">{{ b.ddc ?? '' }}</td><td class="td">{{ (b.receivedOn ?? b.registeredOn) | ldate: 'dd/MM/yyyy' }}
                      @if (b.receiptCode) { <div class="text-[11px] text-gray-400">{{ 'STORE_BOOK_REPORT.RECEIPT_NO' | translate: { no: b.receiptCode } }}</div> }</td>
                    <td class="td">{{ b.storeName ?? '—' }}</td>
                    <td class="td text-center"><span [class]="status(b.status, b.onLoan).badge">{{ status(b.status, b.onLoan).name | translate }}</span></td>
                  </tr>
                } @empty {
                  <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr>
                }
              </tbody>
            </table>
          </div>
          <app-paginator [total]="r.totalCount" [pageIndex]="request.pageIndex" [pageSize]="request.pageSize" (pageChange)="onPage($event)" />
        </div>
      </div>
    }
  `,
})
export class StoreBookReportPage implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly language = inject(Language);

  protected request: StoreBookReportRequest = { storeId: null, from: '', to: '', pageIndex: 1, pageSize: 50 };
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly report = signal<StoreBookReport | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected status(code: string, onLoan: boolean) {
    return itemStatus(code, onLoan);
  }

  protected run(): void {
    this.request.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.request.pageIndex = e.pageIndex;
    this.request.pageSize = e.pageSize;
    void this.load();
  }

  /** In: lấy hết các trang theo bộ lọc (tối đa ACQ_PRINT_MAX_ROWS bản) rồi mở trang in chung. */
  protected async print(): Promise<void> {
    this.busy.set(true);
    try {
      const t = (k: string) => this.language.t(k);
      const rows: StoreBookReport['rows'] = [];
      let first: StoreBookReport | null = null;
      for (let page = 1; page <= Math.ceil(ACQ_PRINT_MAX_ROWS / 500); page++) {
        const r = await this.api.storeBookReport({ ...this.body(), pageIndex: page, pageSize: 500 });
        first ??= r;
        rows.push(...r.rows);
        if (!r.rows.length || rows.length >= r.totalCount) break;
      }
      if (!first || !rows.length) {
        this.toastr.warning('REPORT_PRINT.NOTHING');
        return;
      }
      const date = (v: string | null) => (v ? v.slice(0, 10).split('-').reverse().join('/') : '');
      const store = this.stores().find((x) => x.id === this.request.storeId);
      const job = {
        title: t('STORE_BOOK_REPORT.TITLE'), subtitle: [periodLine(this.request.from, this.request.to), store ? `${t('ITEMS_PAGE.STORE')}: ${store.name}` : ''].filter(Boolean).join(' — '),
        headers: [t('ITEMS_PAGE.BARCODE'), t('ITEMS_PAGE.TITLE_COL'), t('ITEMS_PAGE.AUTHOR'), 'DDC', t('STORE_BOOK_REPORT.RECEIVED_ON'), t('ITEMS_PAGE.STORE'), t('COMMON.STATUS')],
        rows: rows.slice(0, ACQ_PRINT_MAX_ROWS).map((b) => [b.barcode, b.title ?? '', b.author ?? '', b.ddc ?? '', date(b.receivedOn ?? b.registeredOn), b.storeName ?? '', t(this.status(b.status, b.onLoan).name)]),
        total: [t('STORE_STATS.TOTAL'), `${first.titleCount} / ${first.totalCount}`, '', '', '', '', ''], right: [] as number[],
        truncated: first.totalCount > ACQ_PRINT_MAX_ROWS, backUrl: '/store-book-report', backLabel: 'STORE_BOOK_REPORT.TITLE',
      };
      if (!openReportPrint(this.router, job)) this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async export(): Promise<void> {
    this.busy.set(true);
    try {
      saveFile(await this.api.exportStoreBookReport(this.body()), 'sach-trong-kho.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private body(): StoreBookReportRequest {
    return { ...this.request, from: this.request.from || null, to: this.request.to || null };
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.report.set(await this.api.storeBookReport(this.body()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
