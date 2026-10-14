import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, DocumentStats, DocumentStatsRequest, StoreBookReport, StoreBookReportRequest, StoreOption, errorMessage, saveFile } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { itemStatus } from './items';

const CRITERIA: { value: string; label: string }[] = [
  { value: 'bibtype', label: 'STORE_STATS.BY_TYPE' },
  { value: 'ddc', label: 'STORE_STATS.BY_DDC' },
  { value: 'store', label: 'STORE_STATS.BY_STORE' },
  { value: 'status', label: 'STORE_STATS.BY_STATUS' },
];

/** Thống kê tài liệu kho (monolith: /admin/statistics-document, quyền STAT_DOC) theo loại tài liệu, lớp DDC, kho, trạng thái. */
@Component({
  selector: 'app-store-stats',
  imports: [FormsModule, Loading, TranslatePipe],
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
      <div><label for="st-f" class="field-label">{{ 'STORE_STATS.REGISTERED_FROM' | translate }}</label><input id="st-f" type="date" class="input" [(ngModel)]="request.from" /></div>
      <div><label for="st-t" class="field-label">{{ 'STORE_STATS.REGISTERED_TO' | translate }}</label><input id="st-t" type="date" class="input" [(ngModel)]="request.to" /></div>
      <div class="flex gap-2 justify-end">
        <button class="btn-secondary" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button class="btn-primary" [disabled]="busy()" (click)="run()"><span class="material-icons text-[18px]">query_stats</span></button>
      </div>
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
  imports: [FormsModule, LDatePipe, Loading, Paginator, TranslatePipe],
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
      <div><label for="sb-f" class="field-label">{{ 'STORE_STATS.REGISTERED_FROM' | translate }}</label><input id="sb-f" type="date" class="input" [(ngModel)]="request.from" /></div>
      <div><label for="sb-t" class="field-label">{{ 'STORE_STATS.REGISTERED_TO' | translate }}</label><input id="sb-t" type="date" class="input" [(ngModel)]="request.to" /></div>
      <div class="flex gap-2 justify-end">
        <button class="btn-secondary" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button class="btn-primary" [disabled]="busy()" (click)="run()"><span class="material-icons text-[18px]">summarize</span></button>
      </div>
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
                <th class="th w-28">{{ 'STORE_BOOK_REPORT.REGISTERED_ON' | translate }}</th><th class="th w-36">{{ 'ITEMS_PAGE.STORE' | translate }}</th>
                <th class="th w-28 !text-center">{{ 'COMMON.STATUS' | translate }}</th>
              </tr></thead>
              <tbody>
                @for (b of r.rows; track b.barcode) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td font-mono text-[13px]">{{ b.barcode }}</td><td class="td">{{ b.title ?? '—' }}</td><td class="td">{{ b.author ?? '' }}</td>
                    <td class="td font-mono text-[13px]">{{ b.ddc ?? '' }}</td><td class="td">{{ b.registeredOn | ldate: 'dd/MM/yyyy' }}</td>
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
