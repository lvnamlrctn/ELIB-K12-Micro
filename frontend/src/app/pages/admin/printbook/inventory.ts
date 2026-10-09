import { Component, inject, OnInit, OnDestroy, AfterViewInit, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { InventoryService, InventoryImportResult } from '../../../services/printbook/inventory.service';
import { StoreService } from '../../../services/printbook/store.service';
import { Inventory, InventoryBarcode, InventoryFlag, InventorySummary } from '../../../models/printbook/inventory';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { AdminTaskService } from '../../../services/system/admin-task.service';
import { Router } from '@angular/router';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './inventory.html'
})
export class InventoryPage implements OnInit, OnDestroy, AfterViewInit {
  private service   = inject(InventoryService);
  private storeSvc  = inject(StoreService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();
  private adminTaskService = inject(AdminTaskService);
  private router    = inject(Router);
  private auth      = inject(Auth);

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  statusOptions = [ { id: 1, label: 'OPEN' }, { id: 2, label: 'CLOSED' } ];
  // Cờ lưu theo mã ELIB cũ 1 = bình thường · 2 = có vấn đề (trước đây lọc "Không" gửi 0 nên không khớp dòng nào).
  readonly Flag = InventoryFlag;
  filterOptions = [ { id: -1, label: 'ALL' }, { id: InventoryFlag.Yes, label: 'YES' }, { id: InventoryFlag.No, label: 'NO' } ];

  // danh sách phiên
  displayedColumns = ['stt', 'name', 'date', 'status', 'actions'];
  dataSource: Inventory[] = [];
  stores = signal<{ id: number; name: string }[]>([]);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);

  // phiên đang mở (chế độ quét)
  activeInventory = signal<Inventory | null>(null);
  summary = signal<InventorySummary | null>(null);
  barcodes: InventoryBarcode[] = [];
  bcColumns = ['stt', 'barcode', 'bibTitle', 'store', 'flags'];
  bcTotal = 0; bcPageSize = 20; bcPageIndex = 0;
  scanInput = '';
  scanStoreId: number | null = null;
  isScanning = signal(false);
  filterCheckStore = signal<number>(-1);

  showModal = signal(false);
  editMode = signal(false);
  currentPublicId = signal<string | null>(null);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({
    keyword:  new FormControl<string>('', { nonNullable: true }),
    dateFrom: new FormControl<string>('', { nonNullable: true }),
    dateTo:   new FormControl<string>('', { nonNullable: true }),
  });
  dataForm = new FormGroup({
    InventoryName: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    InventoryDate: new FormControl<string>(new Date().toISOString().substring(0, 10), { nonNullable: true }),
    Status:        new FormControl<number>(1, { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadStores(); this.loadData(); }
  ngAfterViewInit(): void { this.paginator?.page.pipe(takeUntil(this.destroy$)).subscribe(() => { this.pageIndex = this.paginator.pageIndex; this.pageSize = this.paginator.pageSize; this.loadData(); }); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadStores(): void { this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} }); }
  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }
  getStatusLabel(id: number | null | undefined): string { const o = this.statusOptions.find(s => s.id === (id ?? 1)); return this.translate.instant('INVENTORY.STATUS_' + (o ? o.label : 'OPEN')); }

