import { Component, computed, inject, OnInit, OnDestroy, AfterViewInit, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { SelectionModel } from '@angular/cdk/collections';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { AbMoveService } from '../../../services/cataloging/move.service';
import { StoreService } from '../../../services/printbook/store.service';
import { AbMove, AbMoveStatus } from '../../../models/cataloging/move';
import { AbMoveDetailLine, MoveDocumentCandidate } from '../../../models/cataloging/move-detail';
import { Store } from '../../../models/printbook/store';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-ab-move',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './ab-move.html'
})
export class AbMovePage implements OnInit, OnDestroy, AfterViewInit {
  private service   = inject(AbMoveService);
  private storeSvc  = inject(StoreService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'code', 'fromStore', 'toStore', 'delivererName', 'delivererDate', 'status', 'actions'];
  dataSource: AbMove[] = [];
  stores = signal<Store[]>([]);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode = signal(false);
  currentId = signal<number | null>(null);
  currentPublicId = signal<string | null>(null);
  currentCode = signal<number | null>(null);
  // Phiếu đã hoàn thành: ĐKCB đã chuyển sang kho nhận, phiếu khoá (không sửa, không thêm/bớt tài liệu)
  currentStatus = signal<number | null>(null);
  isCompleted = computed(() => this.currentStatus() === AbMoveStatus.Completed);
  showConfirmComplete = signal(false);
  isCompleting = signal(false);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeleteId = signal<string | null>(null);

  // Danh sách tài liệu đã gắn vào phiếu (chỉ có ý nghĩa khi phiếu đã lưu, tức currentId() != null)
  lines = signal<AbMoveDetailLine[]>([]);
  isLoadingLines = signal(false);

  // Modal "Chọn tài liệu" (tra cứu theo Kho nguồn của phiếu)
  showPicker = signal(false);
  pickerLoading = signal(false);
  pickerAdding = signal(false);
  pickerDataSource: MoveDocumentCandidate[] = [];
  pickerSelection = new SelectionModel<MoveDocumentCandidate>(true, []);
  pickerTotalRecords = 0; pickerPageSize = 10; pickerPageIndex = 0;
  pickerSearchForm = new FormGroup({
    mfnFrom:     new FormControl<number | null>(null),
    mfnTo:       new FormControl<number | null>(null),
    title:       new FormControl<string>('', { nonNullable: true }),
    author:      new FormControl<string>('', { nonNullable: true }),
    barcodeFrom: new FormControl<string>('', { nonNullable: true }),
    barcodeTo:   new FormControl<string>('', { nonNullable: true }),
    publisher:   new FormControl<string>('', { nonNullable: true }),
    publishYear: new FormControl<string>('', { nonNullable: true }),
  });

