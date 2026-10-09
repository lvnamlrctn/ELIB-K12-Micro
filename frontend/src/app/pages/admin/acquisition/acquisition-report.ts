import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { AcquisitionReportService, AcquisitionReportType, AcquisitionReportRow } from '../../../services/acquisition/acquisition-report.service';
import { StoreService } from '../../../services/printbook/store.service';
import { SupplierService } from '../../../services/acquisition/supplier.service';
import { Store } from '../../../models/printbook/store';

const COLUMNS_BY_TYPE: Record<AcquisitionReportType, string[]> = {
  ACQUISITION_LIST:   ['stt', 'receiptCode', 'title', 'author', 'amount', 'price'],
  STORE_ALLOCATION:   ['stt', 'storeName', 'title', 'amount'],
  ACCESSION_REGISTER: ['stt', 'barcode', 'title', 'isbn', 'storeName'],
  NEW_BOOK_CATALOG:   ['stt', 'title', 'author', 'isbn', 'publishDate'],
};

@Component({
  selector: 'app-acquisition-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, DateInputComponent, NgSelectModule],
  templateUrl: './acquisition-report.html'
})
export class AcquisitionReportPage implements OnInit, OnDestroy {
  private service    = inject(AcquisitionReportService);
  private storeSvc    = inject(StoreService);
  private supplierSvc = inject(SupplierService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private destroy$    = new Subject<void>();

  reportTypes: { value: AcquisitionReportType; label: string }[] = [
    { value: 'ACQUISITION_LIST',   label: 'ACQ_REPORT.T_ACQUISITION_LIST' },
    { value: 'STORE_ALLOCATION',   label: 'ACQ_REPORT.T_STORE_ALLOCATION' },
    { value: 'ACCESSION_REGISTER', label: 'ACQ_REPORT.T_ACCESSION_REGISTER' },
    { value: 'NEW_BOOK_CATALOG',   label: 'ACQ_REPORT.T_NEW_BOOK_CATALOG' },
  ];
  reportType = signal<AcquisitionReportType>('ACQUISITION_LIST');
  displayedColumns = computed(() => COLUMNS_BY_TYPE[this.reportType()]);

  dataSource: AcquisitionReportRow[] = [];
  stores = signal<Store[]>([]);
  suppliers = signal<{ id: number; name?: string }[]>([]);
  totalRecords = 0; pageSize = 20; pageIndex = 0; pageSizeOptions = [10, 20, 50, 100];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);

  searchForm = new FormGroup({
    fromDate:   new FormControl<string>('', { nonNullable: true }),
    toDate:     new FormControl<string>('', { nonNullable: true }),
    storeId:    new FormControl<number | null>(null),
    supplierId: new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.supplierSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.suppliers.set(r.data), error: () => {} });
    this.loadData();
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  private buildParams() {
    const f = this.searchForm.getRawValue();
    return { fromDate: f.fromDate || null, toDate: f.toDate || null, storeId: f.storeId, supplierId: f.supplierId };
  }

  changeReportType(type: AcquisitionReportType): void { this.reportType.set(type); this.triggerSearch(); }

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
