import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { errorMessage, saveFile } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { DateInput } from '../../shared/date-input';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { ACQ_PRINT_MAX_ROWS, openReportPrint, periodLine } from '../acquisition/report-print';
import { MissingClaimRow, ReceivedDetailRow, ReceivedSummaryRow, SerialReportName, SerialReportRequest, SerialsApi, Subscription } from './serials-api';

type Tab = 'summary' | 'detail' | 'missing';
type Row = ReceivedSummaryRow | ReceivedDetailRow | MissingClaimRow;

const TABS: { tab: Tab; label: string; report: SerialReportName; file: string }[] = [
  { tab: 'summary', label: 'SERIALS.REPORT_SUMMARY', report: 'ReceivedSummary', file: 'tong-hop-nhan-bao.xlsx' },
  { tab: 'detail', label: 'SERIALS.REPORT_DETAIL', report: 'ReceivedDetail', file: 'chi-tiet-nhan-bao.xlsx' },
  { tab: 'missing', label: 'SERIALS.REPORT_MISSING', report: 'MissingClaim', file: 'thieu-khieu-nai-bao.xlsx' },
];

/**
 * Báo cáo báo/tạp chí (monolith: /admin/serial-report, quyền SERIAL_REPORT): tổng hợp nhận, chi tiết nhận, thiếu/khiếu nại. Khoảng ngày lọc theo ngày
 * nhận (hai báo cáo nhận) hoặc ngày dự kiến (thiếu/khiếu nại). Xuất Excel từ server; in theo mẫu giấy chung (cơ quan chủ quản, địa danh từ tham số).
 */
