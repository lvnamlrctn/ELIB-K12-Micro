import { Component, ElementRef, OnDestroy, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Chart, ChartConfiguration, ChartDataset, registerables } from 'chart.js';
import { Subscription, firstValueFrom } from 'rxjs';

Chart.register(...registerables);

/**
 * Một nguồn số liệu của bảng điều khiển (một service). Mỗi nguồn có trạng thái đang tải / lỗi / dữ liệu riêng: service chết thì chỉ khối của nó
 * báo lỗi kèm nút thử lại, không làm trắng cả trang. Không có dữ liệu giả — lỗi thì hiện lỗi.
 */
export class Source<T> {
  readonly data = signal<T | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);

  constructor(private readonly fetch: () => Promise<T>) {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    try {
      this.data.set(await this.fetch());
    } catch {
      this.data.set(null);
      this.error.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}

/** Gọi GET /api/admin/<service>/dashboard của từng service. */
export function dashboardSource<T>(http: HttpClient, service: string): Source<T> {
  return new Source<T>(() => firstValueFrom(http.get<T>(`/api/admin/${service}/dashboard`)));
}

export const PALETTE = { blue: '#3b82f6', green: '#10b981', indigo: '#6366f1', amber: '#f59e0b', red: '#ef4444', sky: '#0ea5e9', violet: '#8b5cf6', slate: '#64748b' } as const;

export interface ChartSeries {
  /** Khoá dịch của chú giải. */
  labelKey: string;
  data: number[];
  color: string;
  /** Mặc định theo kiểu của biểu đồ; 'line' trong biểu đồ cột = đường tham chiếu (ví dụ ngân sách). */
  kind?: 'bar' | 'line';
  dashed?: boolean;
}

const alpha = (hex: string, a: number): string => {
  const n = parseInt(hex.slice(1), 16);
  return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${a})`;
};

/** Biểu đồ Chart.js bọc thành component: dựng lại khi dữ liệu hoặc ngôn ngữ đổi, huỷ khi rời trang. */
@Component({
  selector: 'app-chart',
  template: `<div class="relative" [style.height.px]="height()"><canvas #cv></canvas></div>`,
})
export class ChartView implements OnDestroy {
  private readonly translate = inject(TranslateService);
  readonly type = input<'bar' | 'line' | 'doughnut'>('bar');
  readonly labels = input.required<string[]>();
  readonly series = input.required<ChartSeries[]>();
  readonly height = input(256);
  /** Cột chồng (stacked) — không dùng cho doughnut. */
  readonly stacked = input(false);
  /** Nhãn trục/chú giải đã là chữ hiển thị (không phải khoá dịch) — dùng cho series.labelKey khi false. */
  readonly rawLabels = input(false);

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('cv');
  private chart: Chart | null = null;
  private readonly lang = signal(0);
  private readonly langSub: Subscription = this.translate.onLangChange.subscribe(() => this.lang.update((n) => n + 1));

  constructor() {
    effect(() => {
      this.lang();
      const type = this.type();
      const labels = this.labels();
      const series = this.series();
      const stacked = this.stacked();
      untracked(() => this.render(type, labels, series, stacked));
    });
  }

  ngOnDestroy(): void {
    this.langSub.unsubscribe();
    this.chart?.destroy();
    this.chart = null;
  }

