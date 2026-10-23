import { Component, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Session } from '../../core/session';
import { AcquisitionDashboard, HoldingsDashboard } from './dash-types';
import { ChartView, Kpi, PALETTE, Panel, dashboardSource } from './dash-kit';

const ORDER_KEYS: Record<number, string> = { 1: 'DASH.ACQ.ORDER_1', 2: 'DASH.ACQ.ORDER_2', 3: 'DASH.ACQ.ORDER_3' };
const RECEIPT_KEYS: Record<number, string> = { 0: 'DASH.ACQ.RECEIPT_0', 1: 'DASH.ACQ.RECEIPT_1' };

/**
 * Tab Bổ sung & Kho: tiến độ đơn đặt/phiếu nhập theo trạng thái, ngân sách so với chi tiêu theo quỹ (acquisition), mức lấp đầy kho (holdings).
 * Chưa có: số dòng phiếu nhập chờ gán mã vạch (cần thêm API đếm ở holdings) và đề xuất thanh lý chi tiết.
 */
@Component({
  selector: 'app-tab-acquisition',
  imports: [ChartView, Kpi, Panel, DecimalPipe, TranslatePipe],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-5 mb-5">
      <app-kpi icon="shopping_cart" tint="bg-blue-50 text-blue-600" label="DASH.ACQ.ORDERS" [value]="a()?.orderProgress?.orderTotal" [loading]="acq.loading()" [error]="acq.error()" [link]="session.can('AB_ORDERS:view') ? '/ab-orders' : null" />
      <app-kpi icon="inventory" tint="bg-emerald-50 text-emerald-600" label="DASH.ACQ.RECEIPTS" [value]="a()?.orderProgress?.receiptTotal" [loading]="acq.loading()" [error]="acq.error()" [link]="session.can('AB_RECEIPTS:view') ? '/ab-receipts' : null" />
      <app-kpi icon="account_balance_wallet" tint="bg-indigo-50 text-indigo-600" label="DASH.ACQ.BUDGET" [value]="a()?.budgetVsSpend?.totalBudget" [loading]="acq.loading()" [error]="acq.error()" />
      <app-kpi icon="payments" tint="bg-amber-50 text-amber-600" label="DASH.ACQ.SPEND" [value]="a()?.budgetVsSpend?.totalSpend" [loading]="acq.loading()" [error]="acq.error()" />
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-5 mb-5">
      <app-panel title="DASH.ACQ.BUDGET_VS_SPEND" sub="DASH.ACQ.BUDGET_VS_SPEND_SUB" icon="stacked_bar_chart" iconColor="text-indigo-500" [loading]="acq.loading()" [error]="acq.error()" [empty]="!a()?.budgetVsSpend?.funds?.length" (retry)="acq.load()">
        <app-chart type="bar" [labels]="fundLabels()" [series]="fundSeries()" />
      </app-panel>
      <app-panel title="DASH.ACQ.PROGRESS" icon="donut_large" iconColor="text-blue-500" [loading]="acq.loading()" [error]="acq.error()" [empty]="!a() || (a()!.orderProgress.orderTotal + a()!.orderProgress.receiptTotal) === 0" (retry)="acq.load()">
        <app-chart type="bar" [labels]="progressLabels()" [series]="progressSeries()" />
      </app-panel>
    </div>

    @if (holdings) {
      <app-panel title="DASH.ACQ.STORE_FILL" sub="DASH.ACQ.STORE_FILL_SUB" icon="warehouse" iconColor="text-emerald-600" [loading]="holdings.loading()" [error]="holdings.error()" [empty]="!holdings.data()?.stores?.length" [height]="120" (retry)="holdings.load()">
        <ul class="space-y-3">
          @for (s of holdings.data()?.stores ?? []; track s.storeId) {
            <li>
              <div class="flex items-center justify-between text-sm"><span class="font-semibold text-slate-700">{{ s.name }}</span>
                <span class="text-slate-500">{{ s.count | number }}@if (s.capacity) { / {{ s.capacity | number }} ({{ s.percent }}%) } @else { <span class="text-xs text-slate-400"> · {{ 'DASH.ACQ.NO_CAPACITY' | translate }}</span> }</span></div>
              @if (s.capacity) { <div class="mt-1 h-1.5 rounded-full bg-slate-100"><div class="h-1.5 rounded-full" [class]="(s.percent ?? 0) >= 90 ? 'bg-red-500' : 'bg-emerald-500'" [style.width.%]="Math.min(100, s.percent ?? 0)"></div></div> }
            </li>
          }
        </ul>
      </app-panel>
    }
  `,
})
export class TabAcquisition {
  protected readonly Math = Math;
  protected readonly session = inject(Session);
  private readonly http = inject(HttpClient);
  private readonly translate = inject(TranslateService);
  protected readonly acq = dashboardSource<AcquisitionDashboard>(this.http, 'acquisition');
  protected readonly holdings = (this.session.features()?.modules ?? []).includes('HOLDINGS') && this.session.can('STORE_BOOK_REPORT:view')
    ? dashboardSource<HoldingsDashboard>(this.http, 'holdings') : null;
  protected readonly a = this.acq.data;

  protected readonly fundLabels = computed(() => this.a()?.budgetVsSpend.funds.map((f) => f.name) ?? []);
  protected readonly fundSeries = computed(() => [
    { labelKey: 'DASH.ACQ.SPEND', data: this.a()?.budgetVsSpend.funds.map((f) => f.spend) ?? [], color: PALETTE.indigo },
    { labelKey: 'DASH.ACQ.BUDGET', data: this.a()?.budgetVsSpend.funds.map((f) => f.budget) ?? [], color: PALETTE.red, kind: 'line' as const, dashed: true },
  ]);
  /** Nhãn trạng thái dịch theo mã (server chỉ trả nhãn tiếng Việt cố định). */
  protected readonly progressLabels = computed(() => {
    const p = this.a()?.orderProgress;
    return p ? [...p.ordersByStatus.map((s) => this.translate.instant(ORDER_KEYS[s.status] ?? 'DASH.ACQ.ORDERS')), ...p.receiptsByStatus.map((s) => this.translate.instant(RECEIPT_KEYS[s.status] ?? 'DASH.ACQ.RECEIPTS'))] : [];
  });
  protected readonly progressSeries = computed(() => {
    const p = this.a()?.orderProgress;
    return [{ labelKey: 'DASH.ACQ.COUNT', data: p ? [...p.ordersByStatus.map((s) => s.count), ...p.receiptsByStatus.map((s) => s.count)] : [], color: PALETTE.blue }];
  });
}
