import { Component, inject, OnInit, OnDestroy, AfterViewInit, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { MapShelvingService } from '../../../services/printbook/map-shelving.service';
import { StoreService } from '../../../services/printbook/store.service';
import { MapUnshelvedBarcode, FloorWithShelves, ShelfNode } from '../../../models/printbook/map-shelving';
import { Store } from '../../../models/printbook/store';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-map-shelving',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './map-shelving.html'
})
export class MapShelvingPage implements OnInit, OnDestroy, AfterViewInit {
  private service   = inject(MapShelvingService);
  private storeSvc  = inject(StoreService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  stores = signal<Store[]>([]);

  searchForm = new FormGroup({
    receiptCode: new FormControl<string>('', { nonNullable: true }),
    barcodeFrom: new FormControl<string>('', { nonNullable: true }),
    barcodeTo:   new FormControl<string>('', { nonNullable: true }),
    storeId:     new FormControl<number | null>(null),
  });

  displayedColumns = ['select', 'barcode', 'bibTitle', 'ddc', 'suggestion'];
  dataSource = new MatTableDataSource<MapUnshelvedBarcode>([]);
  selection = new SelectionModel<MapUnshelvedBarcode>(true, []);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [10, 25, 50, 100];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  isLoading = signal(false);
  isPlacing = signal(false);

  // Sơ đồ chọn vị trí — chỉ đọc/chọn, không kéo-thả/resize.
  floors          = signal<FloorWithShelves[]>([]);
  selectedFloorId = signal<number | null>(null);
  selectedShelfId = signal<number | null>(null);
  selectedRowId   = signal<number | null>(null);

  currentFloor  = computed(() => this.floors().find(f => f.id === this.selectedFloorId()) || null);
  selectedShelf = computed(() => this.currentFloor()?.shelves.find(s => s.id === this.selectedShelfId()) || null);

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.loadData();
  }
  ngAfterViewInit(): void {
    this.paginator?.page.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.pageIndex = this.paginator.pageIndex; this.pageSize = this.paginator.pageSize; this.loadData();
    });
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  onStoreChange(): void {
    this.selectedFloorId.set(null);
    this.selectedShelfId.set(null);
    this.selectedRowId.set(null);
    this.floors.set([]);
    const storeId = this.searchForm.value.storeId;
    if (storeId) {
      this.service.getShelvesByStore(storeId).pipe(takeUntil(this.destroy$)).subscribe(list => {
        this.floors.set(list);
        if (list.length) this.selectedFloorId.set(list[0].id);
      });
    }
    this.triggerSearch();
  }

  onFloorChange(floorId: number): void {
    this.selectedFloorId.set(floorId);
    this.selectedShelfId.set(null);
    this.selectedRowId.set(null);
  }

  selectShelf(shelf: ShelfNode): void {
    this.selectedShelfId.set(shelf.id);
    this.selectedRowId.set(null);
    // Nếu tất cả số KCB đang chọn cùng gợi ý đúng giá này và cùng 1 ngăn → tự chọn sẵn ngăn đó.
    const selected = this.selection.selected;
    if (selected.length > 0 && selected.every(b => b.suggestedMapObjectId === shelf.id && b.suggestedShelfRowId != null)) {
      const rowIds = new Set(selected.map(b => b.suggestedShelfRowId));
      if (rowIds.size === 1) this.selectedRowId.set(selected[0].suggestedShelfRowId!);
    }
  }

  geomFor(shelf: ShelfNode): { x: number; y: number; w: number; h: number } {
    return { x: shelf.positionX ?? 0, y: shelf.positionY ?? 0, w: shelf.width ?? 10, h: shelf.height ?? 10 };
  }

  loadData(): void {
    this.isLoading.set(true);
    const v = this.searchForm.getRawValue();
    this.service.searchUnshelved({
      receiptCode: v.receiptCode || null, barcodeFrom: v.barcodeFrom || null, barcodeTo: v.barcodeTo || null, storeId: v.storeId,
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource.data = res.data; this.totalRecords = res.recordsTotal; this.selection.clear(); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }

  isAllSelected(): boolean { return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.data.length; }
  toggleAllRows(): void { this.isAllSelected() ? this.selection.clear() : this.selection.select(...this.dataSource.data); }

  suggestionLabel(row: MapUnshelvedBarcode): string {
    if (!row.suggestedMapObjectId) return this.translate.instant('MAP_SHELVING.NO_SUGGESTION');
    const rowPart = row.suggestedRowIndex != null ? ` — ${this.translate.instant('MAP_SHELVING.ROW')} ${row.suggestedRowIndex}` : '';
    return `${row.suggestedShelfName}${rowPart}`;
  }

  placeSelected(): void {
    const ids     = this.selection.selected.map(r => r.id);
    const shelfId = this.selectedShelfId();
    if (ids.length === 0 || !shelfId) return;
    this.isPlacing.set(true);
    this.service.place(ids, shelfId, this.selectedRowId()).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isPlacing.set(false); this.toastr.success(this.translate.instant('MAP_SHELVING.PLACE_SUCCESS')); this.loadData(); },
      error: () => { this.isPlacing.set(false); }
    });
  }
}
