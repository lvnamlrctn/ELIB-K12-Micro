import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CircPolicyService } from '../../../services/circulation/circ-policy.service';
import { ReaderTypeService } from '../../../services/circulation/reader-type.service';
import { CircPolicy } from '../../../models/circulation/circ-policy';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-circ-policy',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './circ-policy.html'
})
export class CircPolicyPage implements OnInit, OnDestroy {
  private service     = inject(CircPolicyService);
  private readerTypeSvc = inject(ReaderTypeService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private auth        = inject(Auth);
  private destroy$    = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'readerType', 'maxItems', 'loanDays', 'maxRenew', 'renewDays', 'finePerDay', 'actions'];
  dataSource: CircPolicy[] = [];
  readerTypes = signal<{ id: number; name: string }[]>([]);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode = signal(false);
  currentPublicId = signal<string | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({ keyword: new FormControl<string>('', { nonNullable: true }) });
  dataForm = new FormGroup({
    ReaderTypeId: new FormControl<number | null>(null),
    MaxItems:     new FormControl<number>(5, { nonNullable: true }),
    LoanDays:     new FormControl<number>(14, { nonNullable: true }),
    MaxRenew:     new FormControl<number>(1, { nonNullable: true }),
    RenewDays:    new FormControl<number>(7, { nonNullable: true }),
    FinePerDay:   new FormControl<number>(0, { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadReaderTypes(); this.loadData(); }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadReaderTypes(): void {
    this.readerTypeSvc.getAll({ draw: 0, start: 0, length: 1000, search: { value: '' } }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.readerTypes.set((res.data || []).map(d => ({ id: Number(d.id), name: d.name }))), error: () => {}
    });
  }
  getReaderTypeName(id: number | null | undefined): string { if (!id) return '—'; return this.readerTypes().find(t => t.id === id)?.name || '—'; }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search({ keyword: this.searchForm.getRawValue().keyword || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void { this.editMode.set(false); this.currentPublicId.set(null); this.dataForm.reset({ MaxItems: 5, LoanDays: 14, MaxRenew: 1, RenewDays: 7, FinePerDay: 0 }); this.showModal.set(true); }
  openEditModal(item: CircPolicy): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.dataForm.patchValue({ ReaderTypeId: d.readerType ?? null, MaxItems: d.numberOfBook ?? 0, LoanDays: d.numberOfDate ?? 0, MaxRenew: d.numberOfRenew ?? 0, RenewDays: d.numberOfRenewDays ?? 0, FinePerDay: d.finePerDay ?? 0 });
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<CircPolicy> = { readerType: v.ReaderTypeId ?? undefined, numberOfBook: v.MaxItems, numberOfDate: v.LoanDays, numberOfRenew: v.MaxRenew, numberOfRenewDays: v.RenewDays, finePerDay: v.FinePerDay };
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); (mode ? this.loadData() : this.triggerSearch()); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  handleDelete(item: CircPolicy): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
