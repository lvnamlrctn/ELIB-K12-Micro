import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { Router, ActivatedRoute } from '@angular/router';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CabinetService } from '../../../services/printbook/cabinet.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { CabinetCompartmentService } from '../../../services/printbook/cabinet-compartment.service';
import { Cabinet } from '../../../models/printbook/cabinet';
import { CircPlace } from '../../../models/printbook/circ-place';
import { CabinetCompartment } from '../../../models/printbook/cabinet-compartment';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-cabinet',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './cabinet.html'
})
export class CabinetPage implements OnInit, OnDestroy {
  private service          = inject(CabinetService);
  private circPlaceSvc     = inject(CircPlaceService);
  private compartmentSvc   = inject(CabinetCompartmentService);
  private toastr           = inject(ToastrService);
  public  translate        = inject(TranslateService);
  private router           = inject(Router);
  private route            = inject(ActivatedRoute);
  private auth             = inject(Auth);
  private destroy$         = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  goToMap(): void { this.router.navigate(['/admin/cabinet-map']); }

  displayedColumns = ['select', 'stt', 'code', 'name', 'circPlaceName', 'status', 'actions'];
  dataSource:  Cabinet[] = [];
  selection    = new SelectionModel<Cabinet>(true, []);

  circPlaces   = signal<CircPlace[]>([]);

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

  searchForm = new FormGroup({
    keyword: new FormControl<string>('', { nonNullable: true }),
  });

