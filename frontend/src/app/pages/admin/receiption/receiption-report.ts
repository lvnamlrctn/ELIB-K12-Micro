import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { ReceiptionReportService, ReceiptionReportRow } from '../../../services/receiption/receiption-report.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { CircPlace } from '../../../models/printbook/circ-place';
import { CanDirective } from '../../../directives/can.directive';

@Component({
  selector: 'app-receiption-report',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, DateInputComponent],
  templateUrl: './receiption-report.html'
})
export class ReceiptionReportPage implements OnInit, OnDestroy {
  private service     = inject(ReceiptionReportService);
  private circPlaceSvc = inject(CircPlaceService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private destroy$    = new Subject<void>();

  displayedColumns = ['stt', 'cardNo', 'fullName', 'readerType', 'className', 'count'];
  dataSource: ReceiptionReportRow[] = [];
  circPlaces = signal<CircPlace[]>([]);
  totalRecords = 0; pageSize = 20; pageIndex = 0; pageSizeOptions = [10, 20, 50, 100];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);

  searchForm = new FormGroup({
    circPlaceId: new FormControl<number | null>(null),
    fromDate:    new FormControl<string>('', { nonNullable: true }),
    toDate:      new FormControl<string>('', { nonNullable: true }),
    top:         new FormControl<number>(0, { nonNullable: true }),
  });

  ngOnInit(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.circPlaces.set(l), error: () => {} });
    this.loadData();
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  private buildParams() {
    const f = this.searchForm.getRawValue();
    return { circPlaceId: f.circPlaceId, fromDate: f.fromDate || null, toDate: f.toDate || null, top: f.top || 0 };
  }
  loadData(): void {
    this.isLoading.set(true);
    this.service.report({ ...this.buildParams(), pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  exportExcel(): void {
    this.service.export(this.buildParams() as Record<string, unknown>).pipe(takeUntil(this.destroy$)).subscribe(blob => {
      if (!blob || blob.size === 0) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
      const url = URL.createObjectURL(blob); const a = document.createElement('a');
      a.href = url; a.download = 'receiption-report.xlsx'; a.click(); URL.revokeObjectURL(url);
    });
  }
}