  loadData(): void {
    this.isLoading.set(true);
    const f = this.searchForm.getRawValue();
    this.service.search({ keyword: f.keyword || null, dateFrom: f.dateFrom || null, dateTo: f.dateTo || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  // ----- CRUD phiên -----
  openAddModal(): void { this.editMode.set(false); this.currentPublicId.set(null); this.dataForm.reset({ InventoryName: '', InventoryDate: new Date().toISOString().substring(0, 10), Status: 1 }); this.showModal.set(true); }
  openEditModal(item: Inventory): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => { this.dataForm.patchValue({ InventoryName: d.inventoryName ?? '', InventoryDate: (d.inventoryDate || '').substring(0, 10), Status: d.status ?? 1 }); this.showModal.set(true); });
  }
  closeModal(): void { this.showModal.set(false); }
  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<Inventory> = { inventoryName: v.InventoryName, inventoryDate: v.InventoryDate, status: v.Status };
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); (mode ? this.loadData() : this.triggerSearch()); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }
  handleDelete(item: Inventory): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }

  // ----- Chế độ quét -----
  openScan(item: Inventory): void { this.activeInventory.set(item); this.scanStoreId = null; this.filterCheckStore.set(-1); this.bcPageIndex = 0; this.loadBarcodes(); this.loadSummary(); }
  closeScan(): void { this.activeInventory.set(null); this.barcodes = []; this.summary.set(null); }

  loadSummary(): void { const inv = this.activeInventory(); if (!inv) return; this.service.getSummary(inv.id).pipe(takeUntil(this.destroy$)).subscribe({ next: s => this.summary.set(s), error: () => {} }); }
  loadBarcodes(): void {
    const inv = this.activeInventory(); if (!inv) return;
    this.service.searchBarcodes({ inventoryId: inv.id, checkStoreStatus: this.filterCheckStore(), pageIndex: this.bcPageIndex + 1, pageSize: this.bcPageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({ next: res => { this.barcodes = res.data; this.bcTotal = res.recordsTotal; }, error: () => {} });
  }
  setFilterCheckStore(v: number): void { this.filterCheckStore.set(v); this.bcPageIndex = 0; this.loadBarcodes(); }
  onBcPage(e: { pageIndex: number; pageSize: number }): void { this.bcPageIndex = e.pageIndex; this.bcPageSize = e.pageSize; this.loadBarcodes(); }
  getBcRowIndex(i: number): number { return this.bcPageIndex * this.bcPageSize + i + 1; }

  submitScan(): void {
    const inv = this.activeInventory(); const code = (this.scanInput || '').trim();
    if (!inv || !code) return;
    this.isScanning.set(true);
    this.service.scanBarcode({ inventoryId: inv.id, barcode: code, storeId: this.scanStoreId ?? undefined })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (row: InventoryBarcode) => {
          this.isScanning.set(false); this.scanInput = '';
          if (row?.checkRegisteter === InventoryFlag.No) this.toastr.warning(this.translate.instant('INVENTORY.SCAN_NOT_REGISTERED', { code }));
          this.loadBarcodes(); this.loadSummary();
        },
        error: err => { this.isScanning.set(false); this.toastr.error(err?.error?.message || this.translate.instant('INVENTORY.SCAN_ERROR')); }
      });
  }

  exportReport(): void {
    const inv = this.activeInventory(); if (!inv) return;
    this.service.exportReport(inv.id).pipe(takeUntil(this.destroy$)).subscribe(blob => {
      if (!blob || blob.size === 0) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
      const url = URL.createObjectURL(blob); const a = document.createElement('a');
      a.href = url; a.download = `inventory-${inv.id}.xlsx`; a.click(); URL.revokeObjectURL(url);
    });
  }

  // ===== ĐỢT 22.5 — NHẬP EXCEL DANH SÁCH MÃ ĐÃ QUÉT HÀNG LOẠT =====
  showImportModal   = signal(false);
  importFile        = signal<File | null>(null);
  importBackground  = signal(false);
  adminTasksEnabled = signal(false);
  isImporting       = signal(false);
  importResult      = signal<InventoryImportResult | null>(null);

  openImportModal(): void {
    this.importFile.set(null);
    this.importResult.set(null);
    this.importBackground.set(false);
    this.showImportModal.set(true);
    this.adminTaskService.health().pipe(takeUntil(this.destroy$)).subscribe(h => this.adminTasksEnabled.set(h.enabled));
  }

  closeImportModal(): void {
    this.showImportModal.set(false);
    this.importFile.set(null);
    this.importResult.set(null);
    this.importBackground.set(false);
  }

  onImportFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.importFile.set(file);
    input.value = '';
  }

  confirmImport(): void {
    const inv = this.activeInventory();
    const file = this.importFile();
    if (!inv || !file) return;

    if (this.importBackground()) {
      this.isImporting.set(true);
      this.service.importExcelBackground(inv.id, file).pipe(takeUntil(this.destroy$)).subscribe({
        next: task => {
          this.isImporting.set(false);
          if (task) { this.closeImportModal(); this.router.navigate(['/admin/admin-tasks']); }
          else this.importResult.set({ successCount: 0, skippedCount: 0, failedCount: 1, errors: ['Không tạo được tác vụ nền'], message: '' });
        },
        error: () => { this.isImporting.set(false); this.importResult.set({ successCount: 0, skippedCount: 0, failedCount: 1, errors: ['Lỗi kết nối'], message: '' }); }
      });
      return;
    }

    this.isImporting.set(true);
    this.service.importExcel(inv.id, file).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => { this.isImporting.set(false); this.importResult.set(result); this.loadBarcodes(); this.loadSummary(); },
      error: () => { this.isImporting.set(false); this.importResult.set({ successCount: 0, skippedCount: 0, failedCount: 1, errors: ['Lỗi kết nối'], message: '' }); }
    });
  }
}
