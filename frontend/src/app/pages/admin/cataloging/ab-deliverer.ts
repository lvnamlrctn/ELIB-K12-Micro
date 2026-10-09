import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { DelivererService } from '../../../services/cataloging/deliverer.service';
import { DelivererStatusService } from '../../../services/cataloging/deliverer-status.service';
import { StoreService } from '../../../services/printbook/store.service';
import { UserService } from '../../../services/system/user.service';
import { Deliverer } from '../../../models/cataloging/deliverer';
import { DelivererStatus } from '../../../models/cataloging/deliverer-status';
import { Store } from '../../../models/printbook/store';
import { User } from '../../../models/system/user';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-ab-deliverer',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './ab-deliverer.html'
})
export class AbDelivererPage implements OnInit, OnDestroy {
  private service    = inject(DelivererService);
  private listState = inject(ListPageStateService);
  private statusSvc  = inject(DelivererStatusService);
  private storeSvc   = inject(StoreService);
  private userSvc    = inject(UserService);
  private toastr     = inject(ToastrService);
  public  translate  = inject(TranslateService);
  private router     = inject(Router);
  private auth       = inject(Auth);
  private destroy$   = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'code', 'delivererDate', 'delivererName', 'receiptName', 'store', 'status', 'actions'];
  dataSource: Deliverer[] = [];
  stores = signal<Store[]>([]);
  statuses = signal<DelivererStatus[]>([]);
  users = signal<User[]>([]);
  filtersCollapsed = signal(false);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({
    keyword:           new FormControl<string>('', { nonNullable: true }),
    codeFrom:          new FormControl<number | null>(null),
    codeTo:            new FormControl<number | null>(null),
    delivererName:     new FormControl<string>('', { nonNullable: true }),
    receiptName:       new FormControl<string>('', { nonNullable: true }),
    status:            new FormControl<number | null>(null),
    createdBy:         new FormControl<number | null>(null),
    delivererDateFrom: new FormControl<string>('', { nonNullable: true }),
    delivererDateTo:   new FormControl<string>('', { nonNullable: true }),
  });
  dataForm = new FormGroup({
    DelivererDate: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    const saved = this.listState.recall('cataloging.ab-deliverer'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.statusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.statuses.set(l), error: () => {} });
    this.userSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.users.set(l), error: () => {} });
    this.loadData();
  }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  toggleFilters(): void { this.filtersCollapsed.update(v => !v); }
  resetForm(): void { this.searchForm.reset(); this.triggerSearch(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }
  getStatusName(id: number | null | undefined): string { if (id == null) return '—'; return this.statuses().find(s => s.id === id)?.name || '—'; }

  loadData(): void {
    this.listState.remember('cataloging.ab-deliverer', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    const v = this.searchForm.getRawValue();
    this.service.search({
      keyword:           v.keyword           || null,
      codeFrom:          v.codeFrom          ?? null,
      codeTo:            v.codeTo            ?? null,
      delivererName:     v.delivererName     || null,
      receiptName:       v.receiptName       || null,
      status:            v.status            ?? null,
      createdBy:         v.createdBy         ?? null,
      delivererDateFrom: v.delivererDateFrom || null,
      delivererDateTo:   v.delivererDateTo   || null,
      tenantId: this.tenantId,
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void { this.dataForm.reset({ DelivererDate: new Date().toISOString().substring(0, 10) }); this.showModal.set(true); }
  closeModal(): void { this.showModal.set(false); this.dataForm.reset(); }

  /** Tạo tối thiểu (Ngày giao) rồi điều hướng sang trang chi tiết để điền đầy đủ — cùng mô hình đã dùng cho Phiếu nhập. */
  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<Deliverer> = { delivererDate: v.DelivererDate || undefined };
    this.service.create(payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.isSaving.set(false); this.closeModal();
        const publicId = res?.publicId;
        if (publicId) this.router.navigate(['/admin/ab-deliverers', publicId]);
        else this.triggerSearch();
      },
      error: () => { this.isSaving.set(false); }
    });
  }

  openDetail(item: Deliverer): void {
    if (!item.publicId) return;
    this.router.navigate(['/admin/ab-deliverers', item.publicId]);
  }

  handleDelete(item: Deliverer): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (!publicId) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
