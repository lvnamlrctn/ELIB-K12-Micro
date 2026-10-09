import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild } from '@angular/core';
import { AdminTablePreferencesComponent, TablePrefColumn, TablePrefsApply, defaultVisibleColumns, sameFilters } from '../../../shared/admin-table-preferences/admin-table-preferences';
import { AdminTableSettings } from '../../../services/system/admin-preference.service';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { Subject, takeUntil, Subscription } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { AbReceiptService } from '../../../services/cataloging/receipt.service';
import { SupplierService } from '../../../services/acquisition/supplier.service';
import { StoreService } from '../../../services/printbook/store.service';
import { UserService } from '../../../services/system/user.service';
import { AbSourceService } from '../../../services/acquisition/ab-source.service';
import { FundService } from '../../../services/acquisition/fund.service';
import { AbReceipt } from '../../../models/cataloging/receipt';
import { Store } from '../../../models/printbook/store';
import { User } from '../../../models/system/user';
import { Fund } from '../../../models/acquisition/fund';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-ab-receipt',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, AdminTablePreferencesComponent, TenantFilterSelectComponent],
  templateUrl: './ab-receipt.html'
})
export class AbReceiptPage implements OnInit, OnDestroy {
  private service     = inject(AbReceiptService);
  private listState = inject(ListPageStateService);
  private supplierSvc = inject(SupplierService);
  private storeSvc    = inject(StoreService);
  private userSvc     = inject(UserService);
  private sourceSvc   = inject(AbSourceService);
  private fundSvc     = inject(FundService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private router      = inject(Router);
  private auth        = inject(Auth);
  private destroy$    = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  // Đợt 21 — cấu hình bảng theo tài khoản. Cột STT/thao tác cố định; khoá cột khớp AdminPreferencePolicy phía server.
  readonly prefColumns: TablePrefColumn[] = [
    { key: 'code',        label: 'AB_RECEIPT.CODE' },
    { key: 'receiptName', label: 'AB_RECEIPT.NAME' },
    { key: 'supplier',    label: 'AB_RECEIPT.SUPPLIER' },
    { key: 'store',       label: 'AB_RECEIPT.RECEIVER_PLACE' },
    { key: 'receiptDate', label: 'AB_RECEIPT.DATE' },
    { key: 'status',      label: 'AB_RECEIPT.STATUS' },
  ];
  displayedColumns = computed(() => {
    const cols = ['stt', ...this.visibleColumns(), 'actions'];
    applyTenantColumn(cols, this.isPrivileged);
    return cols;
  });
  dataSource: AbReceipt[] = [];
  suppliers = signal<{ id: number; name?: string }[]>([]);
  stores = signal<Store[]>([]);
  users = signal<User[]>([]);
  sources = signal<{ id: number; name: string }[]>([]);
  funds = signal<Fund[]>([]);
  filtersCollapsed = signal(false);

  statusOptions = [{ value: 1, label: 'AB_RECEIPT.ORDER_ST_PENDING' }, { value: 2, label: 'AB_RECEIPT.ORDER_ST_DONE' }];
  getStatusLabel(s: number | undefined): string {
    return this.translate.instant(this.statusOptions.find(o => o.value === s)?.label || 'AB_RECEIPT.ORDER_ST_PENDING');
  }
  statusClass(s: number | undefined): string {
    return s === 2 ? 'bg-green-100 text-green-700' : 'bg-amber-100 text-amber-700';
  }

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];

  readonly defaultPageSize = 10;
  private visibleColumns = signal<string[]>(defaultVisibleColumns(this.prefColumns));
  /** true khi vừa khôi phục trang/số dòng từ ListPageStateService — cấu hình tài khoản áp tự động lúc vào trang
   *  không đè lên vị trí đang xem. */
  private restoredListState = false;

  readTableSettings = (): AdminTableSettings => ({
    version: 1, pageSize: this.pageSize, columns: this.visibleColumns(),
    filters: this.searchForm.getRawValue() as AdminTableSettings['filters'],
  });

