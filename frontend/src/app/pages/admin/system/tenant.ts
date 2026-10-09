import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { TenantService } from '../../../services/system/tenant.service';
import { Tenant } from '../../../models/system/tenant';
import { CanDirective } from '../../../directives/can.directive';

@Component({
  selector: 'app-tenant',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule],
  templateUrl: './tenant.html'
})
export class TenantPage implements OnInit, OnDestroy {
  private service   = inject(TenantService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  displayedColumns = ['select', 'stt', 'code', 'name', 'host', 'actions'];
  dataSource: Tenant[] = [];
  selection = new SelectionModel<Tenant>(true, []);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  keyword = '';

  showModal = signal(false);
  editMode  = signal(false);
  currentPublicId = signal<string | null>(null);
  isSaving  = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  dataForm = new FormGroup({
    code:     new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    name:     new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    host:     new FormControl<string>('', { nonNullable: true }),
    logoText: new FormControl<string>('', { nonNullable: true }),
    logoUrl:  new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void { this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search(this.keyword, this.pageIndex + 1, this.pageSize).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.selection.clear(); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  isAllSelected(): boolean { return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length; }
  toggleAllRows(): void { this.isAllSelected() ? this.selection.clear() : this.selection.select(...this.dataSource); }

  openAddModal(): void {
    this.editMode.set(false); this.currentPublicId.set(null);
    this.dataForm.reset({ code: '', name: '', host: '', logoText: '', logoUrl: '' });
    this.showModal.set(true);
  }
  openEditModal(item: Tenant): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      if (!d) return;
      this.dataForm.patchValue({
        code: d.code, name: d.name, host: d.host ?? '', logoText: d.logoText ?? '', logoUrl: d.logoUrl ?? '',
      });
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload = v as Tenant;
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); this.loadData(); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  confirmDelete(item: Tenant): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (!publicId) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
