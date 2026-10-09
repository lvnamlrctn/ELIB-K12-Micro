import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { MapFloorService } from '../../../services/map/map-floor.service';
import { MapBuildingService } from '../../../services/map/map-building.service';
import { MapFloor } from '../../../models/map/map-floor';
import { MapBuilding } from '../../../models/map/map-building';
import { CanDirective } from '../../../directives/can.directive';
import { MapFloorUtilityModalComponent } from '../../../shared/map/map-floor-utility-modal';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-map-floor',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, MapFloorUtilityModalComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './map-floor.html'
})
export class MapFloorPage implements OnInit, OnDestroy {
  private service         = inject(MapFloorService);
  private listState = inject(ListPageStateService);
  private buildingService = inject(MapBuildingService);
  private toastr          = inject(ToastrService);
  private router          = inject(Router);
  private route           = inject(ActivatedRoute);
  public  translate       = inject(TranslateService);
  private auth            = inject(Auth);
  private destroy$        = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'name', 'buildingName', 'floorNumber', 'width', 'height', 'actions'];
  dataSource: MapFloor[] = [];
  buildings  = signal<MapBuilding[]>([]);
  selection  = new SelectionModel<MapFloor>(true, []);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal             = signal(false);
  editMode              = signal(false);
  currentPublicId       = signal<string | null>(null);
  isLoading             = signal(false);
  isSaving              = signal(false);
  showConfirmDelete     = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  // Floor Utility modal
  utilityModalOpen  = signal(false);
  utilityModalFloor = signal<{ id: number; name: string } | null>(null);

  searchForm = new FormGroup({
    keyword:    new FormControl<string>('', { nonNullable: true }),
    buildingId: new FormControl<number | null>(null),
  });

  dataForm = new FormGroup({
    Name:           new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    BuildingId:     new FormControl<number | null>(null, [Validators.required]),
    FloorNumber:    new FormControl<number>(1, { nonNullable: true }),
    Width:          new FormControl<number>(1200, { nonNullable: true }),
    Height:         new FormControl<number>(800, { nonNullable: true }),
    LayoutImageUrl: new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    const saved = this.listState.recall('map.map-floor'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    const qBuildingId = this.route.snapshot.queryParamMap.get('buildingId');
    if (qBuildingId) this.searchForm.patchValue({ buildingId: Number(qBuildingId) });
    this.loadBuildings();
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadBuildings(): void {
    this.buildingService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.buildings.set(list));
  }

  getBuildingName(buildingId: number): string {
    return this.buildings().find(b => b.id === buildingId)?.name || `#${buildingId}`;
  }

  loadData(): void {
    this.listState.remember('map.map-floor', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({ keyword: s.keyword || null, buildingId: s.buildingId, tenantId: this.tenantId, pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
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
    this.editMode.set(false);
    this.currentPublicId.set(null);
    const preselectBuilding = this.searchForm.value.buildingId ?? (this.buildings()[0]?.id ?? null);
    this.dataForm.reset({ Name: '', BuildingId: preselectBuilding, FloorNumber: 1, Width: 1200, Height: 800, LayoutImageUrl: '' });
    this.showModal.set(true);
  }

  openEditModal(item: MapFloor): void {
    if (!item.publicId) return;
    this.editMode.set(true);
    this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.dataForm.patchValue({
        Name:           data.name           ?? '',
        BuildingId:     data.buildingId,
        FloorNumber:    data.floorNumber     ?? 1,
        Width:          data.width           ?? 1200,
        Height:         data.height          ?? 800,
        LayoutImageUrl: data.layoutImageUrl  ?? '',
      });
      this.showModal.set(true);
    });
  }

  closeModal(): void { this.showModal.set(false); this.dataForm.reset(); }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<MapFloor> = {
      name: v.Name, buildingId: v.BuildingId!, floorNumber: v.FloorNumber,
      width: v.Width, height: v.Height, layoutImageUrl: v.LayoutImageUrl || undefined,
    };

    const publicId = this.currentPublicId();
    const mode     = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeModal();
        if (!mode) { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; }
        this.loadData();
        this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: () => { this.isSaving.set(false); }
    });
  }

  manageObjects(item: MapFloor): void {
    this.router.navigate(['/admin/map-objects'], { queryParams: { floorId: item.id } });
  }

  viewOverview(item: MapFloor): void {
    this.router.navigate(['/admin/map-floor-overview'], { queryParams: { floorId: item.id } });
  }

  handleDelete(item: MapFloor): void {
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

  // ── Floor Utility ────────────────────────────────────────────────────────
  openUtilityModal(row: MapFloor): void {
    this.utilityModalFloor.set({ id: row.id, name: row.name || '' });
    this.utilityModalOpen.set(true);
  }
}
