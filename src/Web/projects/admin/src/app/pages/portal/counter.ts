import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, PortalCounterDay, PortalCounterSummary, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Loading } from '../../shared/ui';

/** Bộ đếm truy cập OPAC (LRC: Counter, quyền PORTAL_COUNTER): tổng, hôm nay, 7 ngày và biểu đồ theo ngày lịch Việt Nam. Không lưu IP. */
@Component({
  selector: 'app-portal-counter',
  imports: [FormsModule, LDatePipe, LNumberPipe, Loading, TranslatePipe],
  template: `
    <div class="mb-5 flex items-end gap-3 flex-wrap">
      <h4 class="page-title">{{ 'PORTAL_COUNTER_PAGE.TITLE' | translate }}</h4>
      <div class="ml-auto flex items-end gap-2">
        <div>
          <label for="pc-days" class="form-label">{{ 'PORTAL_COUNTER_PAGE.RANGE' | translate }}</label>
          <select id="pc-days" class="input" [ngModel]="days()" (ngModelChange)="setDays($event)">
            <option [ngValue]="7">7</option><option [ngValue]="30">30</option><option [ngValue]="90">90</option><option [ngValue]="365">365</option>
          </select>
        </div>
      </div>
    </div>

    <div class="relative">
      @if (loading()) { <app-loading /> }
      @if (summary(); as s) {
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-x-4">
          <div class="panel"><div class="text-xs text-gray-500">{{ 'PORTAL_COUNTER_PAGE.TOTAL' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.total | lnumber }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'PORTAL_COUNTER_PAGE.TODAY' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.today | lnumber }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'PORTAL_COUNTER_PAGE.LAST7' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.last7Days | lnumber }}</div></div>
        </div>
      }
      <div class="panel">
        <div class="font-medium text-gray-800 mb-3">{{ 'PORTAL_COUNTER_PAGE.PER_DAY' | translate }} <span class="text-gray-500 font-normal">({{ rangeTotal() | lnumber }})</span></div>
        <div class="flex items-end gap-px h-40 overflow-x-auto" role="img" [attr.aria-label]="'PORTAL_COUNTER_PAGE.PER_DAY' | translate">
          @for (d of points(); track d.day) {
            <div class="flex-1 min-w-[4px] flex flex-col justify-end h-full" [title]="(d.day | ldate: 'dd/MM/yyyy') + ': ' + d.hits">
              <div class="bg-blue-500" [style.height.%]="(d.hits / maxHits()) * 100"></div>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class PortalCounterPage implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly loading = signal(false);
  protected readonly days = signal(30);
  protected readonly summary = signal<PortalCounterSummary | null>(null);
  protected readonly points = signal<PortalCounterDay[]>([]);
  protected readonly maxHits = computed(() => Math.max(1, ...this.points().map((p) => p.hits)));
  protected readonly rangeTotal = computed(() => this.points().reduce((sum, p) => sum + p.hits, 0));

  ngOnInit(): void { void this.load(); }

  protected setDays(value: number): void {
    this.days.set(value);
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [summary, points] = await Promise.all([this.api.portalCounter(), this.api.portalCounterDays(this.days())]);
      this.summary.set(summary);
      this.points.set(points);
    } catch (error) {
      this.toastr.error(errorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }
}