  private render(type: 'bar' | 'line' | 'doughnut', labels: string[], series: ChartSeries[], stacked: boolean): void {
    this.chart?.destroy();
    const ctx = this.canvas().nativeElement.getContext('2d');
    if (!ctx) return;
    const name = (s: ChartSeries) => (this.rawLabels() ? s.labelKey : this.translate.instant(s.labelKey));
    let config: ChartConfiguration;
    if (type === 'doughnut') {
      const palette = [PALETTE.blue, PALETTE.green, PALETTE.amber, PALETTE.indigo, PALETTE.sky, PALETTE.violet, PALETTE.red, PALETTE.slate];
      config = {
        type: 'doughnut',
        data: { labels, datasets: [{ data: series[0]?.data ?? [], backgroundColor: labels.map((_, i) => palette[i % palette.length]), borderWidth: 0 }] },
        options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { usePointStyle: true, boxWidth: 8, font: { size: 11 }, color: '#64748b' } } } },
      };
    } else {
      const datasets = series.map((s): ChartDataset => {
        const kind = s.kind ?? type;
        if (kind === 'line') {
          const area = ctx.createLinearGradient(0, 0, 0, 260);
          area.addColorStop(0, alpha(s.color, 0.22));
          area.addColorStop(1, alpha(s.color, 0));
          return {
            type: 'line', label: name(s), data: s.data, borderColor: s.color, backgroundColor: s.dashed ? 'transparent' : area, borderWidth: s.dashed ? 2 : 2.5,
            borderDash: s.dashed ? [6, 4] : [], fill: !s.dashed, tension: 0.35, pointRadius: 3, pointBackgroundColor: '#ffffff', pointBorderColor: s.color, pointBorderWidth: 2,
          } as ChartDataset;
        }
        return { type: 'bar', label: name(s), data: s.data, backgroundColor: alpha(s.color, 0.85), hoverBackgroundColor: s.color, borderRadius: 4, maxBarThickness: 28 } as ChartDataset;
      });
      config = {
        type: type === 'line' ? 'line' : 'bar',
        data: { labels, datasets },
        options: {
          responsive: true, maintainAspectRatio: false, interaction: { mode: 'index', intersect: false },
          plugins: {
            legend: { display: series.length > 1 || this.rawLabels(), position: 'bottom', align: 'start', labels: { usePointStyle: true, pointStyle: 'circle', boxWidth: 8, font: { size: 11 }, color: '#64748b' } },
            tooltip: { backgroundColor: '#0f172a', padding: 10 },
          },
          scales: {
            x: { stacked, grid: { display: false }, border: { display: false }, ticks: { color: '#94a3b8', font: { size: 11 }, maxRotation: 0, autoSkip: true } },
            y: { stacked, beginAtZero: true, grid: { color: '#f1f5f9' }, border: { display: false }, ticks: { color: '#94a3b8', font: { size: 11 }, precision: 0, maxTicksLimit: 5 } },
          },
        },
      };
    }
    this.chart = new Chart(ctx, config);
  }
}

/** Thẻ số liệu (KPI). */
@Component({
  selector: 'app-kpi',
  imports: [DecimalPipe, RouterLink, TranslatePipe],
  template: `
    <div class="bg-white rounded-2xl border border-slate-200 shadow-xs p-5 flex items-center gap-4 hover:shadow-md transition-shadow h-full">
      <div class="w-12 h-12 rounded-2xl flex items-center justify-center shrink-0" [class]="tint()"><span class="material-icons text-[22px]">{{ icon() }}</span></div>
      <div class="min-w-0">
        <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider truncate">{{ label() | translate }}</p>
        @if (loading()) {
          <span class="inline-block mt-1 h-7 w-20 rounded bg-slate-100 animate-pulse"></span>
        } @else if (error() || value() === null || value() === undefined) {
          <p class="text-2xl font-extrabold leading-tight text-slate-300">—</p>
        } @else {
          <p class="text-2xl font-extrabold leading-tight" [class]="danger() ? 'text-red-600' : 'text-slate-800'">{{ value() | number }}{{ suffix() }}</p>
        }
        @if (sub()) { <p class="text-[11px] text-slate-500 truncate">{{ sub()! | translate }}</p> }
        @if (link()) { <a [routerLink]="link()" class="text-sm text-blue-700 underline">{{ 'DASH.OPEN_LIST' | translate }}</a> }
      </div>
    </div>
  `,
})
export class Kpi {
  readonly icon = input.required<string>();
  readonly tint = input('bg-blue-50 text-blue-600');
  readonly label = input.required<string>();
  readonly value = input<number | null | undefined>(null);
  readonly suffix = input('');
  readonly sub = input<string | null>(null);
  readonly link = input<string | null>(null);
  readonly loading = input(false);
  readonly error = input(false);
  /** Tô đỏ khi giá trị > 0 (cảnh báo như bạn đọc quá hạn). */
  readonly danger = input(false);
}

