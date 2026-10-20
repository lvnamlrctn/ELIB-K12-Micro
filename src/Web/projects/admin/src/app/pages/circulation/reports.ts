import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CircPlace, CirculationReport, CirculationReportRequest, errorMessage, saveFile } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { ACQ_PRINT_MAX_ROWS, numericColumns, openReportPrint, periodLine } from '../acquisition/report-print';
import { Loading, Paginator } from '../../shared/ui';

const REPORT_TYPES: { value: number; label: string; hint: string }[] = [
  { value: 1, label: 'CIRC_REPORT_PAGE.TYPE.1', hint: 'CIRC_REPORT_PAGE.HINT.1' },
  { value: 2, label: 'CIRC_REPORT_PAGE.TYPE.2', hint: 'CIRC_REPORT_PAGE.HINT.2' },
  { value: 3, label: 'CIRC_REPORT_PAGE.TYPE.3', hint: 'CIRC_REPORT_PAGE.HINT.3' },
  { value: 4, label: 'CIRC_REPORT_PAGE.TYPE.4', hint: 'CIRC_REPORT_PAGE.HINT.4' },
  { value: 5, label: 'CIRC_REPORT_PAGE.TYPE.5', hint: 'CIRC_REPORT_PAGE.HINT.5' },
  { value: 6, label: 'CIRC_REPORT_PAGE.TYPE.6', hint: '' },
  { value: 7, label: 'CIRC_REPORT_PAGE.TYPE.7', hint: '' },
  { value: 8, label: 'CIRC_REPORT_PAGE.TYPE.8', hint: 'CIRC_REPORT_PAGE.HINT.8' },
  { value: 9, label: 'CIRC_REPORT_PAGE.TYPE.9', hint: 'CIRC_REPORT_PAGE.HINT.9' },
  { value: 10, label: 'CIRC_REPORT_PAGE.TYPE.10', hint: 'CIRC_REPORT_PAGE.HINT.10' },
  { value: 11, label: 'CIRC_REPORT_PAGE.TYPE.11', hint: 'CIRC_REPORT_PAGE.HINT.11' },
];

type Named = { id: number; name: string; publicId: string };