  dataForm = new FormGroup({
    Name:        new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Code:        new FormControl<string>('', { nonNullable: true }),
    CircPlaceId: new FormControl<number | null>(null),
    Note:        new FormControl<string>('', { nonNullable: true }),
    Status:      new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadCircPlaces();
    this.loadData();

    const openCompartmentsId = this.route.snapshot.queryParamMap.get('openCompartments');
    if (openCompartmentsId) {
      this.service.getById(openCompartmentsId).pipe(takeUntil(this.destroy$)).subscribe(cabinet => {
        if (cabinet) this.openCompartmentModal(cabinet);
      });
    }
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadCircPlaces(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe(data => this.circPlaces.set(data));
  }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({
      keyword:   s.keyword || null,
      pageIndex: this.pageIndex + 1,
      pageSize:  this.pageSize,
      tenantId:  this.tenantId,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.dataSource   = res.data;
        this.totalRecords = res.recordsTotal;
        this.isLoading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  triggerSearch(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  getRowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  isAllSelected(): boolean {
    return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length;
  }

  masterToggle(): void {
    this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r));
  }

  getCircPlaceName(circPlaceId?: number): string {
    if (circPlaceId == null) return '—';
    const found = this.circPlaces().find(c => c.id === circPlaceId);
    return found?.name ?? String(circPlaceId);
  }

  private editingCabinet: Cabinet | null = null;

  openAddModal(): void {
    this.editMode.set(false);
    this.currentPublicId.set(null);
    this.editingCabinet = null;
    this.dataForm.reset();
    this.showModal.set(true);
  }

  openEditModal(item: Cabinet): void {
    if (!item.publicId) return;
    this.editMode.set(true);
    this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.editingCabinet = data;
      this.dataForm.patchValue({
        Name:        data.name        ?? '',
        Code:        data.code        ?? '',
        CircPlaceId: data.circPlaceId ?? null,
        Note:        data.note        ?? '',
        Status:      data.status      ?? null,
      });
      this.showModal.set(true);
    });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<Cabinet> = {
      name:        v.Name        || undefined,
      code:        v.Code        || undefined,
      circPlaceId: v.CircPlaceId ?? undefined,
      note:        v.Note        || undefined,
      status:      v.Status      ?? undefined,
      positionX:   this.editingCabinet?.positionX,
      positionY:   this.editingCabinet?.positionY,
      rows:        this.editingCabinet?.rows,
      cols:        this.editingCabinet?.cols,
    };

    const publicId = this.currentPublicId();
    const mode     = this.editMode();
    const req$ = mode && publicId !== null
      ? this.service.update(publicId, payload)
      : this.service.create(payload);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeModal();
        if (!mode) {
          this.pageIndex = 0;
          if (this.paginator) this.paginator.pageIndex = 0;
        }
        this.loadData();
        this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: () => {
        this.isSaving.set(false);
      }
    });
  }

  handleDelete(item: Cabinet): void {
    if (!item.publicId) return;
    this.confirmDeletePublicId.set(item.publicId);
    this.showConfirmDelete.set(true);
  }

  deleteSelected(): void {
    if (!this.selection.selected.length) return;
    this.confirmDeletePublicId.set(null);
    this.showConfirmDelete.set(true);
  }

  closeConfirm(): void {
    this.showConfirmDelete.set(false);
    this.confirmDeletePublicId.set(null);
  }

  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId();
    if (publicId === null) {
      const reqs = this.selection.selected
        .filter(item => !!item.publicId)
        .map(item => this.service.delete(item.publicId!));
      forkJoin(reqs).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.selection.clear();
          this.closeConfirm();
          this.loadData();
        },
        error: () => {
          this.closeConfirm();
          this.loadData();
        }
      });
      return;
    }
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        this.closeConfirm();
        this.loadData();
      },
      error: () => {
        this.closeConfirm();
      }
    });
  }

  // --- Ngăn tủ (compartments) ---

  showCompartmentModal = signal(false);
  compartmentCabinet   = signal<Cabinet | null>(null);
  compartmentItems     = signal<CabinetCompartment[]>([]);
  isLoadingCompartments = signal(false);
  isSavingGridSize      = signal(false);

  gridSizeForm = new FormGroup({
    Rows: new FormControl<number>(2, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    Cols: new FormControl<number>(2, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
  });

  showCompartmentCellModal = signal(false);
  compartmentCellForm = new FormGroup({
    Code: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Name: new FormControl<string>('', { nonNullable: true }),
  });
  private compartmentCellTarget: { row: number; col: number; existing?: CabinetCompartment } | null = null;
  isSavingCompartmentCell = signal(false);

  gridCells = computed(() => {
    const cabinet = this.compartmentCabinet();
    if (!cabinet?.rows || !cabinet?.cols) return [];
    const items = this.compartmentItems();
    const cells: { row: number; col: number; item?: CabinetCompartment }[] = [];
    for (let r = 0; r < cabinet.rows; r++) {
      for (let c = 0; c < cabinet.cols; c++) {
        cells.push({ row: r, col: c, item: items.find(i => i.rowIndex === r && i.colIndex === c) });
      }
    }
    return cells;
  });

  openCompartmentModal(cabinet: Cabinet): void {
    this.compartmentCabinet.set(cabinet);
    this.gridSizeForm.setValue({ Rows: cabinet.rows || 2, Cols: cabinet.cols || 2 });
    this.showCompartmentModal.set(true);
    if (cabinet.rows && cabinet.cols) this.loadCompartments(cabinet);
  }

  closeCompartmentModal(): void {
    this.showCompartmentModal.set(false);
    this.compartmentCabinet.set(null);
    this.compartmentItems.set([]);
  }

  loadCompartments(cabinet: Cabinet): void {
    if (!cabinet.publicId) return;
    this.isLoadingCompartments.set(true);
    this.compartmentSvc.getByCabinet(cabinet.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: grid => { this.compartmentItems.set(grid.items || []); this.isLoadingCompartments.set(false); },
      error: () => { this.isLoadingCompartments.set(false); }
    });
  }

  saveGridSize(): void {
    const cabinet = this.compartmentCabinet();
    if (!cabinet?.publicId || this.gridSizeForm.invalid) { this.gridSizeForm.markAllAsTouched(); return; }
    const v = this.gridSizeForm.getRawValue();
    this.isSavingGridSize.set(true);
    this.service.update(cabinet.publicId, {
      name: cabinet.name, code: cabinet.code, circPlaceId: cabinet.circPlaceId, note: cabinet.note, status: cabinet.status,
      positionX: cabinet.positionX, positionY: cabinet.positionY,
      rows: v.Rows, cols: v.Cols
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSavingGridSize.set(false);
        const updated = { ...cabinet, rows: v.Rows, cols: v.Cols };
        this.compartmentCabinet.set(updated);
        this.loadCompartments(updated);
        this.loadData();
      },
      error: () => { this.isSavingGridSize.set(false); }
    });
  }

  openAddCompartmentCell(row: number, col: number): void {
    this.compartmentCellTarget = { row, col };
    this.compartmentCellForm.reset({ Code: '', Name: '' });
    this.showCompartmentCellModal.set(true);
  }

  openEditCompartmentCell(row: number, col: number, item: CabinetCompartment): void {
    this.compartmentCellTarget = { row, col, existing: item };
    this.compartmentCellForm.setValue({ Code: item.code || '', Name: item.name || '' });
    this.showCompartmentCellModal.set(true);
  }

  closeCompartmentCellModal(): void {
    this.showCompartmentCellModal.set(false);
    this.compartmentCellTarget = null;
  }

  saveCompartmentCell(): void {
    const cabinet = this.compartmentCabinet();
    const target = this.compartmentCellTarget;
    if (!cabinet || !target || this.compartmentCellForm.invalid) { this.compartmentCellForm.markAllAsTouched(); return; }
    const v = this.compartmentCellForm.getRawValue();
    this.isSavingCompartmentCell.set(true);
    const req$ = target.existing?.publicId
      ? this.compartmentSvc.update(target.existing.publicId, { code: v.Code, name: v.Name })
      : this.compartmentSvc.create({ cabinetId: cabinet.id, rowIndex: target.row, colIndex: target.col, code: v.Code, name: v.Name });
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSavingCompartmentCell.set(false);
        this.closeCompartmentCellModal();
        this.loadCompartments(cabinet);
        this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS'));
      },
      error: () => { this.isSavingCompartmentCell.set(false); }
    });
  }

  deleteCompartmentCell(item: CabinetCompartment): void {
    const cabinet = this.compartmentCabinet();
    if (!cabinet || !item.publicId) return;
    this.compartmentSvc.delete(item.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.loadCompartments(cabinet); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); },
      error: () => {}
    });
  }
}
