import { Component, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { Session } from '../../core/session';
import { CirculationDashboard } from './dash-types';
import { ChartView, Kpi, PALETTE, Panel, RankList, dashboardSource } from './dash-kit';

/** Tab Lưu thông: mượn/trả theo ngày và giờ, xu hướng 12 tháng, quá hạn, cơ cấu bạn đọc, kho dùng nhiều, tài liệu chưa từng mượn. */
@Component({
  selector: 'app-tab-circulation',
  imports: [ChartView, Kpi, Panel, RankList, DecimalPipe, TranslatePipe],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-5 mb-5">
      <app-kpi icon="warning" tint="bg-red-50 text-red-600" label="DASH.CIRC.OVERDUE_READERS" [value]="d()?.overdueReaders" [danger]="(d()?.overdueReaders ?? 0) > 0"
        [loading]="src.loading()" [error]="src.error()" [link]="session.can('CIRC_REPORT:view') ? '/circulation-report' : null" />
      <app-kpi icon="sync_alt" tint="bg-blue-50 text-blue-600" label="DASH.CIRC.LOANS_12M" sub="DASH.LAST_12_MONTHS" [value]="d()?.loans12Months" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="groups" tint="bg-emerald-50 text-emerald-600" label="DASH.CIRC.ACTIVE_READERS" sub="DASH.LAST_12_MONTHS" [value]="d()?.activeReaders" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="inventory_2" tint="bg-amber-50 text-amber-600" label="DASH.CIRC.UNUSED_TITLES" sub="DASH.CIRC.UNUSED_SUB" [value]="d()?.unusedTotal" [loading]="src.loading()" [error]="src.error()" />
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-5 mb-5">
      <app-panel title="DASH.CIRC.DAILY" sub="DASH.CIRC.DAILY_SUB" icon="calendar_view_week" [loading]="src.loading()" [error]="src.error()" [empty]="!d() || (d()!.dailyBorrowed + d()!.dailyReturned) === 0" (retry)="src.load()">
        <app-chart type="bar" [labels]="dailyLabels()" [series]="dailySeries()" />
      </app-panel>
      <app-panel title="DASH.CIRC.HOURLY" sub="DASH.CIRC.HOURLY_SUB" icon="schedule" [loading]="src.loading()" [error]="src.error()" [empty]="!d() || (d()!.hourlyBorrowed + d()!.hourlyReturned) === 0" (retry)="src.load()">
        <app-chart type="bar" [labels]="hourLabels()" [series]="hourSeries()" />
      </app-panel>
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-5 mb-5">
      <app-panel title="DASH.CIRC.TREND" sub="DASH.LAST_12_MONTHS" icon="trending_up" [loading]="src.loading()" [error]="src.error()" [empty]="!d() || d()!.trend.total === 0" (retry)="src.load()">
        <app-chart type="line" [labels]="trendLabels()" [series]="trendSeries()" />
      </app-panel>
      <app-panel title="DASH.CIRC.OVERDUE_AGING" sub="DASH.CIRC.OVERDUE_AGING_SUB" icon="hourglass_bottom" iconColor="text-red-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d() || d()!.overdueLoans === 0" (retry)="src.load()">
        <app-chart type="bar" [labels]="agingLabels()" [series]="agingSeries()" />
      </app-panel>
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-3 gap-5 mb-5">
      <app-panel title="DASH.CIRC.TOP" sub="DASH.LAST_12_MONTHS" icon="emoji_events" iconColor="text-amber-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d()?.topBorrowed?.length" [height]="200" (retry)="src.load()">
        <app-rank-list [rows]="topRows()" />
      </app-panel>
      <app-panel title="DASH.CIRC.BORROWER_TYPES" sub="DASH.LAST_12_MONTHS" icon="groups" iconColor="text-indigo-600" [loading]="src.loading()" [error]="src.error()" [empty]="!d()?.borrowerTypes?.length" [height]="200" (retry)="src.load()">
        <app-chart type="doughnut" [labels]="typeLabels()" [series]="typeSeries()" [height]="240" />
      </app-panel>
      <app-panel title="DASH.CIRC.STORE_USAGE" sub="DASH.LAST_12_MONTHS" icon="warehouse" iconColor="text-emerald-600" [loading]="src.loading()" [error]="src.error()" [empty]="!d()?.storeUsage?.length" [height]="200" (retry)="src.load()">
        <app-rank-list [rows]="storeRows()" />
      </app-panel>
    </div>

    <app-panel title="DASH.CIRC.UNUSED" sub="DASH.CIRC.UNUSED_SUB" icon="inventory_2" iconColor="text-amber-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d()?.unused?.length" [height]="120" (retry)="src.load()">
      <div class="overflow-x-auto">
        <table class="w-full text-left text-sm">
          <thead><tr class="text-[11px] uppercase tracking-wider text-slate-400"><th class="py-2 pr-3">{{ 'DASH.TITLE_COL' | translate }}</th><th class="py-2 pr-3">{{ 'DASH.AUTHOR_COL' | translate }}</th><th class="py-2 pr-3">{{ 'DASH.STORE_COL' | translate }}</th><th class="py-2 text-right">{{ 'DASH.COPIES_COL' | translate }}</th></tr></thead>
          <tbody>
            @for (u of d()?.unused ?? []; track u.mfn) {
              <tr class="border-t border-slate-100"><td class="py-2 pr-3 font-semibold text-slate-700">{{ u.title }}</td><td class="py-2 pr-3 text-slate-500">{{ u.author }}</td><td class="py-2 pr-3 text-slate-500">{{ u.storeName }}</td><td class="py-2 text-right font-bold">{{ u.copies | number }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </app-panel>
  `,
})
export class TabCirculation {
  protected readonly session = inject(Session);
  protected readonly src = dashboardSource<CirculationDashboard>(inject(HttpClient), 'circulation');
  protected readonly d = this.src.data;

  protected readonly dailyLabels = computed(() => this.d()?.daily.map((p) => p.label) ?? []);
  protected readonly dailySeries = computed(() => [
    { labelKey: 'DASH.CIRC.BORROWED', data: this.d()?.daily.map((p) => p.borrowed) ?? [], color: PALETTE.blue },
    { labelKey: 'DASH.CIRC.RETURNED', data: this.d()?.daily.map((p) => p.returned) ?? [], color: PALETTE.green },
  ]);
  protected readonly hourLabels = computed(() => this.d()?.hourly.map((p) => p.label) ?? []);
  protected readonly hourSeries = computed(() => [
    { labelKey: 'DASH.CIRC.BORROWED', data: this.d()?.hourly.map((p) => p.borrowed) ?? [], color: PALETTE.blue },
    { labelKey: 'DASH.CIRC.RETURNED', data: this.d()?.hourly.map((p) => p.returned) ?? [], color: PALETTE.green },
  ]);
  protected readonly trendLabels = computed(() => this.d()?.trend.points.map((p) => p.label) ?? []);
  protected readonly trendSeries = computed(() => [{ labelKey: 'DASH.CIRC.BORROWED', data: this.d()?.trend.points.map((p) => p.value) ?? [], color: PALETTE.blue }]);
  protected readonly agingLabels = computed(() => this.d()?.overdueAging.map((b) => b.label) ?? []);
  protected readonly agingSeries = computed(() => [{ labelKey: 'DASH.CIRC.OVERDUE_LOANS', data: this.d()?.overdueAging.map((b) => b.count) ?? [], color: PALETTE.red }]);
  protected readonly typeLabels = computed(() => this.d()?.borrowerTypes.map((t) => t.name) ?? []);
  protected readonly typeSeries = computed(() => [{ labelKey: 'DASH.CIRC.BORROWED', data: this.d()?.borrowerTypes.map((t) => t.count) ?? [], color: PALETTE.indigo }]);
  protected readonly topRows = computed(() => (this.d()?.topBorrowed ?? []).map((t) => ({ key: t.mfn, name: t.title, note: t.author, value: t.count })));
  protected readonly storeRows = computed(() => (this.d()?.storeUsage ?? []).map((s) => ({ key: s.storeId ?? s.name, name: s.name, value: s.count })));
}
