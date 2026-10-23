import { Component, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Session } from '../../core/session';
import { CatalogDashboard } from './dash-types';
import { ChartView, Kpi, PALETTE, Panel, dashboardSource } from './dash-kit';

/** Tab Biên mục: số biểu ghi, biểu ghi mới theo tuần, biểu ghi còn thiếu dữ liệu quan trọng (tác giả, DDC, ảnh bìa). */
@Component({
  selector: 'app-tab-cataloging',
  imports: [ChartView, Kpi, Panel],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-5 mb-5">
      <app-kpi icon="menu_book" tint="bg-indigo-50 text-indigo-600" label="DASH.CAT.BIBS" [value]="d()?.bibs" [loading]="src.loading()" [error]="src.error()" [link]="session.can('CATALOG_BIBS:view') ? '/catalog-bibs' : null" />
      <app-kpi icon="person_off" tint="bg-amber-50 text-amber-600" label="DASH.CAT.MISSING_AUTHOR" sub="DASH.CAT.MISSING_SUB" [value]="d()?.missingAuthor" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="tag" tint="bg-amber-50 text-amber-600" label="DASH.CAT.MISSING_DDC" sub="DASH.CAT.MISSING_SUB" [value]="d()?.missingDdc" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="hide_image" tint="bg-amber-50 text-amber-600" label="DASH.CAT.MISSING_COVER" sub="DASH.CAT.MISSING_SUB" [value]="d()?.missingCover" [loading]="src.loading()" [error]="src.error()" />
    </div>
    <app-panel title="DASH.CAT.NEW_WEEKLY" sub="DASH.CAT.NEW_WEEKLY_SUB" icon="library_add" iconColor="text-violet-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d() || d()!.newTotal === 0" (retry)="src.load()">
      <app-chart type="bar" [labels]="labels()" [series]="series()" />
    </app-panel>
  `,
})
export class TabCataloging {
  protected readonly session = inject(Session);
  protected readonly src = dashboardSource<CatalogDashboard>(inject(HttpClient), 'catalog');
  protected readonly d = this.src.data;
  protected readonly labels = computed(() => this.d()?.newByWeek.map((w) => w.label) ?? []);
  protected readonly series = computed(() => [{ labelKey: 'DASH.CAT.NEW_RECORDS', data: this.d()?.newByWeek.map((w) => w.value) ?? [], color: PALETTE.violet }]);
}
