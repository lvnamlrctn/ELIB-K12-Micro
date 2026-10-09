import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, takeUntil, Subscription } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BibService } from '../../../services/cataloging/bib.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { PrintQueueService } from '../../../services/cataloging/print-queue.service';
import { EbookCollectionService } from '../../../services/ebook/collection.service';
import { StoreService } from '../../../services/printbook/store.service';
import { BarcodeStatusService } from '../../../services/printbook/barcode-status.service';
import { HistoryBorrowService } from '../../../services/circulation/history-borrow.service';
import { Bib, MarcField } from '../../../models/cataloging/bib';
import { BibType } from '../../../models/cataloging/bib-type';
import { Store } from '../../../models/printbook/store';
import { RegisteredBarcode } from '../../../models/cataloging/receipt';
import { Borrow } from '../../../models/circulation/borrow';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { AdminTablePreferencesComponent, TablePrefColumn, TablePrefsApply, defaultVisibleColumns, sameFilters } from '../../../shared/admin-table-preferences/admin-table-preferences';
import { AdminTableSettings } from '../../../services/system/admin-preference.service';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-bib-list',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, AdminTablePreferencesComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './bib-list.html'
})
export class BibListPage implements OnInit, OnDestroy {
  private service       = inject(BibService);
  private listState = inject(ListPageStateService);
  private bibTypeSvc    = inject(BibTypeService);
  private collectionSvc = inject(EbookCollectionService);
  private storeSvc      = inject(StoreService);
  private barcodeStatusSvc = inject(BarcodeStatusService);
  private historySvc    = inject(HistoryBorrowService);
  private router     = inject(Router);
  private toastr     = inject(ToastrService);
  public  translate  = inject(TranslateService);
  public  printQueue = inject(PrintQueueService);
  private auth       = inject(Auth);
  private destroy$   = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  // Đợt 21 — cấu hình bảng theo tài khoản (thay app-column-toggle nhớ theo trình duyệt). Cột chọn/STT/thao tác cố
  // định; khoá cột khớp AdminPreferencePolicy phía server.
  readonly prefColumns: TablePrefColumn[] = [
    { key: 'mfn',         label: 'MFN' },
    { key: 'title',       label: 'BIB.COL_TITLE' },
    { key: 'author',      label: 'BIB.COL_AUTHOR' },
    { key: 'publisher',   label: 'BIB.COL_PUBLISHER' },
    { key: 'publishDate', label: 'BIB.COL_YEAR' },
    { key: 'status',      label: 'BIB.COL_STATUS',      defaultVisible: false },
    { key: 'hasReceipt',  label: 'BIB.COL_HAS_RECEIPT', defaultVisible: false },
    { key: 'hasOrder',    label: 'BIB.COL_HAS_ORDER',   defaultVisible: false },
    { key: 'bibType',     label: 'BIB.COL_TYPE',        defaultVisible: false },
  ];
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

  displayedColumns = computed(() => {
    const cols = ['select', 'stt', ...this.visibleColumns(), 'actions'];
    applyTenantColumn(cols, this.isPrivileged);
    return cols;
  });
  dataSource: Bib[] = [];
  bibTypes = signal<BibType[]>([]);
  selection = new SelectionModel<Bib>(true, []);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  isLoading = signal(false);
  isRebuildingIndex = signal(false);
  isRebuildingUnifiedIndex = signal(false);
  isRebuildingFromScratch  = signal(false);
  showConfirmRebuildFromScratch = signal(false);
  rebuildConfirmWord = signal('');
  showConfirmDelete = signal(false);
  confirmDeleteMfn = signal<number | null>(null);

  collections = signal<{ id: number; label: string }[]>([]);
  showMoveCollection     = signal(false);
  moveTargetCollectionId = signal<number | null>(null);
  isMoving               = signal(false);

  stores          = signal<Store[]>([]);
  barcodeStatuses = signal<{ id: string; label: string }[]>([]);
  showViewInfo    = signal(false);
  viewInfoLoading = signal(false);
  viewInfoBib     = signal<Bib | null>(null);
  viewInfoItems   = signal<RegisteredBarcode[]>([]);
  viewInfoHistory = signal<Borrow[]>([]);
  viewInfoTab     = signal<'info' | 'items' | 'history'>('info');

