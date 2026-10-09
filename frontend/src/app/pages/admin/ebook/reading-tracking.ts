import { Component, inject, OnInit, OnDestroy, AfterViewInit, ViewChild, ElementRef, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import Chart from 'chart.js/auto';
import { ReadingTrackingService, ReadingLog, ReadingStats } from '../../../services/ebook/reading-tracking.service';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

@Component({
  selector: 'app-reading-tracking',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, AppDatePipe, DateInputComponent],
  templateUrl: './reading-tracking.html'
})
export class ReadingTrackingPage implements OnInit, AfterViewInit, OnDestroy {
  private service   = inject(ReadingTrackingService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$  = new Subject<void>();

  // Đơn vị (chỉ super-admin) — dùng chung cho cả 2 tab
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  activeTab = signal<'stats' | 'detail'>('stats');

  // ===== Tab Thống kê =====
  @ViewChild('statsChart') statsChartRef!: ElementRef<HTMLCanvasElement>;
  private chart?: Chart;
  stats = signal<ReadingStats | null>(null);
  statsLoading = signal(false);
  groupBy: 'day' | 'month' = 'month';
  statFrom = '';
  statTo = '';

  // ===== Tab Chi tiết =====
  displayedColumns = ['stt', 'reader', 'doc', 'time', 'duration'];

  private applyTenantColumn(): void {
    if (this.isPrivileged && !this.displayedColumns.includes('tenant')) {
      this.displayedColumns.splice(this.displayedColumns.length - 1, 0, 'tenant');
    }
  }

  private loadTenants(): void {
    if (!this.isPrivileged) return;
    this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
      .pipe(takeUntil(this.destroy$))
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
  }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadStats();
    this.loadDetail();
  }
  dataSource: ReadingLog[] = [];
  detailLoading = signal(false);
  keyword = '';
  detailFrom = '';
  detailTo = '';
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit(): void {
    this.applyTenantColumn();
    this.loadTenants();
    this.loadStats();
    this.loadDetail();
  }

  ngAfterViewInit(): void {
    const s = this.stats();
    if (s) this.renderChart(s);
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadDetail();
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); this.chart?.destroy(); }

  setTab(tab: 'stats' | 'detail'): void { this.activeTab.set(tab); }

  // ===== Thống kê =====
  loadStats(): void {
    this.statsLoading.set(true);
    this.service.getStatistics({ fromDate: this.statFrom || null, toDate: this.statTo || null, groupBy: this.groupBy, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe(s => {
        this.stats.set(s);
        this.statsLoading.set(false);
        this.renderChart(s);
      });
  }

  private renderChart(s: ReadingStats): void {
    if (!this.statsChartRef?.nativeElement) return;
    if (this.chart) {
      this.chart.data.labels = s.labels;
      this.chart.data.datasets[0].data = s.counts;
      this.chart.update();
      return;
    }
    this.chart = new Chart(this.statsChartRef.nativeElement, {
      type: 'line',
      data: {
        labels: s.labels,
        datasets: [{
          label: this.translate.instant('READING_TRACKING.LEGEND_READS'),
          data: s.counts,
          borderColor: '#10b981',
          backgroundColor: 'rgba(16,185,129,0.12)',
          borderWidth: 3, tension: 0.4, fill: true, pointBackgroundColor: '#10b981'
        }]
      },
      options: {
        maintainAspectRatio: false, responsive: true,
        plugins: { legend: { position: 'bottom', labels: { usePointStyle: true, padding: 20 } } },
        scales: { y: { grid: { color: '#f3f4f6' }, border: { dash: [4, 4] } }, x: { grid: { display: false } } }
      }
    });
  }

  // ===== Chi tiết =====
  loadDetail(): void {
    this.detailLoading.set(true);
    this.service.searchDetail({
      keyword: this.keyword || '', fromDate: this.detailFrom || null, toDate: this.detailTo || null,
      tenantId: this.tenantId, pageIndex: this.pageIndex + 1, pageSize: this.pageSize
    }).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.dataSource = res.items;
      this.totalRecords = res.recordsTotal;
      this.detailLoading.set(false);
    });
  }

  triggerSearch(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadDetail();
  }

  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }
}
