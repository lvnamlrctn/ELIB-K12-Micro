import { Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { Session } from '../../core/session';
import { SpaceDashboard } from './dash-types';
import { Kpi, Panel, dashboardSource } from './dash-kit';

/** Tab Không gian & Phòng học: phòng đang có người, phòng trống, tỉ lệ không tới (30 ngày), yêu cầu đặt chờ duyệt. */
@Component({
  selector: 'app-tab-space',
  imports: [Kpi, Panel, DecimalPipe, TranslatePipe],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-5 mb-5">
      <app-kpi icon="meeting_room" tint="bg-blue-50 text-blue-600" label="DASH.SPACE.OCCUPIED" [value]="d()?.occupancyNow?.occupied" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="door_open" tint="bg-emerald-50 text-emerald-600" label="DASH.SPACE.FREE" [value]="d()?.occupancyNow?.free" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="person_off" tint="bg-amber-50 text-amber-600" label="DASH.SPACE.NO_SHOW_RATE" sub="DASH.LAST_30_DAYS" suffix="%" [value]="d()?.noShow30?.ratePercent" [loading]="src.loading()" [error]="src.error()" />
      <app-kpi icon="pending_actions" tint="bg-red-50 text-red-600" label="DASH.SPACE.PENDING" [value]="d()?.pendingRequests" [danger]="(d()?.pendingRequests ?? 0) > 0" [loading]="src.loading()" [error]="src.error()" [link]="session.can('SPACE_BOOKINGS:view') ? '/space-bookings' : null" />
    </div>
    <app-panel title="DASH.SPACE.NO_SHOW" sub="DASH.LAST_30_DAYS" icon="person_off" iconColor="text-amber-500" [loading]="src.loading()" [error]="src.error()" [empty]="!d() || d()!.noShow30.total === 0" [height]="80" (retry)="src.load()">
      <p class="text-sm text-slate-600">{{ 'DASH.SPACE.NO_SHOW_DETAIL' | translate: { noShow: d()?.noShow30?.noShow, total: d()?.noShow30?.total } }}</p>
      <p class="text-3xl font-extrabold text-slate-800 mt-2">{{ d()?.noShow30?.ratePercent | number }}%</p>
    </app-panel>
  `,
})
export class TabSpace {
  protected readonly session = inject(Session);
  protected readonly src = dashboardSource<SpaceDashboard>(inject(HttpClient), 'space');
  protected readonly d = this.src.data;
}