  searchForm = new FormGroup({ keyword: new FormControl<string>('', { nonNullable: true }) });
  dataForm = new FormGroup({
    StoreDelivererId: new FormControl<number | null>(null),
    StoreReceiptId:   new FormControl<number | null>(null),
    DelivererName:    new FormControl<string>('', { nonNullable: true }),
    DelivererAddress: new FormControl<string>('', { nonNullable: true }),
    ReceiptName:      new FormControl<string>('', { nonNullable: true }),
    ReceiptAddress:   new FormControl<string>('', { nonNullable: true }),
    DelivererDate:    new FormControl<string>('', { nonNullable: true }),
    Note:             new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} }); this.loadData(); }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  ngAfterViewInit(): void { this.paginator?.page.pipe(takeUntil(this.destroy$)).subscribe(() => { this.pageIndex = this.paginator.pageIndex; this.pageSize = this.paginator.pageSize; this.loadData(); }); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }

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

  openAddModal(): void { this.editMode.set(false); this.currentId.set(null); this.currentPublicId.set(null); this.currentCode.set(null); this.currentStatus.set(null); this.lines.set([]); this.dataForm.reset(); this.dataForm.enable(); this.showModal.set(true); }
  openEditModal(item: AbMove): void {
    this.editMode.set(true); this.currentId.set(item.id); this.currentPublicId.set(item.publicId ?? null);
    this.service.getById(item.id).pipe(takeUntil(this.destroy$)).subscribe(d => {
      if (!d) return;
      this.currentCode.set(d.code ?? null);
      this.currentStatus.set(d.status ?? null);
      this.dataForm.patchValue({
        StoreDelivererId: d.storeDeliver_Id ?? null, StoreReceiptId: d.storeReceipt_Id ?? null,
        DelivererName: d.delivererName ?? '', DelivererAddress: d.delivererAddress ?? '', ReceiptName: d.receiptName ?? '',
        ReceiptAddress: d.receiptAddress ?? '', DelivererDate: (d.delivererDate ?? '').toString().substring(0, 10), Note: d.note ?? '',
      });
      this.showModal.set(true);
      if (this.isCompleted()) this.dataForm.disable(); else this.dataForm.enable();
      this.loadLines();
    });
  }
  isRowCompleted(row: AbMove): boolean { return row.status === AbMoveStatus.Completed; }

  // ── Hoàn thành điều chuyển (port ELIB-LRC 10-04) ─────────────────────────────
  openConfirmComplete(): void { if (this.currentId() != null && this.lines().length > 0) this.showConfirmComplete.set(true); }
  closeConfirmComplete(): void { if (!this.isCompleting()) this.showConfirmComplete.set(false); }
  completeMove(): void {
    const id = this.currentId(); if (id == null || this.isCompleting()) return;
    this.isCompleting.set(true);
    this.service.complete(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.isCompleting.set(false); this.showConfirmComplete.set(false);
        this.currentStatus.set(AbMoveStatus.Completed); this.dataForm.disable();
        this.toastr.success(this.translate.instant('AB_MOVE.COMPLETE_OK', { moved: res?.moved ?? 0 }));
        const skipped = res?.skipped ?? [];
        if (skipped.length) {
          const list = skipped.slice(0, 10).map(x => `${x.barcode ?? '?'} (${x.reason})`).join(', ') + (skipped.length > 10 ? '…' : '');
          this.toastr.warning(this.translate.instant('AB_MOVE.COMPLETE_SKIPPED', { count: skipped.length, list }));
        }
        this.loadData();
      },
      error: err => { this.isCompleting.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }
  closeModal(): void { this.showModal.set(false); this.dataForm.reset(); this.dataForm.enable(); this.lines.set([]); this.currentStatus.set(null); }

  onSubmit(): void {
    if (this.isCompleted()) return;
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<AbMove> = {
      storeDeliver_Id: v.StoreDelivererId ?? undefined, storeReceipt_Id: v.StoreReceiptId ?? undefined,
      delivererName: v.DelivererName || undefined, delivererAddress: v.DelivererAddress || undefined, receiptName: v.ReceiptName || undefined,
      receiptAddress: v.ReceiptAddress || undefined, delivererDate: v.DelivererDate || undefined, note: v.Note || undefined,
    };
    const wasEditMode = this.editMode();
    const publicId = this.currentPublicId();
    const req$ = wasEditMode && publicId ? this.service.update(publicId, payload) : this.service.add(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: (res: AbMove) => {
        this.isSaving.set(false);
        this.toastr.success(this.translate.instant(wasEditMode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
        if (wasEditMode) {
          this.closeModal();
          this.loadData();
        } else {
          // Giữ modal mở, chuyển sang chế độ Sửa tại chỗ để nút "Thêm tài liệu" bật ngay (phiếu đã có Id).
          this.editMode.set(true);
          this.currentId.set(res?.id ?? null);
          this.currentPublicId.set(res?.publicId ?? null);
          this.currentCode.set(res?.code ?? null);
          this.triggerSearch();
        }
      },
      error: err => { this.isSaving.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  // ── Tài liệu điều chuyển ────────────────────────────────────────────────────
  loadLines(): void {
    const id = this.currentId(); if (id == null) return;
    this.isLoadingLines.set(true);
    this.service.getLines(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: l => { this.lines.set(l); this.isLoadingLines.set(false); },
      error: () => { this.isLoadingLines.set(false); }
    });
  }
  removeLine(line: AbMoveDetailLine): void {
    if (!line.publicId) return;
    this.service.deleteLine(line.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.loadLines(); },
      error: () => { }
    });
  }

  // ── Modal chọn tài liệu ──────────────────────────────────────────────────────
  openPicker(): void {
    this.pickerSearchForm.reset();
    this.pickerSelection.clear();
    this.pickerPageIndex = 0;
    this.showPicker.set(true);
    this.pickerSearch();
  }
  closePicker(): void { this.showPicker.set(false); }

  pickerSearch(): void {
    const moveId = this.currentId(); if (moveId == null) return;
    const v = this.pickerSearchForm.getRawValue();
    this.pickerLoading.set(true);
    this.service.searchDocuments({
      moveId, mfnFrom: v.mfnFrom, mfnTo: v.mfnTo, title: v.title || null, author: v.author || null,
      barcodeFrom: v.barcodeFrom || null, barcodeTo: v.barcodeTo || null, publisher: v.publisher || null, publishYear: v.publishYear || null,
      pageIndex: this.pickerPageIndex + 1, pageSize: this.pickerPageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.pickerDataSource = res.data; this.pickerTotalRecords = res.recordsTotal; this.pickerLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.pickerLoading.set(false); }
    });
  }
  pickerTriggerSearch(): void { this.pickerPageIndex = 0; this.pickerSelection.clear(); this.pickerSearch(); }
  pickerResetForm(): void { this.pickerSearchForm.reset(); this.pickerTriggerSearch(); }
  pickerIsAllSelected(): boolean { return this.pickerSelection.selected.length > 0 && this.pickerSelection.selected.length === this.pickerDataSource.length; }
  pickerToggleAll(): void { this.pickerIsAllSelected() ? this.pickerSelection.clear() : this.pickerSelection.select(...this.pickerDataSource); }

  confirmPickerSelection(): void {
    const moveId = this.currentId();
    const barcodeIds = this.pickerSelection.selected.map(x => x.id);
    if (moveId == null || barcodeIds.length === 0) return;
    this.pickerAdding.set(true);
    this.service.addDetails(moveId, barcodeIds).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.pickerAdding.set(false);
        this.closePicker();
        this.loadLines();
        this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
      },
      error: err => { this.pickerAdding.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  handleDelete(item: AbMove): void { this.confirmDeleteId.set(item.publicId ?? null); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeleteId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeleteId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