/** Báo cáo lưu thông (monolith: /admin/circulation-report, quyền CIRC_REPORT) — 11 loại, xem theo trang hoặc xuất Excel. */
@Component({
  selector: 'app-circulation-reports',
  imports: [FormsModule, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'CIRC_REPORT_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-6 gap-4">
        <div class="md:col-span-2">
          <label for="rp-type" class="field-label">{{ 'CIRC_REPORT_PAGE.TYPE_LABEL' | translate }}</label>
          <select id="rp-type" class="input" [(ngModel)]="request.reportType" (change)="run()">
            @for (t of types; track t.value) { <option [ngValue]="t.value">{{ t.value }}. {{ t.label | translate }}</option> }
          </select>
          <div class="text-[11px] text-gray-500 mt-1">{{ hint() ? (hint() | translate) : '' }}</div>
        </div>
        <div>
          <label for="rp-from" class="field-label">{{ 'CIRC_REPORT_PAGE.FROM' | translate }}</label>
          <input id="rp-from" type="date" class="input" [(ngModel)]="request.from" />
        </div>
        <div>
          <label for="rp-to" class="field-label">{{ 'CIRC_REPORT_PAGE.TO' | translate }}</label>
          <input id="rp-to" type="date" class="input" [(ngModel)]="request.to" />
        </div>
        <div>
          <label for="rp-place" class="field-label">{{ 'CIRC_REPORT_PAGE.PLACE' | translate }}</label>
          <select id="rp-place" class="input" [(ngModel)]="request.circPlaceId">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (p of places(); track p.id) { <option [ngValue]="p.id">{{ p.code }} — {{ p.name }}</option> }
          </select>
        </div>
        <div>
          <label for="rp-type-reader" class="field-label">{{ 'CIRC_REPORT_PAGE.READER_TYPE' | translate }}</label>
          <select id="rp-type-reader" class="input" [(ngModel)]="request.readerTypeId">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (t of readerTypes(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select>
        </div>
      </div>
      <div class="flex justify-end gap-2 mt-4">
        <button class="btn-secondary" [disabled]="busy()" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'REPORT_PRINT.PRINT' | translate }}</button>
        <button class="btn-secondary" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button class="btn-primary" [disabled]="busy()" (click)="run()"><span class="material-icons text-[18px]">summarize</span> {{ 'CIRC_REPORT_PAGE.RUN' | translate }}</button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      @if (report(); as r) {
        <div class="px-5 py-4 border-b border-gray-100 text-center">
          @if (r.libraryName) { <div class="text-xs text-gray-500 uppercase">{{ r.libraryName }}</div> }
          <div class="font-semibold text-gray-800">{{ r.title }}</div>
          <div class="text-xs text-gray-500">{{ 'CIRC_REPORT_PAGE.ROWS' | translate: { count: r.totalCount } }}@if (r.truncated) { {{ 'CIRC_REPORT_PAGE.TRUNCATED' | translate }} }</div>
        </div>
        <div class="overflow-x-auto w-full">
          <table class="w-full text-left border-collapse min-w-[900px]">
            <thead><tr>@for (h of r.headers; track $index) { <th class="th">{{ h }}</th> }</tr></thead>
            <tbody>
              @for (row of r.rows; track $index) {
                <tr class="hover:bg-gray-50/50">@for (c of row; track $index) { <td class="td text-[13px]">{{ c }}</td> }</tr>
              } @empty {
                <tr><td [attr.colspan]="r.headers.length || 1" class="py-8 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr>
              }
              @if (r.totalRow; as t) {
                <tr class="bg-gray-50 font-semibold">@for (c of t; track $index) { <td class="td text-[13px]">{{ c }}</td> }</tr>
              }
            </tbody>
          </table>
        </div>
        <app-paginator [total]="r.totalCount" [pageIndex]="request.pageIndex" [pageSize]="request.pageSize" (pageChange)="onPage($event)" />
      }
    </div>
  `,
})
export class CirculationReports implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);

  protected readonly types = REPORT_TYPES;
  protected readonly places = signal<CircPlace[]>([]);
  protected readonly readerTypes = signal<Named[]>([]);
  protected readonly report = signal<CirculationReport | null>(null);
  protected readonly busy = signal(false);
  protected request: CirculationReportRequest = {
    reportType: 1, from: null, to: null, circPlaceId: null, readerTypeId: null, className: null, pageIndex: 1, pageSize: 20,
  };

  async ngOnInit(): Promise<void> {
    void this.load();
    this.api.crud<CircPlace>('circ-places', 'circulation').searchAll().then((p) => this.places.set(p), () => undefined);
    this.api.crud<Named>('reader-types', 'patron').searchAll().then((t) => this.readerTypes.set(t), () => undefined);
  }

  protected hint(): string {
    return REPORT_TYPES.find((t) => t.value === this.request.reportType)?.hint ?? '';
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
      saveFile(await this.api.exportCirculationReport(this.body()), `bao-cao-luu-thong-${this.request.reportType}.xlsx`);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** In theo mẫu giấy: lấy hết các trang của báo cáo (tối đa ACQ_PRINT_MAX_ROWS dòng) rồi mở trang in chung. */
  protected async print(): Promise<void> {
    this.busy.set(true);
    try {
      const rows: string[][] = [];
      let first: CirculationReport | null = null;
      for (let page = 1; page <= Math.ceil(ACQ_PRINT_MAX_ROWS / 500); page++) {
        const r = await this.api.circulationReport({ ...this.body(), pageIndex: page, pageSize: 500 });
        first ??= r;
        rows.push(...r.rows);
        if (!r.rows.length || rows.length >= r.totalCount) break;
      }
      if (!first || !rows.length) {
        this.toastr.warning('REPORT_PRINT.NOTHING');
        return;
      }
      // Hầu hết loại báo cáo đã có cột STT riêng — bỏ đi vì trang in tự thêm cột STT (dòng tổng giữ nhãn "Tổng cộng" ở cột đầu còn lại).
      const dropStt = first.headers[0] === 'STT';
      const cut = <T>(cells: T[]): T[] => (dropStt ? cells.slice(1) : cells);
      const body = rows.slice(0, ACQ_PRINT_MAX_ROWS).map(cut);
      const total = first.totalRow ? cut(first.totalRow) : null;
      if (dropStt && total && first.totalRow?.[0] && !total[0]) total[0] = first.totalRow[0];
      const job = {
        title: first.title, subtitle: periodLine(this.request.from, this.request.to), headers: cut(first.headers), rows: body,
        total, right: numericColumns(body), truncated: first.truncated || first.totalCount > ACQ_PRINT_MAX_ROWS,
        backUrl: '/circulation-report', backLabel: 'MENU.CIRC_REPORT',
      };
      if (!openReportPrint(this.router, job)) this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private body(): CirculationReportRequest {
    return { ...this.request, from: this.request.from || null, to: this.request.to || null };
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.report.set(await this.api.circulationReport(this.body()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
