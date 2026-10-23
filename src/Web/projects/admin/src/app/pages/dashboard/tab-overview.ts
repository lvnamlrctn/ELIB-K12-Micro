import { Component, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { TranslateService } from '@ngx-translate/core';
import { Session } from '../../core/session';
import { CirculationDashboard, DigitalDashboard, HoldingsDashboard, PatronDashboard, PortalDashboard } from './dash-types';
import { ChartView, Kpi, PALETTE, Panel, RankList, Source, dashboardSource } from './dash-kit';

/**
 * Tab Tổng quan (lãnh đạo): quy mô kho, xu hướng mượn/đọc/bổ sung 12 tháng, truy cập portal, xếp hạng mượn nhiều.
 * Số liệu đến từ nhiều service (holdings, digital, circulation, portal, patron) — mỗi khối chỉ nạp khi người dùng có quyền xem nguồn của nó và có
 * license phân hệ; một service lỗi chỉ làm khối của nó báo lỗi.
 */
@Component({
  selector: 'app-tab-overview',
  imports: [ChartView, Kpi, Panel, RankList],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-5 gap-5 mb-5">
      <app-kpi icon="collections_bookmark" tint="bg-blue-50 text-blue-600" label="DASH.OV.TOTAL" sub="DASH.OV.TOTAL_SUB" [value]="totalTitles()" [loading]="kpiLoading()" [error]="kpiError()" />
      <app-kpi icon="cloud_done" tint="bg-emerald-50 text-emerald-600" label="DASH.OV.DIGITAL" sub="DASH.OV.DIGITAL_SUB" [value]="digital?.data()?.ebooks" [loading]="digital?.loading() ?? false" [error]="digital?.error() ?? false" />
      <app-kpi icon="menu_book" tint="bg-indigo-50 text-indigo-600" label="DASH.OV.PRINT" sub="DASH.OV.PRINT_SUB" [value]="holdings?.data()?.titles" [loading]="holdings?.loading() ?? false" [error]="holdings?.error() ?? false" [link]="session.can('CATALOG_BIBS:view') ? '/catalog-bibs' : null" />
      <app-kpi icon="donut_large" tint="bg-amber-50 text-amber-600" label="DASH.OV.RATIO" sub="DASH.OV.RATIO_SUB" suffix="%" [value]="digitalRatio()" [loading]="kpiLoading()" [error]="kpiError()" />
      <app-kpi icon="warning" tint="bg-red-50 text-red-600" label="DASH.OV.OVERDUE" sub="DASH.OV.OVERDUE_SUB" [value]="circ?.data()?.overdueReaders" [danger]="(circ?.data()?.overdueReaders ?? 0) > 0"
        [loading]="circ?.loading() ?? false" [error]="circ?.error() ?? false" [link]="session.can('CIRC_REPORT:view') ? '/circulation-report' : null" />
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-3 gap-5 mb-5">
      <app-kpi icon="person_check" tint="bg-emerald-50 text-emerald-600" label="DASH.OV.ACTIVE_RATE" sub="DASH.OV.ACTIVE_RATE_SUB" suffix="%" [value]="activeRate()" [loading]="rateLoading()" [error]="rateError()" />
      <app-kpi icon="autorenew" tint="bg-blue-50 text-blue-600" label="DASH.OV.TURNOVER" sub="DASH.OV.TURNOVER_SUB" [value]="turnover()" [loading]="turnoverLoading()" [error]="turnoverError()" />
      <app-kpi icon="public" tint="bg-sky-50 text-sky-600" label="DASH.OV.VISITS" sub="DASH.OV.VISITS_SUB" [value]="portal?.data()?.visits?.total" [loading]="portal?.loading() ?? false" [error]="portal?.error() ?? false" />
    </div>

    @if (circ) {
      <div class="mb-5">
        <app-panel title="DASH.CIRC.DAILY" sub="DASH.CIRC.DAILY_SUB" icon="calendar_view_week" [loading]="circ.loading()" [error]="circ.error()" [empty]="!circ.data() || (circ.data()!.dailyBorrowed + circ.data()!.dailyReturned) === 0" (retry)="circ.load()">
          <app-chart type="bar" [labels]="dailyLabels()" [series]="dailySeries()" />
        </app-panel>
      </div>
    }

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-5 mb-5">
      @if (circ) {
        <app-panel title="DASH.OV.TREND" sub="DASH.LAST_12_MONTHS" icon="trending_up" [loading]="circ.loading()" [error]="circ.error()" [empty]="!circ.data() || circ.data()!.trend.total === 0" (retry)="circ.load()">
          <app-chart type="line" [labels]="labels(circ.data()?.trend)" [series]="[{ labelKey: 'DASH.OV.TREND', data: values(circ.data()?.trend), color: PALETTE.blue }]" />
        </app-panel>
      }
      @if (digital) {
        <app-panel title="DASH.OV.READS" sub="DASH.LAST_12_MONTHS" icon="auto_stories" iconColor="text-sky-500" [loading]="digital.loading()" [error]="digital.error()" [empty]="!digital.data() || digital.data()!.readsTrend.total === 0" (retry)="digital.load()">
          <app-chart type="line" [labels]="labels(digital.data()?.readsTrend)" [series]="[{ labelKey: 'DASH.OV.READS', data: values(digital.data()?.readsTrend), color: PALETTE.sky }]" />
        </app-panel>
      }
      @if (holdings) {
        <app-panel title="DASH.OV.PRINT_ADDED" sub="DASH.LAST_12_MONTHS" icon="library_add" iconColor="text-indigo-500" [loading]="holdings.loading()" [error]="holdings.error()" [empty]="!holdings.data() || holdings.data()!.printAdded.total === 0" (retry)="holdings.load()">
          <app-chart type="bar" [labels]="labels(holdings.data()?.printAdded)" [series]="[{ labelKey: 'DASH.OV.PRINT_ADDED', data: values(holdings.data()?.printAdded), color: PALETTE.indigo }]" />
        </app-panel>
      }
      @if (digital) {
        <app-panel title="DASH.OV.DIGITAL_ADDED" sub="DASH.LAST_12_MONTHS" icon="cloud_upload" iconColor="text-emerald-500" [loading]="digital.loading()" [error]="digital.error()" [empty]="!digital.data() || digital.data()!.addedTrend.total === 0" (retry)="digital.load()">
          <app-chart type="bar" [labels]="labels(digital.data()?.addedTrend)" [series]="[{ labelKey: 'DASH.OV.DIGITAL_ADDED', data: values(digital.data()?.addedTrend), color: PALETTE.green }]" />
        </app-panel>
      }
      @if (portal) {
        <app-panel title="DASH.OV.VISIT_TREND" sub="DASH.LAST_12_MONTHS" icon="public" iconColor="text-amber-500" [loading]="portal.loading()" [error]="portal.error()" [empty]="!portal.data() || portal.data()!.trend.total === 0" (retry)="portal.load()">
          <app-chart type="line" [labels]="labels(portal.data()?.trend)" [series]="[{ labelKey: 'DASH.OV.VISIT_TREND', data: values(portal.data()?.trend), color: PALETTE.amber }]" />
        </app-panel>
      }
      @if (holdings) {
        <app-panel title="DASH.OV.DISTRIBUTION" sub="DASH.OV.DISTRIBUTION_SUB" icon="donut_large" iconColor="text-violet-500" [loading]="holdings.loading()" [error]="holdings.error()" [empty]="!holdings.data()?.distribution?.length" (retry)="holdings.load()">
          <app-chart type="doughnut" [labels]="distLabels()" [series]="distSeries()" />
        </app-panel>
      }
    </div>

    @if (circ) {
      <app-panel title="DASH.CIRC.TOP" sub="DASH.LAST_12_MONTHS" icon="emoji_events" iconColor="text-amber-500" [loading]="circ.loading()" [error]="circ.error()" [empty]="!circ.data()?.topBorrowed?.length" [height]="160" (retry)="circ.load()">
        <app-rank-list [rows]="topRows()" />
      </app-panel>
    }
  `,
})
export class TabOverview {
  protected readonly PALETTE = PALETTE;
  protected readonly session = inject(Session);
  private readonly http = inject(HttpClient);
  private readonly translate = inject(TranslateService);
  private readonly modules = this.session.features()?.modules ?? [];

  private allowed(module: string, perm: string): boolean { return this.modules.includes(module) && this.session.can(perm); }

  protected readonly holdings: Source<HoldingsDashboard> | null = this.allowed('HOLDINGS', 'STORE_BOOK_REPORT:view') ? dashboardSource(this.http, 'holdings') : null;
  protected readonly digital: Source<DigitalDashboard> | null = this.allowed('DIGITAL', 'DIGITAL_ITEMS:view') ? dashboardSource(this.http, 'digital') : null;
  protected readonly circ: Source<CirculationDashboard> | null = this.allowed('CIRCULATION', 'CIRC_REPORT:view') ? dashboardSource(this.http, 'circulation') : null;
  protected readonly portal: Source<PortalDashboard> | null = this.allowed('PORTAL', 'PORTAL_COUNTER:view') ? dashboardSource(this.http, 'portal') : null;
  protected readonly patron: Source<PatronDashboard> | null = this.session.can('READERS:view') ? dashboardSource(this.http, 'patron') : null;

  /** Thẻ tổng cần cả hai nguồn (in + số): thiếu quyền một nguồn thì chỉ cộng nguồn còn lại. */
  protected readonly kpiLoading = computed(() => (this.holdings?.loading() ?? false) || (this.digital?.loading() ?? false));
  protected readonly kpiError = computed(() => (this.holdings?.error() ?? false) || (this.digital?.error() ?? false));
  protected readonly totalTitles = computed(() => (this.holdings || this.digital) ? (this.holdings?.data()?.titles ?? 0) + (this.digital?.data()?.ebooks ?? 0) : null);
  protected readonly digitalRatio = computed(() => {
    const total = this.totalTitles();
    const digital = this.digital?.data()?.ebooks;
    return total && digital !== undefined && this.holdings ? Math.round((digital / total) * 100) : null;
  });

  /** Bạn đọc hoạt động = người khác nhau có mượn 12 tháng / bạn đọc chưa khoá (lưu thông + bạn đọc). */
  protected readonly rateLoading = computed(() => (this.circ?.loading() ?? false) || (this.patron?.loading() ?? false));
  protected readonly rateError = computed(() => (this.circ?.error() ?? false) || (this.patron?.error() ?? false));
  protected readonly activeRate = computed(() => {
    const active = this.circ?.data()?.activeReaders;
    const r = this.patron?.data()?.readers;
    const base = r ? r.total - r.locked : 0;
    return active === undefined || !base ? null : Math.min(100, Math.round((active / base) * 100));
  });

  /** Vòng quay tài liệu = lượt mượn 12 tháng / số bản trong kho (lưu thông + kho). */
  protected readonly turnoverLoading = computed(() => (this.circ?.loading() ?? false) || (this.holdings?.loading() ?? false));
  protected readonly turnoverError = computed(() => (this.circ?.error() ?? false) || (this.holdings?.error() ?? false));
  protected readonly turnover = computed(() => {
    const loans = this.circ?.data()?.loans12Months;
    const copies = this.holdings?.data()?.copies;
    return loans === undefined || !copies ? null : Math.round((loans / copies) * 10) / 10;
  });

  protected labels(s: { points: { label: string }[] } | null | undefined): string[] { return s?.points.map((p) => p.label) ?? []; }
  protected values(s: { points: { value: number }[] } | null | undefined): number[] { return s?.points.map((p) => p.value) ?? []; }

  protected readonly dailyLabels = computed(() => this.circ?.data()?.daily.map((p) => p.label) ?? []);
  protected readonly dailySeries = computed(() => [
    { labelKey: 'DASH.CIRC.BORROWED', data: this.circ?.data()?.daily.map((p) => p.borrowed) ?? [], color: PALETTE.blue },
    { labelKey: 'DASH.CIRC.RETURNED', data: this.circ?.data()?.daily.map((p) => p.returned) ?? [], color: PALETTE.green },
  ]);
  protected readonly distLabels = computed(() => this.holdings?.data()?.distribution.map((d) => (d.other ? this.translate.instant('DASH.OV.OTHER') : d.name || this.translate.instant('DASH.OV.UNCLASSIFIED'))) ?? []);
  protected readonly distSeries = computed(() => [{ labelKey: 'DASH.OV.DISTRIBUTION', data: this.holdings?.data()?.distribution.map((d) => d.count) ?? [], color: PALETTE.violet }]);
  protected readonly topRows = computed(() => (this.circ?.data()?.topBorrowed ?? []).map((t) => ({ key: t.mfn, name: t.title, note: t.author, value: t.count })));
}