@Component({
  selector: 'app-serial-reports',
  imports: [DateInput, FormsModule, LDatePipe, LNumberPipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'SERIALS.REPORTS_TITLE' | translate }}</h4></div>
    <div class="flex flex-wrap gap-2 mb-4">
      @for (t of tabs; track t.tab) {
        <button type="button" [class]="tab() === t.tab ? 'btn-primary' : 'btn-secondary'" (click)="setTab(t.tab)">{{ t.label | translate }}</button>
      }
    </div>

    <div class="panel grid grid-cols-2 md:grid-cols-4 gap-3 items-end">
      <div class="col-span-2"><label for="sr-su" class="field-label">{{ 'SERIALS.SUBSCRIPTION' | translate }}</label>
        <select id="sr-su" class="input" [(ngModel)]="request.subscriptionPublicId"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (s of subscriptions(); track s.publicId) { <option [ngValue]="s.publicId">{{ s.title }}@if (s.issn) { ({{ s.issn }}) }</option> }</select></div>
      <div class="col-span-2"><label for="sr-kw" class="field-label">{{ 'SERIALS.SEARCH_TITLE_ISSN' | translate }}</label><input id="sr-kw" class="input" [(ngModel)]="request.keyword" (keydown.enter)="find()" /></div>
      <div><label for="sr-f" class="field-label">{{ (tab() === 'missing' ? 'SERIALS.PLANNED_FROM' : 'SERIALS.RECEIVED_FROM') | translate }}</label><app-date-input fieldId="sr-f" [(ngModel)]="request.from"></app-date-input></div>
      <div><label for="sr-t" class="field-label">{{ (tab() === 'missing' ? 'SERIALS.PLANNED_TO' : 'SERIALS.RECEIVED_TO') | translate }}</label><app-date-input fieldId="sr-t" [(ngModel)]="request.to"></app-date-input></div>
      <div class="col-span-2 flex gap-2 justify-end">
        <button class="btn-secondary" [disabled]="busy()" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'ACQ_REPORT.PRINT' | translate }}</button>
        <button class="btn-secondary" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button class="btn-primary" [disabled]="busy()" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        @switch (tab()) {
          @case ('summary') {
            <table class="w-full text-left border-collapse min-w-[760px]">
              <thead><tr><th class="th w-12">{{ 'ACQ.NO' | translate }}</th><th class="th">{{ 'SERIALS.TITLE_COL' | translate }}</th><th class="th w-28">ISSN</th>
                <th class="th w-24 !text-right">{{ 'SERIALS.ISSUES_COUNT' | translate }}</th><th class="th w-24 !text-right">{{ 'SERIALS.QUANTITY' | translate }}</th>
                <th class="th w-36">{{ 'SERIALS.LAST_RECEIVED' | translate }}</th></tr></thead>
              <tbody>
                @for (r of summary(); track r.subscriptionPublicId; let i = $index) {
                  <tr class="hover:bg-gray-50/50"><td class="td">{{ (request.pageIndex - 1) * request.pageSize + i + 1 }}</td><td class="td">{{ r.title }}</td><td class="td font-mono text-[13px]">{{ r.issn ?? '' }}</td>
                    <td class="td text-right">{{ r.issueCount | lnumber }}</td><td class="td text-right">{{ r.quantity | lnumber }}</td><td class="td">{{ r.lastReceivedDate ? (r.lastReceivedDate | ldate: 'dd/MM/yyyy') : '' }}</td></tr>
                } @empty { <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr> }
              </tbody>
            </table>
          }
          @case ('detail') {
            <table class="w-full text-left border-collapse min-w-[860px]">
              <thead><tr><th class="th w-12">{{ 'ACQ.NO' | translate }}</th><th class="th">{{ 'SERIALS.TITLE_COL' | translate }}</th><th class="th w-40">{{ 'SERIALS.ISSUE_LABEL' | translate }}</th>
                <th class="th w-28">{{ 'SERIALS.PLANNED_DATE' | translate }}</th><th class="th w-28">{{ 'SERIALS.RECEIVED_DATE' | translate }}</th><th class="th w-20 !text-right">{{ 'SERIALS.QUANTITY' | translate }}</th>
                <th class="th w-24 !text-center">{{ 'SERIALS.LATE' | translate }}</th></tr></thead>
              <tbody>
                @for (r of detail(); track r.issuePublicId; let i = $index) {
                  <tr class="hover:bg-gray-50/50"><td class="td">{{ (request.pageIndex - 1) * request.pageSize + i + 1 }}</td><td class="td">{{ r.title }}</td><td class="td">{{ r.label }}</td>
                    <td class="td">{{ r.plannedDate ? (r.plannedDate | ldate: 'dd/MM/yyyy') : '' }}</td><td class="td">{{ r.receivedDate ? (r.receivedDate | ldate: 'dd/MM/yyyy') : '' }}</td>
                    <td class="td text-right">{{ r.quantity | lnumber }}</td><td class="td text-center">@if (r.late) { <span class="badge-warn">{{ 'SERIALS.LATE' | translate }}</span> }</td></tr>
                } @empty { <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr> }
              </tbody>
            </table>
          }
          @case ('missing') {
            <table class="w-full text-left border-collapse min-w-[860px]">
              <thead><tr><th class="th w-12">{{ 'ACQ.NO' | translate }}</th><th class="th">{{ 'SERIALS.TITLE_COL' | translate }}</th><th class="th w-40">{{ 'SERIALS.ISSUE_LABEL' | translate }}</th>
                <th class="th w-28">{{ 'SERIALS.PLANNED_DATE' | translate }}</th><th class="th w-32">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-20 !text-right">{{ 'SERIALS.CLAIMS' | translate }}</th>
                <th class="th w-28">{{ 'SERIALS.CLAIM_DATE' | translate }}</th></tr></thead>
              <tbody>
                @for (r of missing(); track r.issuePublicId; let i = $index) {
                  <tr class="hover:bg-gray-50/50"><td class="td">{{ (request.pageIndex - 1) * request.pageSize + i + 1 }}</td><td class="td">{{ r.title }}</td><td class="td">{{ r.label }}</td>
                    <td class="td">{{ r.plannedDate ? (r.plannedDate | ldate: 'dd/MM/yyyy') : '' }}</td><td class="td">{{ r.statusName }}</td><td class="td text-right">{{ r.claimCount | lnumber }}</td>
                    <td class="td">{{ r.claimDate ? (r.claimDate | ldate: 'dd/MM/yyyy') : '' }}</td></tr>
                } @empty { <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr> }
              </tbody>
            </table>
          }
        }
      </div>
      @if (total() > 0) { <app-paginator [total]="total()" [pageIndex]="request.pageIndex" [pageSize]="request.pageSize" (pageChange)="onPage($event)" /> }
    </div>
  `,
})
export class SerialReports implements OnInit {
  private readonly serials = inject(SerialsApi);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslateService);

  protected readonly tabs = TABS;
  protected readonly tab = signal<Tab>('summary');
  protected request: SerialReportRequest = { subscriptionPublicId: null, keyword: null, from: null, to: null, pageIndex: 1, pageSize: 20 };
  protected readonly subscriptions = signal<Subscription[]>([]);
  protected readonly summary = signal<ReceivedSummaryRow[]>([]);
  protected readonly detail = signal<ReceivedDetailRow[]>([]);
  protected readonly missing = signal<MissingClaimRow[]>([]);
  protected readonly total = signal(0);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.run();
    this.serials.searchSubscriptions({ pageIndex: 1, pageSize: 200 }).then((p) => this.subscriptions.set(p.items), () => undefined);
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

  protected async export(): Promise<void> {
    const t = this.current();
    await this.act(async () => saveFile(await this.serials.exportReport(t.report, this.body()), t.file));
  }

  /** Lấy hết các trang (trần ACQ_PRINT_MAX_ROWS dòng) rồi mở trang in theo mẫu giấy chung. */
  protected async print(): Promise<void> {
    const t = this.current();
    await this.act(async () => {
      const rows: Row[] = [];
      let total = 0;
      for (let page = 1; ; page++) {
        const p = await this.serials.report<Row>(t.report, { ...this.body(), pageIndex: page, pageSize: 500 });
        total = p.totalCount;
        rows.push(...p.items);
        if (!p.items.length || rows.length >= total || rows.length >= ACQ_PRINT_MAX_ROWS) break;
      }
      if (!rows.length) {
        this.toastr.warning('ACQ_REPORT.NOTHING_TO_PRINT');
        return;
      }
      const tr = (k: string) => this.i18n.instant(k) as string;
      const date = (v: string | null) => (v ? v.split('-').reverse().join('/') : '');
      const num = (v: number) => v.toLocaleString('vi-VN');
      const common = { title: tr(t.label), subtitle: periodLine(this.request.from, this.request.to), total: null, truncated: total > ACQ_PRINT_MAX_ROWS, backUrl: '/serial-reports', backLabel: tr('SERIALS.REPORTS_TITLE') };
      const job = this.tab() === 'summary'
        ? { ...common, headers: [tr('SERIALS.TITLE_COL'), 'ISSN', tr('SERIALS.ISSUES_COUNT'), tr('SERIALS.QUANTITY'), tr('SERIALS.LAST_RECEIVED')], right: [2, 3],
            rows: (rows as ReceivedSummaryRow[]).map((r) => [r.title, r.issn ?? '', num(r.issueCount), num(r.quantity), date(r.lastReceivedDate)]) }
        : this.tab() === 'detail'
          ? { ...common, headers: [tr('SERIALS.TITLE_COL'), tr('SERIALS.ISSUE_LABEL'), tr('SERIALS.PLANNED_DATE'), tr('SERIALS.RECEIVED_DATE'), tr('SERIALS.QUANTITY'), tr('SERIALS.LATE')], right: [4],
              rows: (rows as ReceivedDetailRow[]).map((r) => [r.title, r.label, date(r.plannedDate), date(r.receivedDate), num(r.quantity), r.late ? tr('SERIALS.LATE') : '']) }
          : { ...common, headers: [tr('SERIALS.TITLE_COL'), tr('SERIALS.ISSUE_LABEL'), tr('SERIALS.PLANNED_DATE'), tr('COMMON.STATUS'), tr('SERIALS.CLAIMS'), tr('SERIALS.CLAIM_DATE')], right: [4],
              rows: (rows as MissingClaimRow[]).map((r) => [r.title, r.label, date(r.plannedDate), r.statusName, num(r.claimCount), date(r.claimDate)]) };
      if (!openReportPrint(this.router, job)) this.toastr.error(tr('LABELS_PAGE.STORAGE_BLOCKED'));
    });
  }

  private current() {
    return TABS.find((t) => t.tab === this.tab())!;
  }

  private body(): SerialReportRequest {
    const r = this.request;
    return { ...r, keyword: r.keyword?.trim() || null, from: r.from || null, to: r.to || null };
  }

  private async run(): Promise<void> {
    const t = this.current();
    this.busy.set(true);
    try {
      const page = await this.serials.report<Row>(t.report, this.body());
      this.total.set(page.totalCount);
      if (this.tab() === 'summary') this.summary.set(page.items as ReceivedSummaryRow[]);
      else if (this.tab() === 'detail') this.detail.set(page.items as ReceivedDetailRow[]);
      else this.missing.set(page.items as MissingClaimRow[]);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
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
}
