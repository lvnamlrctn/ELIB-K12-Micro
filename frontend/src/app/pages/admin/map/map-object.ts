import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin, firstValueFrom } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { MapObjectService } from '../../../services/map/map-object.service';
import { MapFloorService } from '../../../services/map/map-floor.service';
import { MapShelfDetailService } from '../../../services/map/map-shelf-detail.service';
import { MapShelfRowService } from '../../../services/map/map-shelf-row.service';
import { StoreService } from '../../../services/printbook/store.service';
import { MapObject, MAP_OBJECT_TYPES, MAP_OBJECT_CATEGORIES } from '../../../models/map/map-object';
import { MapFloor } from '../../../models/map/map-floor';
import { Store } from '../../../models/printbook/store';
import { MapShelfRow } from '../../../models/map/map-shelf-row';
import { CanDirective } from '../../../directives/can.directive';
import { MapObjectFormModalComponent, MapObjectFormInitial } from '../../../shared/map/map-object-form-modal';
import { MapObjectEquipmentModalComponent } from '../../../shared/map/map-object-equipment-modal';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-map-object',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, MapObjectFormModalComponent, MapObjectEquipmentModalComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './map-object.html'
})
export class MapObjectPage implements OnInit, OnDestroy {
  private service       = inject(MapObjectService);
  private floorService  = inject(MapFloorService);
  private shelfDetailSvc= inject(MapShelfDetailService);
  private shelfRowSvc   = inject(MapShelfRowService);
  private storeService  = inject(StoreService);
  private toastr        = inject(ToastrService);
  private route         = inject(ActivatedRoute);
  public  translate     = inject(TranslateService);
  private auth          = inject(Auth);
  private destroy$      = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  readonly objectTypes = MAP_OBJECT_TYPES;
  readonly categories  = MAP_OBJECT_CATEGORIES;

  displayedColumns = ['select', 'stt', 'name', 'code', 'floorName', 'objectType', 'category', 'actions'];
  dataSource: MapObject[] = [];
  floors     = signal<MapFloor[]>([]);
  stores     = signal<Store[]>([]);
  selection  = new SelectionModel<MapObject>(true, []);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  formOpen               = signal(false);
  formInitial            = signal<MapObjectFormInitial | null>(null);
  private formWasEdit    = false;
  isLoading              = signal(false);
  showConfirmDelete      = signal(false);
  confirmDeletePublicId  = signal<string | null>(null);

  // Shelf row modal (quản lý ngăn kệ theo DDC)
  showShelfRowModal    = signal(false);
  activeShelfObjectName= signal('');
  shelfRowsList        = signal<MapShelfRow[]>([]);
  newShelfRow = { rowIndex: 1, ddcStart: '', ddcEnd: '', description: '' };
  private activeShelfDetailId: number | null = null;

  // Equipment modal
  equipmentModalOpen   = signal(false);
  equipmentModalObject = signal<{ id: number; name: string } | null>(null);

  searchForm = new FormGroup({
    keyword:    new FormControl<string>('', { nonNullable: true }),
    floorId:    new FormControl<number | null>(null),
    objectType: new FormControl<string | null>(null),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    const qFloorId = this.route.snapshot.queryParamMap.get('floorId');
    if (qFloorId) this.searchForm.patchValue({ floorId: Number(qFloorId) });
    this.loadFloors();
    this.loadStores();
    this.loadData();
  }

  loadStores(): void {
    this.storeService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.stores.set(list));
  }

  getStoreName(storeId?: number): string {
    if (!storeId) return '—';
    return this.stores().find(s => s.id === storeId)?.name || `#${storeId}`;
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadFloors(): void {
    this.floorService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.floors.set(list));
  }

  getFloorName(floorId: number): string {
    return this.floors().find(f => f.id === floorId)?.name || `#${floorId}`;
  }

  typeLabel(type?: string): string { return this.objectTypes.find(t => t.value === type)?.label || type || '—'; }
  typeIcon(type?: string): string  { return this.objectTypes.find(t => t.value === type)?.icon  || 'category'; }
  categoryLabel(cat?: number): string { return this.categories.find(c => c.value === cat)?.label || '—'; }

