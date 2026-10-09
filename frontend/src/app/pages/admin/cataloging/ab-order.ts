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
import { AbOrderService } from '../../../services/cataloging/order.service';
import { SupplierService } from '../../../services/acquisition/supplier.service';
import { FundService } from '../../../services/acquisition/fund.service';
import { AbOrder } from '../../../models/cataloging/order';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-ab-order',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './ab-order.html'
})
export class AbOrderPage implements OnInit, OnDestroy {
  private service     = inject(AbOrderService);
  private listState = inject(ListPageStateService);
  private supplierSvc = inject(SupplierService);
  private fundSvc     = inject(FundService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private router      = inject(Router);
  private auth        = inject(Auth);
  private destroy$    = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  statusOptions = [
    { value: 0, label: 'AB_ORDER.ST_DRAFT' },
    { value: 1, label: 'AB_ORDER.ST_ORDERED' },
    { value: 2, label: 'AB_ORDER.ST_RECEIVED' },
  ];

  displayedColumns = ['stt', 'code', 'orderName', 'supplier', 'orderDate', 'status', 'actions'];
  dataSource: AbOrder[] = [];
  suppliers = signal<{ id: number; name?: string }[]>([]);
  funds = signal<{ id: number; name?: string }[]>([]);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode = signal(false);
  currentId = signal<number | null>(null);
  currentPublicId = signal<string | null>(null);
  currentCode = signal<number | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeleteId = signal<string | null>(null);

  searchForm = new FormGroup({ orderName: new FormControl<string>('', { nonNullable: true }) });
  dataForm = new FormGroup({
    OrderName:  new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    OrderDate:  new FormControl<string>('', { nonNullable: true }),
    DueDate:    new FormControl<string>('', { nonNullable: true }),
    SupplierId: new FormControl<number | null>(null),
    FundId:     new FormControl<number | null>(null),
    Status:     new FormControl<number>(0, { nonNullable: true }),
    Note:       new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    const saved = this.listState.recall('cataloging.ab-order'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.supplierSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.suppliers.set(r.data), error: () => {} });
    this.fundSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.funds.set(r.data), error: () => {} });
    this.loadData();
  }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getSupplierName(id: number | null | undefined): string { if (!id) return '—'; return this.suppliers().find(s => s.id === id)?.name || '—'; }
  getStatusLabel(s: number | undefined): string { return this.translate.instant(this.statusOptions.find(o => o.value === (s ?? 0))?.label || 'AB_ORDER.ST_DRAFT'); }

  loadData(): void {
    this.listState.remember('cataloging.ab-order', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    this.service.search({ orderName: this.searchForm.getRawValue().orderName || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void { this.editMode.set(false); this.currentId.set(null); this.currentPublicId.set(null); this.currentCode.set(null); this.dataForm.reset({ Status: 0 }); this.showModal.set(true); }
  openEditModal(item: AbOrder): void {
    this.editMode.set(true); this.currentId.set(item.id); this.currentPublicId.set(item.publicId ?? null);
    this.service.getById(item.id).pipe(takeUntil(this.destroy$)).subscribe(d => {
      if (!d) return;
      this.currentCode.set(d.code ?? null);
      this.dataForm.patchValue({
        OrderName: d.order_Name ?? '', OrderDate: (d.date_Order ?? '').toString().substring(0, 10),
        DueDate: (d.duedate ?? '').toString().substring(0, 10), SupplierId: d.supplier_Id ?? null, FundId: d.fundId ?? null,
        Status: d.status ?? 0, Note: d.note ?? '',
      });
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); this.dataForm.reset({ Status: 0 }); }

  /** Sửa thì lưu tại chỗ; thêm mới thì tạo xong điều hướng sang trang chi tiết để thêm tài liệu — cùng mô hình đã dùng cho Đơn nhận. */
  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<AbOrder> = {
      order_Name: v.OrderName, date_Order: v.OrderDate || undefined,
      duedate: v.DueDate || undefined, supplier_Id: v.SupplierId ?? undefined, fundId: v.FundId ?? undefined, status: v.Status, note: v.Note || undefined,
    };
    const wasEditMode = this.editMode();
    const publicId = this.currentPublicId();
    const req$ = wasEditMode && publicId ? this.service.update(publicId, payload) : this.service.add(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.isSaving.set(false); this.closeModal();
        this.toastr.success(this.translate.instant(wasEditMode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
        if (wasEditMode) { this.loadData(); return; }
        const newPublicId = res?.publicId;
        if (newPublicId) this.router.navigate(['/admin/ab-orders', newPublicId]);
        else this.triggerSearch();
      },
      error: () => { this.isSaving.set(false); }
    });
  }

  handleDelete(item: AbOrder): void { this.confirmDeleteId.set(item.publicId ?? null); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeleteId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeleteId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }

  openDetail(item: AbOrder): void {
    if (!item.publicId) return;
    this.router.navigate(['/admin/ab-orders', item.publicId]);
  }
}
