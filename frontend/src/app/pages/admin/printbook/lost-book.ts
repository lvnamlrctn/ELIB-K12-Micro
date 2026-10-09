import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { LostBookService } from '../../../services/printbook/lost-book.service';
import { StoreService } from '../../../services/printbook/store.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { LostBook, LostBookLookup } from '../../../models/printbook/lost-book';
import { BibType } from '../../../models/cataloging/bib-type';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

type ActionMode = 'lost' | 'restore';

@Component({
  selector: 'app-lost-book',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './lost-book.html'
})
export class LostBookPage implements OnInit, OnDestroy {
  private service   = inject(LostBookService);
  private storeSvc  = inject(StoreService);
  private bibTypeSvc = inject(BibTypeService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'barcode', 'bibTitle', 'store', 'lossDate', 'reason', 'actions'];
  dataSource: LostBook[] = [];
  stores = signal<{ id: number; name: string }[]>([]);
  bibTypes = signal<BibType[]>([]);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  isExporting = signal(false);

  showModal = signal(false);
  isSaving = signal(false);
  actionMode = signal<ActionMode>('lost');
  lookupResult = signal<LostBookLookup | null>(null);
  lookupLoading = signal(false);
  lookupNotFound = signal(false);

  searchForm = new FormGroup({
    storeId:   new FormControl<number | null>(null),
    dateFrom:  new FormControl<string>('', { nonNullable: true }),
    dateTo:    new FormControl<string>('', { nonNullable: true }),
    barcode:   new FormControl<string>('', { nonNullable: true }),
    bibTypeId: new FormControl<number | null>(null),
    mfnFrom:   new FormControl<number | null>(null),
    mfnTo:     new FormControl<number | null>(null),
  });
  dataForm = new FormGroup({
    Barcode:  new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    LossDate: new FormControl<string>(new Date().toISOString().substring(0, 10), { nonNullable: true }),
    Reason:   new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadStores(); this.loadBibTypes(); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadStores(): void { this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} }); }
  loadBibTypes(): void { this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.bibTypes.set(list), error: () => {} }); }
  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }

  private currentFilters() {
    const f = this.searchForm.getRawValue();
    return {
      storeId: f.storeId, dateFrom: f.dateFrom || null, dateTo: f.dateTo || null,
      barcode: f.barcode || null, bibTypeId: f.bibTypeId, mfnFrom: f.mfnFrom, mfnTo: f.mfnTo,
      tenantId: this.tenantId
    };
  }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search({ ...this.currentFilters(), pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a   = document.createElement('a');
    a.href     = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  exportExcel(): void {
    if (this.isExporting()) return;
    this.isExporting.set(true);
    this.service.exportExcel(this.currentFilters()).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => { this.downloadBlob(blob, `sach-mat_${Date.now()}.xlsx`); this.isExporting.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR')); this.isExporting.set(false); }
    });
  }

  openMarkModal(prefillBarcode?: string, mode: ActionMode = 'lost'): void {
    this.dataForm.reset({ Barcode: prefillBarcode || '', LossDate: new Date().toISOString().substring(0, 10), Reason: '' });
    this.actionMode.set(mode);
    this.lookupResult.set(null);
    this.lookupNotFound.set(false);
    this.showModal.set(true);
    if (prefillBarcode) this.onBarcodeBlur();
  }
  closeModal(): void { this.showModal.set(false); }

  // Rời khỏi ô ĐKCB: tự động tra cứu MFN/Kho/Trạng thái/ISBD của tài liệu
  onBarcodeBlur(): void {
    const barcode = this.dataForm.controls.Barcode.value.trim();
    if (!barcode) { this.lookupResult.set(null); this.lookupNotFound.set(false); return; }
    this.lookupLoading.set(true);
    this.service.lookupBarcode(barcode).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.lookupLoading.set(false);
        this.lookupResult.set(res);
        this.lookupNotFound.set(!res);
      },
      error: () => { this.lookupLoading.set(false); this.lookupResult.set(null); this.lookupNotFound.set(true); }
    });
  }

  onSubmit(): void {
    if (this.dataForm.controls.Barcode.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);

    if (this.actionMode() === 'lost') {
      this.service.markLost({ barcode: v.Barcode.trim(), lossDate: v.LossDate, reason: v.Reason }).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.isSaving.set(false); this.closeModal(); this.loadData(); this.toastr.success(this.translate.instant('LOST_BOOK.MARK_OK')); },
        error: (err) => { this.isSaving.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
      });
    } else {
      this.service.restoreLost(v.Barcode.trim()).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.isSaving.set(false); this.closeModal(); this.loadData(); this.toastr.success(this.translate.instant('LOST_BOOK.UNDO_OK')); },
        error: (err) => { this.isSaving.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
      });
    }
  }
}