/** Khung một khối biểu đồ/bảng: tiêu đề, trạng thái đang tải/lỗi/rỗng, nút thử lại. */
@Component({
  selector: 'app-panel',
  imports: [TranslatePipe, RouterLink],
  template: `
    <section class="bg-white rounded-2xl border border-slate-200 shadow-xs p-5 h-full">
      <div class="flex items-start justify-between gap-4 mb-4">
        <div class="min-w-0">
          <h5 class="text-base font-bold text-slate-800 flex items-center gap-2"><span class="material-icons text-[18px]" [class]="iconColor()">{{ icon() }}</span>{{ title() | translate }}</h5>
          @if (sub()) { <p class="text-xs text-slate-400 mt-0.5">{{ sub()! | translate }}</p> }
        </div>
        @if (link()) { <a [routerLink]="link()" class="shrink-0 text-[11px] font-bold text-slate-500 hover:text-slate-700 uppercase tracking-wider">{{ 'DASH.OPEN_LIST' | translate }}</a> }
      </div>
      @if (loading()) {
        <div class="rounded-xl bg-slate-50 animate-pulse" [style.height.px]="height()"></div>
      } @else if (error()) {
        <div class="flex flex-col items-center justify-center text-center text-slate-400 gap-2" [style.height.px]="height()">
          <span class="material-icons text-3xl text-red-300">error_outline</span>
          <p class="text-xs">{{ 'DASH.LOAD_ERROR' | translate }}</p>
          <button type="button" class="px-3 py-1 text-xs rounded-lg border border-slate-300 text-slate-700 hover:bg-slate-50" (click)="retry.emit()">{{ 'DASH.RETRY' | translate }}</button>
        </div>
      } @else if (empty()) {
        <div class="flex flex-col items-center justify-center text-center text-slate-400" [style.height.px]="height()">
          <span class="material-icons text-4xl mb-2">{{ icon() }}</span>
          <p class="text-xs">{{ 'DASH.EMPTY' | translate }}</p>
        </div>
      } @else {
        <ng-content />
      }
    </section>
  `,
})
export class Panel {
  readonly title = input.required<string>();
  readonly icon = input('insights');
  readonly iconColor = input('text-blue-600');
  readonly sub = input<string | null>(null);
  readonly link = input<string | null>(null);
  readonly loading = input(false);
  readonly error = input(false);
  readonly empty = input(false);
  readonly height = input(256);
  readonly retry = output<void>();
}

/** Thanh ngang tỉ lệ cho bảng xếp hạng (top mượn nhiều, kho dùng nhiều…). */
@Component({
  selector: 'app-rank-list',
  imports: [DecimalPipe],
  template: `
    <ol class="space-y-2.5">
      @for (r of rows(); track r.key; let i = $index) {
        <li>
          <div class="flex items-center justify-between gap-3 text-sm">
            <span class="min-w-0 truncate"><span class="text-slate-400 font-bold mr-2">{{ i + 1 }}</span><span class="font-semibold text-slate-700">{{ r.name }}</span>@if (r.note) { <span class="text-xs text-slate-400 ml-2">{{ r.note }}</span> }</span>
            <span class="font-extrabold text-slate-800 shrink-0">{{ r.value | number }}</span>
          </div>
          <div class="mt-1 h-1.5 rounded-full bg-slate-100"><div class="h-1.5 rounded-full bg-blue-500" [style.width.%]="width(r.value)"></div></div>
        </li>
      }
    </ol>
  `,
})
export class RankList {
  readonly rows = input.required<{ key: string | number; name: string; note?: string | null; value: number }[]>();
  private max(): number { return Math.max(0, ...this.rows().map((r) => r.value)); }
  protected width(v: number): number { const m = this.max(); return m <= 0 ? 0 : Math.max(6, Math.round((v / m) * 100)); }
}
