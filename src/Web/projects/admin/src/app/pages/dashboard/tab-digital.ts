import { Component, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Session } from '../../core/session';
import { DigitalDashboard } from './dash-types';
import { ChartView, Kpi, PALETTE, Panel, RankList, dashboardSource } from './dash-kit';

/** Tab Tài liệu số: quy mô, lượt xem/tải, đọc theo tháng, tài liệu đọc nhiều (30 ngày), định dạng file, dung lượng khai báo. */
@Component({
  selector: 'app-tab-digital',
  imports: [ChartView, Kpi, Panel, RankList],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-5 mb-5">
      <app-kpi icon="cloud_done" tint="bg-emerald-50 text-emerald-600" label="DASH.DIG.EBOOKS" sub="DASH.DIG.PUBLISHED_SUB" [value]="d()?.ebooks" [loading]="src.loading()" [error]="src.error()" [link]="session.can('DIGITAL_ITEMS:view') ? '/digital-items' : null" />
      <app-kpi icon="visibility" tint="bg-blue-50 text-blue-600" label="DASH.DIG.VIEWS" [value]="d()?.totalViews" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="download" tint="bg-indigo-50 text-indigo-600" label="DASH.DIG.DOWNLOADS" [value]="d()?.totalDownloads" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="hard_drive" tint="bg-amber-50 text-amber-600" label="DASH.DIG.STORAGE" sub="DASH.DIG.STORAGE_SUB" [value]="storageMb()" suffix=" MB" [loading]="src.loading()" [error]="src.error()" />
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-5 mb-5">
      <app-panel title="DASH.DIG.TOP" sub="DASH.LAST_30_DAYS" icon="emoji_events" iconColor="text-amber-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d()?.topRead?.length" [height]="200" (retry)="src.load()">
        <app-rank-list [rows]="topRows()" />
      </app-panel>
      <app-panel title="DASH.DIG.FORMATS" icon="donut_large" iconColor="text-violet-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d()?.formatDistribution?.length" (retry)="src.load()">
        <app-chart type="doughnut" [labels]="formatLabels()" [series]="formatSeries()" />
      </app-panel>
      <app-panel title="DASH.DIG.READS" sub="DASH.LAST_12_MONTHS" icon="auto_stories" iconColor="text-sky-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d() || d()!.readsTrend.total === 0" (retry)="src.load()">
        <app-chart type="line" [labels]="readsLabels()" [series]="readsSeries()" />
      </app-panel>
      <app-panel title="DASH.DIG.ADDED" sub="DASH.LAST_12_MONTHS" icon="cloud_upload" iconColor="text-emerald-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d() || d()!.addedTrend.total === 0" (retry)="src.load()">
        <app-chart type="bar" [labels]="addedLabels()" [series]="addedSeries()" />
      </app-panel>
    </div>
  `,
})
export class TabDigital {
  protected readonly session = inject(Session);
  protected readonly src = dashboardSource<DigitalDashboard>(inject(HttpClient), 'digital');
  protected readonly d = this.src.data;

  protected readonly storageMb = computed(() => { const b = this.d()?.storage.totalBytes; return b === undefined ? null : Math.round(b / 1048576); });
  protected readonly topRows = computed(() => (this.d()?.topRead ?? []).map((t) => ({ key: t.ebookPublicId, name: t.title, note: t.author, value: t.count })));
  protected readonly formatLabels = computed(() => this.d()?.formatDistribution.map((f) => f.format.toUpperCase()) ?? []);
  protected readonly formatSeries = computed(() => [{ labelKey: 'DASH.DIG.FORMATS', data: this.d()?.formatDistribution.map((f) => f.count) ?? [], color: PALETTE.violet }]);
  protected readonly readsLabels = computed(() => this.d()?.readsTrend.points.map((p) => p.label) ?? []);
  protected readonly readsSeries = computed(() => [{ labelKey: 'DASH.DIG.READS', data: this.d()?.readsTrend.points.map((p) => p.value) ?? [], color: PALETTE.sky }]);
  protected readonly addedLabels = computed(() => this.d()?.addedTrend.points.map((p) => p.label) ?? []);
  protected readonly addedSeries = computed(() => [{ labelKey: 'DASH.DIG.ADDED', data: this.d()?.addedTrend.points.map((p) => p.value) ?? [], color: PALETTE.green }]);
}
