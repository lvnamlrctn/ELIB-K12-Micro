import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../../core/i18n';
import { Api, SPACE_CATEGORIES, SpaceReport, errorMessage, saveFile } from '../../core/api';
import { Loading } from '../../shared/ui';
import { ToastrService } from '../../shared/toastr';
import { openReportPrint, periodLine } from '../acquisition/report-print';

/**
 * Báo cáo đặt phòng (monolith: RoomBookingReportService, quyền SPACE_REPORT) — bản gọn hơn bản gốc: không có histogram
 * giờ/ngày-trong-tuần, biểu đồ dạng cột CSS đúng khuôn search-stats.ts (chưa có thư viện biểu đồ nào trong admin).
 */
@Component({
  selector: 'app-space-report',
  imports: [FormsModule, Loading, TranslatePipe],
  template: `
    <div class="mb-5 flex items-end gap-3 flex-wrap">
      <h4 class="page-title">{{ 'SPACE_REPORT_PAGE.TITLE' | translate }}</h4>
      <div class="ml-auto flex items-end gap-2 flex-wrap">
        <div><label class="form-label">{{ 'COMMON.FROM' | translate }}</label><input type="date" class="input" [(ngModel)]="from" /></div>
        <div><label class="form-label">{{ 'COMMON.TO' | translate }}</label><input type="date" class="input" [(ngModel)]="to" /></div>
        <div>
          <label class="form-label">{{ 'SPACE_CAT.CATEGORY' | translate }}</label>
          <select class="input" [(ngModel)]="category">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (c of categories; track c.value) { <option [ngValue]="c.value">{{ c.label | translate }}</option> }
          </select>
        </div>
        <button type="button" class="btn-primary" [disabled]="loading()" (click)="load()"><span class="material-icons text-[18px]">query_stats</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
        <button type="button" class="btn-secondary" [disabled]="loading() || !report()" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'REPORT_PRINT.PRINT' | translate }}</button>
        <button type="button" class="btn-secondary" [disabled]="loading() || exporting()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
      </div>
    </div>

    <div class="relative">
      @if (loading()) { <app-loading /> }
      @if (report(); as r) {
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-x-4 mb-5">
          <div class="panel"><div class="text-xs text-gray-500">{{ 'SPACE_REPORT_PAGE.TOTAL' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ r.totalBookings }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'SPACE_REPORT_PAGE.NO_SHOW_RATE' | translate }}</div>
            <div class="text-2xl font-semibold" [class]="r.noShowRatePercent > 20 ? 'text-red-600' : 'text-gray-800'">{{ r.noShowRatePercent }}%</div>
            <div class="text-xs text-gray-500">{{ r.noShow }} {{ 'SPACE_REPORT_PAGE.TIMES' | translate }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'SPACE_REPORT_PAGE.CANCEL_RATE' | translate }}</div>
            <div class="text-2xl font-semibold text-gray-800">{{ r.cancelRatePercent }}%</div>
            <div class="text-xs text-gray-500">{{ r.cancelled }} {{ 'SPACE_REPORT_PAGE.TIMES' | translate }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'SPACE_REPORT_PAGE.USED_HOURS' | translate }}</div>
            <div class="text-2xl font-semibold text-gray-800">{{ r.totalUsedHours }}</div>
            <div class="text-xs text-gray-500">{{ r.totalVisitors }} {{ 'SPACE_REPORT_PAGE.VISITORS' | translate }}</div></div>
        </div>

        @if (r.bySpace.length) {
          <div class="panel mb-5">
            <div class="font-medium text-gray-800 mb-3">{{ 'SPACE_REPORT_PAGE.BY_SPACE' | translate }}</div>
            <div class="space-y-2">
              @for (row of r.bySpace; track row.spaceId) {
                <div class="flex items-center gap-3">
                  <div class="w-40 truncate text-sm text-gray-700" [title]="row.spaceName">{{ row.spaceName }}</div>
                  <div class="flex-1 bg-gray-100 rounded h-5 overflow-hidden">
                    <div class="bg-blue-500 h-full" [style.width.%]="(row.bookings / maxBookings(r)) * 100"></div>
                  </div>
                  <div class="w-16 text-right text-sm text-gray-600">{{ row.bookings }}</div>
                </div>
              }
            </div>
          </div>
        }

        <div class="panel">
          <div class="font-medium text-gray-800 mb-2">{{ 'SPACE_REPORT_PAGE.TOP_READERS' | translate }}</div>
          <table class="w-full text-left border-collapse">
            <thead><tr>
              <th class="th">{{ 'SPACE_BANS_PAGE.READER' | translate }}</th>
              <th class="th !text-right">{{ 'SPACE_REPORT_PAGE.BOOKINGS' | translate }}</th>
              <th class="th !text-right">{{ 'SPACE_REPORT_PAGE.NO_SHOW_COUNT' | translate }}</th>
            </tr></thead>
            <tbody>
              @for (tr of r.topReaders; track tr.cardNo) {
                <tr><td class="td">{{ tr.readerName ?? '—' }}<span class="text-gray-400 font-mono ml-1">{{ tr.cardNo }}</span></td>
                  <td class="td text-right">{{ tr.bookings }}</td><td class="td text-right">{{ tr.noShow }}</td></tr>
              } @empty { <tr><td colspan="3" class="td text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class SpaceReportPage {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly language = inject(Language);

  protected readonly categories = SPACE_CATEGORIES;
  protected from = isoDaysAgo(30);
  protected to = isoDaysAgo(0);
  protected category: number | null = null;
  protected readonly report = signal<SpaceReport | null>(null);
  protected readonly loading = signal(false);
  protected readonly exporting = signal(false);

  protected maxBookings(r: SpaceReport): number {
    return Math.max(1, ...r.bySpace.map((s) => s.bookings));
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.report.set(await this.api.spaceReport({ from: this.from, to: this.to, category: this.category }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  /** In: các chỉ tiêu tổng, theo không gian, bạn đọc đặt nhiều nhất — mỗi phần một bảng. */
  protected print(): void {
    const r = this.report();
    if (!r) return;
    const t = (k: string) => this.language.t(k);
    const num = (v: number) => v.toLocaleString('vi-VN', { maximumFractionDigits: 2 });
    const sections = [
      {
        heading: t('SPACE_REPORT_PAGE.SUMMARY'), headers: [t('SPACE_REPORT_PAGE.INDICATOR'), t('SPACE_REPORT_PAGE.VALUE')], right: [1], total: null,
        rows: [
          [t('SPACE_REPORT_PAGE.TOTAL'), num(r.totalBookings)],
          [t('SPACE_REPORT_PAGE.NO_SHOW_RATE'), `${num(r.noShowRatePercent)}% (${num(r.noShow)} ${t('SPACE_REPORT_PAGE.TIMES')})`],
          [t('SPACE_REPORT_PAGE.CANCEL_RATE'), `${num(r.cancelRatePercent)}% (${num(r.cancelled)} ${t('SPACE_REPORT_PAGE.TIMES')})`],
          [t('SPACE_REPORT_PAGE.USED_HOURS'), num(r.totalUsedHours)],
          [t('SPACE_REPORT_PAGE.VISITORS'), num(r.totalVisitors)],
        ],
      },
      {
        heading: t('SPACE_REPORT_PAGE.BY_SPACE'), right: [1, 2, 3, 4, 5],
        headers: [t('SPACE_REPORT_PAGE.SPACE'), t('SPACE_REPORT_PAGE.BOOKINGS'), t('SPACE_REPORT_PAGE.COMPLETED'), t('SPACE_REPORT_PAGE.NO_SHOW_COUNT'), t('SPACE_REPORT_PAGE.CANCELLED'), t('SPACE_REPORT_PAGE.USED_HOURS')],
        rows: r.bySpace.map((s) => [s.spaceName, num(s.bookings), num(s.completed), num(s.noShow), num(s.cancelled), num(s.usedHours)]),
        total: r.bySpace.length ? [t('STORE_STATS.TOTAL'), num(r.bySpace.reduce((a, s) => a + s.bookings, 0)), num(r.bySpace.reduce((a, s) => a + s.completed, 0)),
          num(r.bySpace.reduce((a, s) => a + s.noShow, 0)), num(r.bySpace.reduce((a, s) => a + s.cancelled, 0)), num(r.bySpace.reduce((a, s) => a + s.usedHours, 0))] : null,
      },
      {
        heading: t('SPACE_REPORT_PAGE.TOP_READERS'), right: [2, 3], total: null,
        headers: [t('SPACE_REPORT_PAGE.CARD_NO'), t('SPACE_BANS_PAGE.READER'), t('SPACE_REPORT_PAGE.BOOKINGS'), t('SPACE_REPORT_PAGE.NO_SHOW_COUNT')],
        rows: r.topReaders.map((x) => [x.cardNo, x.readerName ?? '', num(x.bookings), num(x.noShow)]),
      },
    ].filter((s) => s.rows.length);
    const job = {
      title: t('SPACE_REPORT_PAGE.TITLE'), subtitle: periodLine(this.from, this.to), headers: [] as string[], rows: [] as string[][], total: null, right: [] as number[],
      truncated: false, sections, backUrl: '/space-report', backLabel: 'MENU.SPACE_REPORT',
    };
    if (!openReportPrint(this.router, job)) this.toastr.error('LABELS_PAGE.STORAGE_BLOCKED');
  }

  protected async export(): Promise<void> {
    this.exporting.set(true);
    try {
      const blob = await this.api.exportSpaceReport({ from: this.from, to: this.to, category: this.category });
      saveFile(blob, 'bao-cao-dat-phong.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.exporting.set(false);
    }
  }
}

function isoDaysAgo(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().slice(0, 10);
}
