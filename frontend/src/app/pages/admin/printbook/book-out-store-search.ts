import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BookOutStoreService } from '../../../services/printbook/book-out-store.service';
import { ExportReasonService } from '../../../services/printbook/export-reason.service';
import { BookOutUnitService } from '../../../services/printbook/book-out-unit.service';
import { ExhibitionLocationService } from '../../../services/printbook/exhibition-location.service';
import { BookOutStoreLine } from '../../../models/printbook/book-out-store';
import { ExportReason } from '../../../models/printbook/export-reason';
import { BookOutUnit } from '../../../models/printbook/book-out-unit';
import { ExhibitionLocation } from '../../../models/printbook/exhibition-location';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-book-out-store-search',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, RouterLink, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './book-out-store-search.html'
})
export class BookOutStoreSearchPage implements OnInit, OnDestroy {
  private service      = inject(BookOutStoreService);
  private reasonSvc    = inject(ExportReasonService);
  private unitSvc      = inject(BookOutUnitService);
  private locationSvc  = inject(ExhibitionLocationService);
  private toastr       = inject(ToastrService);
  private location     = inject(Location);
  public  translate    = inject(TranslateService);
  private auth         = inject(Auth);
  private destroy$     = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  reasons   = signal<ExportReason[]>([]);
  units     = signal<BookOutUnit[]>([]);
  locations = signal<ExhibitionLocation[]>([]);
  isLoading = signal(false);
  isExporting = signal(false);

  displayedColumns = ['stt', 'barcode', 'bibTitle', 'exportDate', 'importDate', 'status', 'reasonName'];
  dataSource: BookOutStoreLine[] = [];
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [10, 25, 50];

  searchForm = new FormGroup({
    typeStatus:      new FormControl<string | null>(null),
    unitId:          new FormControl<number | null>(null),
    exportDateFrom:  new FormControl<string>('', { nonNullable: true }),
    exportDateTo:    new FormControl<string>('', { nonNullable: true }),
    importDateFrom:  new FormControl<string>('', { nonNullable: true }),
    importDateTo:    new FormControl<string>('', { nonNullable: true }),
    reasonId:        new FormControl<number | null>(null),
    exhibitionLocationId: new FormControl<number | null>(null),
    delivererName:   new FormControl<string>('', { nonNullable: true }),
    receiverName:    new FormControl<string>('', { nonNullable: true }),
    barcode:         new FormControl<string>('', { nonNullable: true }),
    returnBarcodeAtLibrary: new FormControl<boolean | null>(null),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.reasonSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.reasons.set(l), error: () => {} });
    this.unitSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.units.set(l), error: () => {} });
    this.locationSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.locations.set(l), error: () => {} });
    this.loadData();
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  loadData(): void {
    this.isLoading.set(true);
    const v = this.searchForm.getRawValue();
    this.service.history({
      typeStatus: v.typeStatus, unitId: v.unitId, reasonId: v.reasonId, exhibitionLocationId: v.exhibitionLocationId,
      delivererName: v.delivererName || null, receiverName: v.receiverName || null, barcode: v.barcode || null,
      returnBarcodeAtLibrary: v.returnBarcodeAtLibrary,
      exportDateFrom: v.exportDateFrom || null, exportDateTo: v.exportDateTo || null,
      importDateFrom: v.importDateFrom || null, importDateTo: v.importDateTo || null,
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  resetForm(): void { this.searchForm.reset(); this.triggerSearch(); }

  exportExcel(): void {
    this.isExporting.set(true);
    const v = this.searchForm.getRawValue();
    this.service.exportExcel({
      typeStatus: v.typeStatus, unitId: v.unitId, reasonId: v.reasonId, exhibitionLocationId: v.exhibitionLocationId,
      delivererName: v.delivererName || null, receiverName: v.receiverName || null, barcode: v.barcode || null,
      returnBarcodeAtLibrary: v.returnBarcodeAtLibrary,
      exportDateFrom: v.exportDateFrom || null, exportDateTo: v.exportDateTo || null,
      importDateFrom: v.importDateFrom || null, importDateTo: v.importDateTo || null,
      pageIndex: 1, pageSize: 99999, tenantId: this.tenantId,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.isExporting.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = 'sach-ra-vao-kho.xlsx'; a.click();
        URL.revokeObjectURL(url);
      },
      error: () => { this.isExporting.set(false); this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR')); }
    });
  }

  goBack(): void { this.location.back(); }
}
