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
import { LiquidateService } from '../../../services/printbook/liquidate.service';
import { StoreService } from '../../../services/printbook/store.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { LiquidateItem } from '../../../models/printbook/liquidate';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-liquidate',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './liquidate.html'
})
export class LiquidatePage implements OnInit, OnDestroy {
  private service    = inject(LiquidateService);
  private storeSvc   = inject(StoreService);
  private bibTypeSvc = inject(BibTypeService);
  private toastr     = inject(ToastrService);
  public  translate  = inject(TranslateService);
  private auth       = inject(Auth);
  private destroy$   = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'barcode', 'bibTitle', 'author', 'store', 'liquidateDate', 'reason', 'actions'];
  dataSource: LiquidateItem[] = [];
  stores   = signal<{ id: number; name: string }[]>([]);
  bibTypes = signal<{ id: number; name: string }[]>([]);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  isExporting = signal(false);

  showModal = signal(false);
  isSaving = signal(false);
  showConfirmUndo = signal(false);
  undoBarcode = signal<string | null>(null);

  searchForm = new FormGroup({
    title:         new FormControl<string>('', { nonNullable: true }),
    author:        new FormControl<string>('', { nonNullable: true }),
    barcode:       new FormControl<string>('', { nonNullable: true }),
    storeId:       new FormControl<number | null>(null),
    bibTypeId:     new FormControl<number | null>(null),
    liquidateFrom: new FormControl<string>('', { nonNullable: true }),
    liquidateTo:   new FormControl<string>('', { nonNullable: true }),
  });
  dataForm = new FormGroup({
    Barcode:       new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    LiquidateDate: new FormControl<string>(new Date().toISOString().substring(0, 10), { nonNullable: true }),
    Reason:        new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadDropdowns(); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadDropdowns(): void {
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} });
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.bibTypes.set((list || []).map(t => ({ id: Number(t.id), name: t.name || '' }))), error: () => {} });
  }
  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }

  private currentFilters() {
    const f = this.searchForm.getRawValue();
    return {
      title: f.title || null, author: f.author || null, barcode: f.barcode || null,
      storeId: f.storeId, bibTypeId: f.bibTypeId, liquidateFrom: f.liquidateFrom || null, liquidateTo: f.liquidateTo || null,
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
      next: blob => { this.downloadBlob(blob, `thanh-ly_${Date.now()}.xlsx`); this.isExporting.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR')); this.isExporting.set(false); }
    });
  }

  openModal(): void { this.dataForm.reset({ Barcode: '', LiquidateDate: new Date().toISOString().substring(0, 10), Reason: '' }); this.showModal.set(true); }
  closeModal(): void { this.showModal.set(false); }
  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    this.service.liquidate({ barcode: v.Barcode.trim(), reason: v.Reason, liquidateDate: v.LiquidateDate }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); this.loadData(); this.toastr.success(this.translate.instant('LIQUIDATE.OK')); },
      error: (err) => { this.isSaving.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
    });
  }

  handleUndo(item: LiquidateItem): void { this.undoBarcode.set(item.barcode); this.showConfirmUndo.set(true); }
  closeConfirm(): void { this.showConfirmUndo.set(false); this.undoBarcode.set(null); }
  confirmUndo(): void {
    const bc = this.undoBarcode(); if (!bc) return;
    this.service.reLiquidate(bc).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('LIQUIDATE.UNDO_OK')); this.closeConfirm(); this.loadData(); },
      error: (err) => { this.closeConfirm(); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
    });
  }
}