  searchForm = new FormGroup({
    keyword:   new FormControl<string>('', { nonNullable: true }),
    title:     new FormControl<string>('', { nonNullable: true }),
    author:    new FormControl<string>('', { nonNullable: true }),
    publisher: new FormControl<string>('', { nonNullable: true }),
    mfnFrom:   new FormControl<number | null>(null),
    mfnTo:     new FormControl<number | null>(null),
    bibTypeId: new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    const saved = this.listState.recall('cataloging.bib-list'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; this.restoredListState = true; }
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.bibTypes.set(l), error: () => {} });
    this.collectionSvc.getTree().pipe(takeUntil(this.destroy$)).subscribe({ next: tree => this.collections.set(this.collectionSvc.flattenForSelect(tree)), error: () => {} });
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.barcodeStatusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const arr = list as any[];
        this.barcodeStatuses.set(arr.map(x => ({ id: String(x.id ?? ''), label: x.commentStatus ?? x.name ?? String(x.id ?? '') })));
      },
      error: () => {}
    });
    this.loadData();
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  /** Đợt 21 — huỷ lần tải trước: cấu hình bảng có thể kích hoạt lần tải thứ 2 ngay sau lần tải mặc định, phản hồi
   *  cũ về sau sẽ ghi đè kết quả đã lọc. */
  private loadSub?: Subscription;

  loadData(): void {
    this.loadSub?.unsubscribe();
    this.listState.remember('cataloging.bib-list', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.loadSub = this.service.search({ ...s, tenantId: this.tenantId, pageIndex: this.pageIndex + 1, pageSize: this.pageSize }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.selection.clear(); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  resetForm(): void { this.searchForm.reset(); this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  addNew(): void { this.router.navigate(['/admin/catalog-bibs/new']); }
  edit(row: Bib): void { if (row.mfn != null) this.router.navigate(['/admin/catalog-bibs/edit', row.mfn]); }

  viewInfo(row: Bib): void {
    this.showViewInfo.set(true);
    this.viewInfoTab.set('info');
    this.viewInfoLoading.set(true);
    this.viewInfoBib.set(null);
    this.viewInfoItems.set([]);
    this.viewInfoHistory.set([]);

    if (row.mfn != null) {
      this.service.getByMfn(row.mfn).pipe(takeUntil(this.destroy$)).subscribe({
        next: b => this.viewInfoBib.set(b), error: () => {}
      });
    }
    this.service.getItems(row.bibId).pipe(takeUntil(this.destroy$)).subscribe({
      next: items => this.viewInfoItems.set(items), error: () => {}
    });
    this.historySvc.search({ bibId: row.bibId, pageSize: 50 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.viewInfoHistory.set(res.data); this.viewInfoLoading.set(false); },
      error: () => this.viewInfoLoading.set(false)
    });
  }

  closeViewInfo(): void {
    this.showViewInfo.set(false);
    this.viewInfoBib.set(null);
    this.viewInfoItems.set([]);
    this.viewInfoHistory.set([]);
  }

  getSub(fields: MarcField[] | undefined, tag: string, code: string): string {
    return fields?.find(f => f.tag === tag)?.subFields?.find(s => s.code === code)?.value || '—';
  }

  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }
  getBibTypeName(id: number | null | undefined): string { if (!id) return '—'; return this.bibTypes().find(t => t.id === id)?.name || '—'; }
  getStatusLabel(code: string | null | undefined): string { if (!code) return '—'; return this.barcodeStatuses().find(s => s.id === code)?.label || code; }

  loanStatusClass(status: number | undefined): string {
    switch (status) {
      case 3:  return 'bg-red-50 text-red-700';
      case 2:  return 'bg-gray-100 text-gray-600';
      default: return 'bg-green-50 text-green-700';
    }
  }

  handleDelete(row: Bib): void { if (row.mfn == null) return; this.confirmDeleteMfn.set(row.mfn); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeleteMfn.set(null); }
  confirmActionExecute(): void {
    const mfn = this.confirmDeleteMfn(); if (mfn == null) return;
    this.service.deleteByMfn(mfn).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }

  isAllSelected(): boolean { return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length; }
  toggleAllRows(): void { this.isAllSelected() ? this.selection.clear() : this.selection.select(...this.dataSource); }

  addSelectedToPrintQueue(): void {
    if (this.selection.selected.length === 0) return;
    this.printQueue.add(this.selection.selected);
    this.selection.clear();
    this.toastr.success(this.translate.instant('BIB.ADDED_TO_PRINT_QUEUE'));
  }
  goToPrintQueue(): void { this.router.navigate(['/admin/print-card']); }

  openMoveCollection(): void {
    if (!this.selection.selected.length) return;
    this.moveTargetCollectionId.set(null);
    this.showMoveCollection.set(true);
  }

  closeMoveCollection(): void {
    this.showMoveCollection.set(false);
    this.moveTargetCollectionId.set(null);
  }

  confirmMoveCollection(): void {
    const cid = this.moveTargetCollectionId();
    if (!cid || !this.selection.selected.length) return;
    this.isMoving.set(true);
    const bibIds = this.selection.selected.map(b => b.bibId);
    this.service.bulkMoveCollection(bibIds, cid).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isMoving.set(false);
        this.closeMoveCollection();
        this.selection.clear();
        this.loadData();
        this.toastr.success(this.translate.instant('EBOOK.MOVE_SUCCESS'));
      },
      error: (err: any) => {
        this.isMoving.set(false);
        this.toastr.error(err?.error?.message || this.translate.instant('EBOOK.MOVE_ERROR'));
      }
    });
  }

  rebuildIndex(): void {
    this.isRebuildingIndex.set(true);
    this.service.rebuildIndex().pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isRebuildingIndex.set(false); this.toastr.success(this.translate.instant('BIB.REBUILD_INDEX_SUCCESS')); },
      error: () => { this.isRebuildingIndex.set(false); }
    });
  }

  rebuildUnifiedIndex(): void {
    this.isRebuildingUnifiedIndex.set(true);
    this.service.rebuildUnifiedIndex().pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isRebuildingUnifiedIndex.set(false); this.toastr.success(this.translate.instant('BIB.REBUILD_UNIFIED_INDEX_SUCCESS')); },
      error: () => { this.isRebuildingUnifiedIndex.set(false); }
    });
  }

  openConfirmRebuildFromScratch(): void { this.rebuildConfirmWord.set(''); this.showConfirmRebuildFromScratch.set(true); }

  confirmRebuildFromScratchExecute(): void {
    if (this.rebuildConfirmWord() !== 'XOA') return;
    this.showConfirmRebuildFromScratch.set(false);
    this.isRebuildingFromScratch.set(true);
    this.service.rebuildUnifiedIndexFromScratch().pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isRebuildingFromScratch.set(false); this.toastr.success(this.translate.instant('BIB.REBUILD_FROM_SCRATCH_SUCCESS')); },
      error: () => { this.isRebuildingFromScratch.set(false); }
    });
  }
}
