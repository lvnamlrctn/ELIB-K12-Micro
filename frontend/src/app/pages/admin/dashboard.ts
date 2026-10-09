import { Component, AfterViewInit, ViewChild, ElementRef, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import Chart from 'chart.js/auto';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DashboardService, DashboardData, DashboardTypeDistributionItem, DashboardTopBorrowedItem, DashboardOverdueBucket, DashboardCatalogingData, DashboardAcquisitionData, DashboardDigitalData, DashboardSpaceData, DashboardDevOpsData } from '../../services/dashboard';
import { Auth } from '../../services/auth';
import { PermissionService } from '../../services/system/permission.service';
import { DataQualityService, QUALITY_RULES, QualitySummary } from '../../services/system/data-quality.service';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

/** Góc nhìn dashboard theo vai trò (Đợt 21 — port từ ELIB-LRC `dashboard-roles.ts`, bản 3 tab). Tab hiện theo quyền
 *  module đã tải, không suy ra từ tên chức danh; số liệu vẫn do API kiểm quyền DASHBOARD/view + đơn vị hiệu lực. */
export type DashboardRole = 'overview' | 'circulation' | 'cataloging' | 'acquisition' | 'digital' | 'space' | 'devops';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe, DatePipe, TranslateModule, RouterLink],
  templateUrl: './dashboard.html'
})
export class Dashboard implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('borrowTrendChart') borrowTrendChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('dailyTrendChart') dailyTrendChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('fundBudgetChart') fundBudgetChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('circDailyChart') circDailyChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('weeklyAddedChart') weeklyAddedChartRef?: ElementRef<HTMLCanvasElement>;

  private dashboardService = inject(DashboardService);
  private translate = inject(TranslateService);
  private permissions = inject(PermissionService);
  private dataQuality = inject(DataQualityService);
  private authService = inject(Auth);
  private sub?: Subscription;
  private langSub?: Subscription;

  dashboardData = signal<DashboardData | null>(null);
  loading = signal<boolean>(true);

  // ── Đợt 21: dashboard theo vai trò ──────────────────────────────────────────
  /** Tab được phép theo quyền đã tải; chưa có dữ liệu quyền thì chỉ Tổng quan (không mở tab khi thiếu quyền). */
  roles = computed<{ id: DashboardRole; label: string }[]>(() => {
    if (!this.permissions.ready()) return [{ id: 'overview', label: 'DASHBOARD.ROLE_OVERVIEW' }];
    const list: { id: DashboardRole; label: string }[] = [{ id: 'overview', label: 'DASHBOARD.ROLE_OVERVIEW' }];
    if (this.permissions.canView('/admin/circulation-report')) list.push({ id: 'circulation', label: 'DASHBOARD.ROLE_CIRCULATION' });
    if (this.permissions.canView('/admin/catalog-bibs')) list.push({ id: 'cataloging', label: 'DASHBOARD.ROLE_CATALOGING' });
    if (this.permissions.canView('/admin/ab-receipts')) list.push({ id: 'acquisition', label: 'DASHBOARD.ROLE_ACQUISITION' });
    if (this.permissions.canView('/admin/ebooks')) list.push({ id: 'digital', label: 'DASHBOARD.ROLE_DIGITAL' });
    if (this.permissions.canView('/admin/study-room-bookings')) list.push({ id: 'space', label: 'DASHBOARD.ROLE_SPACE' });
    // DevOps: chỉ tài khoản hệ thống (đúng SystemAdminOnly phía API) — không dựa vào quyền module, vì
    // module DASHBOARD/view chỉ kiểm tra được ở API, không phân biệt được "hệ thống hay đơn vị" ở đây.
    if (this.authService.isPrivileged()) list.push({ id: 'devops', label: 'DASHBOARD.ROLE_DEVOPS' });
    return list;
  });
  private selectedRole = signal<DashboardRole>('overview');
  /** Tab đang xem — rơi về Tổng quan nếu tab đã chọn không còn được phép. */
  role = computed<DashboardRole>(() => this.roles().some(r => r.id === this.selectedRole()) ? this.selectedRole() : 'overview');
  canOpenCircReport = computed(() => this.permissions.canView('/admin/circulation-report'));
  canOpenBibs = computed(() => this.permissions.canView('/admin/catalog-bibs'));

  /** Lượt mượn/trả hôm nay = điểm cuối của chuỗi 14 ngày (Summary.dailyTrend). */
  today = computed(() => {
    const list = this.dashboardData()?.dailyTrend ?? [];
    return list.length ? list[list.length - 1] : null;
  });
  dailyTotals = computed(() => (this.dashboardData()?.dailyTrend ?? []).reduce(
    (a, p) => ({ borrowed: a.borrowed + p.borrowed, returned: a.returned + p.returned }), { borrowed: 0, returned: 0 }));

  cataloging = signal<DashboardCatalogingData | null>(null);
  qualitySummary = signal<QualitySummary | null>(null);
  catalogingLoading = signal(false);
  catalogingError = signal(false);
  private catalogingLoaded = false;
  readonly qualityRules = QUALITY_RULES;
  qualityRulesVisible = computed(() => {
    const counts = this.qualitySummary()?.counts ?? {};
    return QUALITY_RULES.filter(r => counts[r.key] !== undefined);
  });
  qualityCount(key: string): number { return (this.qualitySummary()?.counts as Record<string, number> | undefined)?.[key] ?? 0; }
  private circDailyChartInstance?: Chart;
  private weeklyAddedChartInstance?: Chart;

  // Đợt 22.4
  acquisition = signal<DashboardAcquisitionData | null>(null);
  digital = signal<DashboardDigitalData | null>(null);
  space = signal<DashboardSpaceData | null>(null);
  devops = signal<DashboardDevOpsData | null>(null);
  acquisitionLoading = signal(false);
  digitalLoading = signal(false);
  spaceLoading = signal(false);
  devopsLoading = signal(false);
  private acquisitionLoaded = false;
  private digitalLoaded = false;
  private spaceLoaded = false;
  private devopsLoaded = false;

  selectRole(id: DashboardRole): void {
    this.selectedRole.set(id);
    if (id === 'cataloging') this.loadCataloging();
    if (id === 'acquisition') this.loadAcquisition();
    if (id === 'digital') this.loadDigital();
    if (id === 'space') this.loadSpace();
    if (id === 'devops') this.loadDevOps();
    // Canvas nằm trong khối [hidden] lúc khởi tạo có kích thước 0 — vẽ lại khi tab hiện ra.
    setTimeout(() => { this.circDailyChartInstance?.resize(); this.weeklyAddedChartInstance?.resize(); });
  }

  loadCataloging(force = false): void {
    if ((this.catalogingLoaded && !force) || this.catalogingLoading()) return;
    this.catalogingLoading.set(true);
    this.catalogingError.set(false);
    forkJoin({
      data: this.dashboardService.getCataloging(),
      quality: this.dataQuality.summary().pipe(catchError(() => of(null))),
    }).subscribe(({ data, quality }) => {
      this.catalogingLoading.set(false);
      this.catalogingLoaded = true;
      this.cataloging.set(data);
      this.qualitySummary.set(quality);
      this.catalogingError.set(!data);
      this.updateWeeklyAddedChart();
    });
  }

  loadAcquisition(force = false): void {
    if ((this.acquisitionLoaded && !force) || this.acquisitionLoading()) return;
    this.acquisitionLoading.set(true);
    this.dashboardService.getAcquisition().subscribe(data => {
      this.acquisitionLoading.set(false);
      this.acquisitionLoaded = true;
      this.acquisition.set(data);
    });
  }

  loadDigital(force = false): void {
    if ((this.digitalLoaded && !force) || this.digitalLoading()) return;
    this.digitalLoading.set(true);
    this.dashboardService.getDigital().subscribe(data => {
      this.digitalLoading.set(false);
      this.digitalLoaded = true;
      this.digital.set(data);
    });
  }

  loadSpace(force = false): void {
    if ((this.spaceLoaded && !force) || this.spaceLoading()) return;
    this.spaceLoading.set(true);
    this.dashboardService.getSpace().subscribe(data => {
      this.spaceLoading.set(false);
      this.spaceLoaded = true;
      this.space.set(data);
    });
  }

  loadDevOps(force = false): void {
    if ((this.devopsLoaded && !force) || this.devopsLoading()) return;
    this.devopsLoading.set(true);
    this.dashboardService.getDevOps().subscribe(data => {
      this.devopsLoading.set(false);
      this.devopsLoaded = true;
      this.devops.set(data);
    });
  }

  fillRateBarColor(rate: number | null): string {
    if (rate == null) return 'bg-gray-300';
    if (rate >= 90) return 'bg-red-500';
    if (rate >= 70) return 'bg-amber-400';
    return 'bg-emerald-500';
  }

  formatBytes(bytes: number): string {
    if (!bytes) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let v = bytes, i = 0;
    while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
    return `${v.toFixed(1)} ${units[i]}`;
  }

  private updateWeeklyAddedChart(): void {
    const data = this.cataloging();
    if (!this.weeklyAddedChartRef) return;
    if (!this.weeklyAddedChartInstance) {
      this.weeklyAddedChartInstance = new Chart(this.weeklyAddedChartRef.nativeElement, {
        type: 'bar',
        data: { labels: [], datasets: [{ label: this.translate.instant('DASHBOARD.LEGEND_WEEKLY_ADDED'), data: [], backgroundColor: '#8b5cf6', borderRadius: 4 }] },
        options: {
          maintainAspectRatio: false, responsive: true,
          plugins: { legend: { display: false } },
          scales: { y: { grid: { color: '#f3f4f6' }, border: { dash: [4, 4] }, beginAtZero: true, ticks: { precision: 0 } }, x: { grid: { display: false } } }
        }
      });
    }
    const weeks = data?.weeklyAdded ?? [];
    // Đọc thẳng yyyy-MM-dd từ chuỗi — new Date() đổi theo múi giờ trình duyệt có thể lùi sang ngày Chủ nhật.
    this.weeklyAddedChartInstance.data.labels = weeks.map(w => {
      const [, m, d] = (w.weekStart ?? '').slice(0, 10).split('-').map(Number);
      return `${d}/${m}`;
    });
    this.weeklyAddedChartInstance.data.datasets[0].data = weeks.map(w => w.count);
    this.weeklyAddedChartInstance.update();
  }

  borrowTrendTotal = computed(() =>
    (this.dashboardData()?.borrowTrend?.data ?? []).reduce((a, b) => a + b, 0)
  );

  private borrowTrendChartInstance?: Chart;
  private dailyTrendChartInstance?: Chart;
  private fundBudgetChartInstance?: Chart;

  // We keep a flag to check if view is initialized
  private viewInit = false;

  ngOnInit() {
    this.sub = this.dashboardService.getDashboardData().subscribe(data => {
      this.dashboardData.set(data);
      this.loading.set(false);
      this.updateCharts(data);
    });
    // Cập nhật nhãn legend biểu đồ khi đổi ngôn ngữ
    this.langSub = this.translate.onLangChange.subscribe(() => this.refreshChartLabels());
  }

  ngAfterViewInit() {
    this.viewInit = true;
    this.initCharts();

    // If data loaded before view init (rare but possible), update now
    const data = this.dashboardData();
    if (data) {
      this.updateCharts(data);
    }
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
    this.langSub?.unsubscribe();
    this.borrowTrendChartInstance?.destroy();
    this.dailyTrendChartInstance?.destroy();
    this.fundBudgetChartInstance?.destroy();
    this.circDailyChartInstance?.destroy();
    this.weeklyAddedChartInstance?.destroy();
  }

  // Đồng bộ nhãn legend của biểu đồ theo ngôn ngữ hiện tại
  private refreshChartLabels() {
    if (this.borrowTrendChartInstance) {
      this.borrowTrendChartInstance.data.datasets[0].label = this.translate.instant('DASHBOARD.LEGEND_BORROW_TREND');
      this.borrowTrendChartInstance.update();
    }
    if (this.dailyTrendChartInstance) {
      this.dailyTrendChartInstance.data.datasets[0].label = this.translate.instant('DASHBOARD.LEGEND_DAILY_BORROWED');
      this.dailyTrendChartInstance.data.datasets[1].label = this.translate.instant('DASHBOARD.LEGEND_DAILY_RETURNED');
      this.dailyTrendChartInstance.update();
    }
    if (this.circDailyChartInstance) {
      this.circDailyChartInstance.data.datasets[0].label = this.translate.instant('DASHBOARD.LEGEND_DAILY_BORROWED');
      this.circDailyChartInstance.data.datasets[1].label = this.translate.instant('DASHBOARD.LEGEND_DAILY_RETURNED');
      this.circDailyChartInstance.update();
    }
    if (this.weeklyAddedChartInstance) {
      this.weeklyAddedChartInstance.data.datasets[0].label = this.translate.instant('DASHBOARD.LEGEND_WEEKLY_ADDED');
      this.weeklyAddedChartInstance.update();
    }
    if (this.fundBudgetChartInstance) {
      this.fundBudgetChartInstance.data.datasets[0].label = this.translate.instant('DASHBOARD.LEGEND_FUND_BUDGET');
      this.fundBudgetChartInstance.data.datasets[1].label = this.translate.instant('DASHBOARD.LEGEND_FUND_SPENT');
      this.fundBudgetChartInstance.update();
    }
  }

  // Độ rộng thanh ngang của bucket tuổi nợ, tương đối theo bucket lớn nhất.
  overdueBucketBarWidth(bucket: DashboardOverdueBucket): number {
    const list = this.dashboardData()?.overdueBuckets ?? [];
    const max = Math.max(1, ...list.map(b => b.count));
    return Math.max((bucket.count / max) * 100, bucket.count > 0 ? 2 : 0);
  }

  // Độ rộng thanh progress của mục phân bố loại tài liệu, tương đối theo mục lớn nhất (đầu mảng, đã sort giảm dần).
  typeBarWidth(item: DashboardTypeDistributionItem): number {
    const list = this.dashboardData()?.typeDistribution ?? [];
    const max = list[0]?.count ?? 0;
    return max > 0 ? Math.max((item.count / max) * 100, 2) : 0;
  }

  // Độ rộng thanh progress của mục Top 5 mượn nhiều, tương đối theo hạng 1.
  top5BarWidth(item: DashboardTopBorrowedItem): number {
    const list = this.dashboardData()?.top5Borrowed ?? [];
    const max = list[0]?.count ?? 0;
    return max > 0 ? Math.max((item.count / max) * 100, 2) : 0;
  }

  // Container bọc canvas dùng [hidden] khi không có quỹ nào có dữ liệu (không dùng @if) — [hidden] chỉ
  // ẩn bằng CSS, không gỡ khỏi DOM, nên ViewChild luôn resolve được ngay từ ngAfterViewInit.
  private initFundBudgetChart(): void {
    if (!this.fundBudgetChartRef) return;
    this.fundBudgetChartInstance = new Chart(this.fundBudgetChartRef.nativeElement, {
      type: 'bar',
      data: {
        labels: [],
        datasets: [
          { label: this.translate.instant('DASHBOARD.LEGEND_FUND_BUDGET'), data: [], backgroundColor: '#a78bfa', borderRadius: 4 },
          { label: this.translate.instant('DASHBOARD.LEGEND_FUND_SPENT'),  data: [], backgroundColor: '#f59e0b', borderRadius: 4 }
        ]
      },
      options: {
        maintainAspectRatio: false,
        responsive: true,
        plugins: {
          legend: { position: 'bottom', labels: { usePointStyle: true, padding: 20 } }
        },
        scales: {
          y: { grid: { color: '#f3f4f6' }, border: { dash: [4, 4] }, beginAtZero: true },
          x: { grid: { display: false } }
        }
      }
    });
  }

  private initCharts() {
    this.borrowTrendChartInstance = new Chart(this.borrowTrendChartRef.nativeElement, {
      type: 'line',
      data: {
        labels: [],
        datasets: [
          {
            label: this.translate.instant('DASHBOARD.LEGEND_BORROW_TREND'),
            data: [],
            borderColor: '#3b82f6',
            backgroundColor: 'rgba(59,130,246,0.12)',
            borderWidth: 3,
            tension: 0.4,
            fill: true,
            pointBackgroundColor: '#3b82f6'
          }
        ]
      },
      options: {
        maintainAspectRatio: false,
        responsive: true,
        plugins: {
          legend: { position: 'bottom', labels: { usePointStyle: true, padding: 20 } }
        },
        scales: {
          y: { grid: { color: '#f3f4f6' }, border: { dash: [4, 4] } },
          x: { grid: { display: false } }
        }
      }
    });

    this.dailyTrendChartInstance = new Chart(this.dailyTrendChartRef.nativeElement, {
      type: 'bar',
      data: {
        labels: [],
        datasets: [
          {
            label: this.translate.instant('DASHBOARD.LEGEND_DAILY_BORROWED'),
            data: [],
            backgroundColor: '#3b82f6',
            borderRadius: 4
          },
          {
            label: this.translate.instant('DASHBOARD.LEGEND_DAILY_RETURNED'),
            data: [],
            backgroundColor: '#10b981',
            borderRadius: 4
          }
        ]
      },
      options: {
        maintainAspectRatio: false,
        responsive: true,
        plugins: {
          legend: { position: 'bottom', labels: { usePointStyle: true, padding: 20 } }
        },
        scales: {
          y: { grid: { color: '#f3f4f6' }, border: { dash: [4, 4] }, beginAtZero: true, ticks: { precision: 0 } },
          x: { grid: { display: false } }
        }
      }
    });

    this.initFundBudgetChart();

    // Đợt 21 — biểu đồ mượn/trả theo ngày của tab Lưu thông (cùng dữ liệu Summary, canvas riêng).
    if (this.circDailyChartRef) {
      this.circDailyChartInstance = new Chart(this.circDailyChartRef.nativeElement, {
        type: 'bar',
        data: {
          labels: [],
          datasets: [
            { label: this.translate.instant('DASHBOARD.LEGEND_DAILY_BORROWED'), data: [], backgroundColor: '#3b82f6', borderRadius: 4 },
            { label: this.translate.instant('DASHBOARD.LEGEND_DAILY_RETURNED'), data: [], backgroundColor: '#10b981', borderRadius: 4 }
          ]
        },
        options: {
          maintainAspectRatio: false, responsive: true,
          plugins: { legend: { position: 'bottom', labels: { usePointStyle: true, padding: 20 } } },
          scales: { y: { grid: { color: '#f3f4f6' }, border: { dash: [4, 4] }, beginAtZero: true, ticks: { precision: 0 } }, x: { grid: { display: false } } }
        }
      });
    }
  }

  private updateCharts(data: DashboardData) {
    if (!this.viewInit || !this.borrowTrendChartInstance || !this.dailyTrendChartInstance) return;

    this.borrowTrendChartInstance.data.labels = data.borrowTrend.labels;
    this.borrowTrendChartInstance.data.datasets[0].data = data.borrowTrend.data;
    this.borrowTrendChartInstance.update();

    this.dailyTrendChartInstance.data.labels = data.dailyTrend.map(p => p.label);
    this.dailyTrendChartInstance.data.datasets[0].data = data.dailyTrend.map(p => p.borrowed);
    this.dailyTrendChartInstance.data.datasets[1].data = data.dailyTrend.map(p => p.returned);
    this.dailyTrendChartInstance.update();

    if (this.circDailyChartInstance) {
      this.circDailyChartInstance.data.labels = data.dailyTrend.map(p => p.label);
      this.circDailyChartInstance.data.datasets[0].data = data.dailyTrend.map(p => p.borrowed);
      this.circDailyChartInstance.data.datasets[1].data = data.dailyTrend.map(p => p.returned);
      this.circDailyChartInstance.update();
    }

    if (this.fundBudgetChartInstance) {
      this.fundBudgetChartInstance.data.labels = data.fundBudgetVsSpending.map(f => f.fundName);
      this.fundBudgetChartInstance.data.datasets[0].data = data.fundBudgetVsSpending.map(f => f.budget);
      this.fundBudgetChartInstance.data.datasets[1].data = data.fundBudgetVsSpending.map(f => f.spent);
      this.fundBudgetChartInstance.update();
    }
  }
}
