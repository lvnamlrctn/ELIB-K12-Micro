import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { SerialReportService, SerialReportType, SerialReportRow } from '../../../services/serial/serial-report.service';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

const COLUMNS_BY_TYPE: Record<SerialReportType, string[]> = {
  RECEIVED_SUMMARY:   ['stt', 'subscriptionTitle', 'issn', 'receivedIssues', 'quantity'],
  RECEIVED_DETAIL:    ['stt', 'subscriptionTitle', 'serialSeq', 'publishedDate', 'status'],
  MISSING_CLAIM_LIST: ['stt', 'subscriptionTitle', 'serialSeq', 'plannedDate', 'status', 'claimCount'],
};

@Component({
  selector: 'app-serial-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent],
  templateUrl: './serial-report.html'
})
export class SerialReportPage implements OnInit, OnDestroy {
  private service    = inject(SerialReportService);
  private toastr     = inject(ToastrService);
  public  translate  = inject(TranslateService);
  private destroy$   = new Subject<void>();

  reportTypes: { value: SerialReportType; label: string }[] = [
    { value: 'RECEIVED_SUMMARY',   label: 'SERIAL_REPORT.T_RECEIVED_SUMMARY' },
    { value: 'RECEIVED_DETAIL',    label: 'SERIAL_REPORT.T_RECEIVED_DETAIL' },
    { value: 'MISSING_CLAIM_LIST', label: 'SERIAL_REPORT.T_MISSING_CLAIM_LIST' },
  ];
  reportType = signal<SerialReportType>('RECEIVED_SUMMARY');
  displayedColumns = computed(() => COLUMNS_BY_TYPE[this.reportType()]);

  dataSource: SerialReportRow[] = [];
  totalRecords = 0; pageSize = 20; pageIndex = 0; pageSizeOptions = [10, 20, 50, 100];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);

  searchForm = new FormGroup({
    subscriptionCode: new FormControl<string>('', { nonNullable: true }),
    dateFrom:         new FormControl<string>('', { nonNullable: true }),
    dateTo:           new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void { this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  private buildParams() {
    const f = this.searchForm.getRawValue();
    return { subscriptionCode: f.subscriptionCode || null, dateFrom: f.dateFrom || null, dateTo: f.dateTo || null };
  }

  changeReportType(type: SerialReportType): void { this.reportType.set(type); this.triggerSearch(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.report(this.reportType(), { ...this.buildParams(), pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  exportExcel(): void {
    this.service.export(this.reportType(), this.buildParams()).pipe(takeUntil(this.destroy$)).subscribe(blob => {
      if (!blob || blob.size === 0) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
      const url = URL.createObjectURL(blob); const a = document.createElement('a');
      a.href = url; a.download = `${this.reportType().toLowerCase()}.xlsx`; a.click(); URL.revokeObjectURL(url);
    });
  }
}
