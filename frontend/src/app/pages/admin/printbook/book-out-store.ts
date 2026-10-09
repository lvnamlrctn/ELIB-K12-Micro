import { Component, inject, OnInit, OnDestroy, signal, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
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
  selector: 'app-book-out-store',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, RouterLink, AppDatePipe, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './book-out-store.html'
})
export class BookOutStorePage implements OnInit, OnDestroy {
  private service      = inject(BookOutStoreService);
  private reasonSvc    = inject(ExportReasonService);
  private unitSvc      = inject(BookOutUnitService);
  private locationSvc  = inject(ExhibitionLocationService);
  private toastr       = inject(ToastrService);
  public  translate    = inject(TranslateService);
  private auth         = inject(Auth);
  private destroy$     = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  @ViewChild('barcodeInput') barcodeInput!: ElementRef<HTMLInputElement>;
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  mode = signal<'out' | 'in'>('out');
  isScanning = signal(false);
  isLoading = signal(false);

  reasons   = signal<ExportReason[]>([]);
  units     = signal<BookOutUnit[]>([]);
  locations = signal<ExhibitionLocation[]>([]);

  displayedColumns = ['stt', 'barcode', 'bibTitle', 'exportDate', 'importDate', 'status', 'reasonName'];
  dataSource: BookOutStoreLine[] = [];
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [10, 25, 50];

  sessionForm = new FormGroup({
    returnAtLibrary: new FormControl<boolean>(false, { nonNullable: true }),
    delivererName:   new FormControl<string>('', { nonNullable: true }),
    receiverName:    new FormControl<string>('', { nonNullable: true }),
    reasonId:        new FormControl<number | null>(null),
    unitId:          new FormControl<number | null>(null),
    exhibitionLocationId: new FormControl<number | null>(null),
    barcode:         new FormControl<string>('', { nonNullable: true }),
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

  setMode(m: 'out' | 'in'): void { this.mode.set(m); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  loadData(): void {
    this.isLoading.set(true);
    this.service.history({ pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }

  scan(): void {
    const barcode = this.sessionForm.value.barcode?.trim();
    if (!barcode) return;
    this.isScanning.set(true);

    const v = this.sessionForm.getRawValue();
    const req$ = this.mode() === 'out'
      ? this.service.scanOut({
          barcode, delivererName: v.delivererName || null, receiverName: v.receiverName || null,
          reasonId: v.reasonId, unitId: v.unitId, exhibitionLocationId: v.exhibitionLocationId,
          returnBarcodeAtLibrary: v.returnAtLibrary,
        })
      : this.service.scanIn(barcode);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isScanning.set(false);
        this.sessionForm.patchValue({ barcode: '' });
        this.toastr.success(this.translate.instant(this.mode() === 'out' ? 'BOOK_OUT_STORE.SCAN_OUT_SUCCESS' : 'BOOK_OUT_STORE.SCAN_IN_SUCCESS'));
        this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0;
        this.loadData();
        setTimeout(() => this.barcodeInput?.nativeElement?.focus());
      },
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      error: (err: any) => {
        this.isScanning.set(false);
        this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR'));
        setTimeout(() => this.barcodeInput?.nativeElement?.focus());
      }
    });
  }
}