  loadData(): void {
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({ keyword: s.keyword || null, floorId: s.floorId, objectType: s.objectType, tenantId: this.tenantId, pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }

  triggerSearch(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  onTenantChange(): void { this.triggerSearch(); }

  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  isAllSelected(): boolean {
    return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length;
  }

  masterToggle(): void {
    this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r));
  }

  openAddModal(): void {
    const preselectFloor = this.searchForm.value.floorId ?? (this.floors()[0]?.id ?? null);
    this.formInitial.set({
      objectId: null, publicId: null,
      name: '', code: '', floorId: preselectFloor, objectType: 'ROOM', category: 4,
      colorHex: '#3b82f6', iconName: '', storeId: null,
      positionX: 10, positionY: 10, width: 15, height: 10,
      shelfDetail: null,
    });
    this.formWasEdit = false;
    this.formOpen.set(true);
  }

  openEditModal(item: MapObject): void {
    if (!item.publicId) return;
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(data => {
      const setInitial = (shelfDetail: MapObjectFormInitial['shelfDetail']) => {
        this.formInitial.set({
          objectId: item.id, publicId: item.publicId!,
          name: data.name ?? '', code: data.code ?? '', floorId: data.floorId,
          objectType: data.objectType ?? 'ROOM', category: data.category ?? 4,
          colorHex: data.colorHex ?? '#3b82f6', iconName: data.iconName ?? '', storeId: data.storeId ?? null,
          positionX: data.positionX ?? 10, positionY: data.positionY ?? 10, width: data.width ?? 15, height: data.height ?? 10,
          shelfDetail,
        });
        this.formWasEdit = true;
        this.formOpen.set(true);
      };
      if (data.objectType === 'SHELF') {
        this.shelfDetailSvc.getByObjectId(item.id).pipe(takeUntil(this.destroy$)).subscribe(detail => {
          setInitial(detail ? {
            id: detail.id, publicId: detail.publicId ?? null,
            categoryRange: detail.categoryRange ?? '', subjectName: detail.subjectName ?? '',
            capacity: detail.capacity ?? 100, description: detail.description ?? '',
          } : null);
        });
      } else {
        setInitial(null);
      }
    });
  }

  onFormSaved(): void {
    this.formOpen.set(false);
    if (!this.formWasEdit) { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; }
    this.loadData();
    this.toastr.success(this.translate.instant(this.formWasEdit ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
  }

  handleDelete(item: MapObject): void {
    if (!item.publicId) return;
    this.confirmDeletePublicId.set(item.publicId);
    this.showConfirmDelete.set(true);
  }

  deleteSelected(): void {
    if (!this.selection.selected.length) return;
    this.confirmDeletePublicId.set(null);
    this.showConfirmDelete.set(true);
  }

  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }

  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId();
    if (publicId === null) {
      const reqs = this.selection.selected.filter(item => !!item.publicId).map(item => this.service.delete(item.publicId!));
      forkJoin(reqs).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.selection.clear(); this.closeConfirm(); this.loadData(); },
        error: () => { this.closeConfirm(); this.loadData(); }
      });
      return;
    }
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }

  // ── Shelf rows (ngăn kệ theo DDC) ────────────────────────────────────────
  async openShelfRowModal(item: MapObject): Promise<void> {
    this.activeShelfObjectName.set(item.name || '');
    this.newShelfRow = { rowIndex: 1, ddcStart: '', ddcEnd: '', description: '' };
    const detail = await firstValueFrom(this.shelfDetailSvc.getByObjectId(item.id));
    if (!detail) {
      this.toastr.error(this.translate.instant('MAP_OBJECT.NO_SHELF_DETAIL'));
      return;
    }
    this.activeShelfDetailId = detail.id;
    this.showShelfRowModal.set(true);
    this.loadShelfRows();
  }

  closeShelfRowModal(): void { this.showShelfRowModal.set(false); this.activeShelfDetailId = null; this.shelfRowsList.set([]); }

  loadShelfRows(): void {
    if (!this.activeShelfDetailId) return;
    this.shelfRowSvc.searchAllByShelfDetail(this.activeShelfDetailId).pipe(takeUntil(this.destroy$)).subscribe(list => this.shelfRowsList.set(list));
  }

  async addShelfRow(): Promise<void> {
    if (!this.activeShelfDetailId || !this.newShelfRow.ddcStart || !this.newShelfRow.ddcEnd) return;
    const payload = { shelfDetailId: this.activeShelfDetailId, ...this.newShelfRow };
    try {
      await firstValueFrom(this.shelfRowSvc.create(payload));
      this.newShelfRow = { rowIndex: this.newShelfRow.rowIndex + 1, ddcStart: '', ddcEnd: '', description: '' };
      this.loadShelfRows();
    } catch { }
  }

  async deleteShelfRow(row: MapShelfRow): Promise<void> {
    if (!row.publicId) return;
    try { await firstValueFrom(this.shelfRowSvc.delete(row.publicId)); this.loadShelfRows(); }
    catch { }
  }

  // ── Equipment ─────────────────────────────────────────────────────────────
  openEquipmentModal(item: MapObject): void {
    this.equipmentModalObject.set({ id: item.id, name: item.name || '' });
    this.equipmentModalOpen.set(true);
  }
}