  applyTableSettings({ settings, automatic }: TablePrefsApply): void {
    this.visibleColumns.set(settings.columns);
    const filtersChanged = !sameFilters(settings.filters, this.searchForm.getRawValue());
    const keepPosition = automatic && this.restoredListState;
    const sizeChanged = !keepPosition && settings.pageSize !== this.pageSize;
    if (!filtersChanged && !sizeChanged) return;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    if (filtersChanged) this.searchForm.patchValue(settings.filters as any);
    if (sizeChanged) this.pageSize = settings.pageSize;
    if (!keepPosition || filtersChanged) this.pageIndex = 0;
    this.loadData();
  }

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({
    keyword:         new FormControl<string>('', { nonNullable: true }),
    codeFrom:        new FormControl<number | null>(null),
    codeTo:          new FormControl<number | null>(null),
    receiptName:     new FormControl<string>('', { nonNullable: true }),
    createdBy:       new FormControl<number | null>(null),
    supplierId:      new FormControl<number | null>(null),
    status:          new FormControl<number | null>(null),
    receiptDateFrom: new FormControl<string>('', { nonNullable: true }),
    receiptDateTo:   new FormControl<string>('', { nonNullable: true }),
    createdDateFrom: new FormControl<string>('', { nonNullable: true }),
    createdDateTo:   new FormControl<string>('', { nonNullable: true }),
    sourceId:        new FormControl<number | null>(null),
    fundId:          new FormControl<number | null>(null),
  });
  dataForm = new FormGroup({
    ReceiptName: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    ReceiptDate: new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    const saved = this.listState.recall('cataloging.ab-receipt'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; this.restoredListState = true; }
    this.supplierSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.suppliers.set(r.data), error: () => {} });
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.userSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.users.set(l), error: () => {} });
    this.sourceSvc.getAll({ draw: 1, start: 0, length: 500, search: { value: '' } }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.sources.set(r.data as unknown as { id: number; name: string }[]), error: () => {} });
    this.fundSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.funds.set(r.data), error: () => {} });
    this.loadData();
  }
  toggleFilters(): void { this.filtersCollapsed.update(v => !v); }
  resetForm(): void { this.searchForm.reset(); this.triggerSearch(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getSupplierName(id: number | null | undefined): string { if (!id) return '—'; return this.suppliers().find(s => s.id === id)?.name || '—'; }
  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }

  /** Đợt 21 — huỷ lần tải trước: cấu hình bảng có thể kích hoạt lần tải thứ 2 ngay sau lần tải mặc định, phản hồi
   *  cũ về sau sẽ ghi đè kết quả đã lọc. */
  private loadSub?: Subscription;

  loadData(): void {
    this.loadSub?.unsubscribe();
    this.listState.remember('cataloging.ab-receipt', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    const v = this.searchForm.getRawValue();
    this.loadSub = this.service.search({
      keyword:         v.keyword         || null,
      codeFrom:        v.codeFrom        ?? null,
      codeTo:          v.codeTo          ?? null,
      receiptName:     v.receiptName     || null,
      createdBy:       v.createdBy       ?? null,
      supplierId:      v.supplierId      ?? null,
      status:          v.status          ?? null,
      receiptDateFrom: v.receiptDateFrom || null,
      receiptDateTo:   v.receiptDateTo   || null,
      createdDateFrom: v.createdDateFrom || null,
      createdDateTo:   v.createdDateTo   || null,
      sourceId:        v.sourceId        ?? null,
      fundId:          v.fundId          ?? null,
      tenantId:        this.tenantId,
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void { this.dataForm.reset(); this.showModal.set(true); }
  closeModal(): void { this.showModal.set(false); this.dataForm.reset(); }

  /** Tạo tối thiểu (Tên đơn + Ngày nhận) rồi điều hướng sang trang chi tiết để điền đầy đủ — cùng mô hình đã dùng cho Worksheet. */
  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<AbReceipt> = { receipt_Name: v.ReceiptName, receipt_Date: v.ReceiptDate || undefined };
    this.service.create(payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.isSaving.set(false); this.closeModal();
        const publicId = res?.publicId;
        if (publicId) this.router.navigate(['/admin/ab-receipts', publicId]);
        else this.triggerSearch();
      },
      error: () => { this.isSaving.set(false); }
    });
  }

  openDetail(item: AbReceipt): void {
    if (!item.publicId) return;
    this.router.navigate(['/admin/ab-receipts', item.publicId]);
  }

  handleDelete(item: AbReceipt): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (!publicId) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
