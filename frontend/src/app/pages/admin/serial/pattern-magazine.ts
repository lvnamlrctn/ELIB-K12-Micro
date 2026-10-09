import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { PatternMagazineService } from '../../../services/serial/pattern-magazine.service';
import { PatternMagazine } from '../../../models/serial/pattern-magazine';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-pattern-magazine',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './pattern-magazine.html'
})
export class PatternMagazinePage implements OnInit, OnDestroy {
  private service   = inject(PatternMagazineService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'name', 'description', 'function', 'order', 'actions'];
  dataSource: PatternMagazine[] = [];
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode = signal(false);
  currentPublicId = signal<string | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({ keyword: new FormControl<string>('', { nonNullable: true }) });
  dataForm = new FormGroup({
    Name:        new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Description: new FormControl<string>('', { nonNullable: true }),
    Function:    new FormControl<string>('', { nonNullable: true }),
    Order:       new FormControl<number>(0, { nonNullable: true }),
    // chi tiết đánh số
    X: new FormControl<string>('', { nonNullable: true }), Y: new FormControl<string>('', { nonNullable: true }), Z: new FormControl<string>('', { nonNullable: true }),
    StepX: new FormControl<number>(1, { nonNullable: true }), StepY: new FormControl<number>(1, { nonNullable: true }), StepZ: new FormControl<number>(1, { nonNullable: true }),
    RepeatX: new FormControl<number>(0, { nonNullable: true }), RepeatY: new FormControl<number>(0, { nonNullable: true }), RepeatZ: new FormControl<number>(0, { nonNullable: true }),
    MaxX: new FormControl<number>(0, { nonNullable: true }), MaxY: new FormControl<number>(0, { nonNullable: true }), MaxZ: new FormControl<number>(0, { nonNullable: true }),
    ResetX: new FormControl<number>(0, { nonNullable: true }), ResetY: new FormControl<number>(0, { nonNullable: true }), ResetZ: new FormControl<number>(0, { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search({ keyword: this.searchForm.getRawValue().keyword || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void {
    this.editMode.set(false); this.currentPublicId.set(null);
    this.dataForm.reset({ Name: '', Description: '', Function: '', Order: 0, X: '', Y: '', Z: '', StepX: 1, StepY: 1, StepZ: 1, RepeatX: 0, RepeatY: 0, RepeatZ: 0, MaxX: 0, MaxY: 0, MaxZ: 0, ResetX: 0, ResetY: 0, ResetZ: 0 });
    this.showModal.set(true);
  }
  openEditModal(item: PatternMagazine): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      const det = d.detail || {};
      this.dataForm.patchValue({
        Name: d.name ?? '', Description: d.description ?? '', Function: d.function ?? '', Order: d.order ?? 0,
        X: det.x ?? '', Y: det.y ?? '', Z: det.z ?? '',
        StepX: det.stepX ?? 1, StepY: det.stepY ?? 1, StepZ: det.stepZ ?? 1,
        RepeatX: det.repeatX ?? 0, RepeatY: det.repeatY ?? 0, RepeatZ: det.repeatZ ?? 0,
        MaxX: det.maxX ?? 0, MaxY: det.maxY ?? 0, MaxZ: det.maxZ ?? 0,
        ResetX: det.resetX ?? 0, ResetY: det.resetY ?? 0, ResetZ: det.resetZ ?? 0,
      });
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<PatternMagazine> = {
      name: v.Name, description: v.Description, function: v.Function, order: v.Order,
      detail: {
        x: v.X, y: v.Y, z: v.Z,
        stepX: v.StepX, stepY: v.StepY, stepZ: v.StepZ,
        repeatX: v.RepeatX, repeatY: v.RepeatY, repeatZ: v.RepeatZ,
        maxX: v.MaxX, maxY: v.MaxY, maxZ: v.MaxZ,
        resetX: v.ResetX, resetY: v.ResetY, resetZ: v.ResetZ,
      }
    };
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); (mode ? this.loadData() : this.triggerSearch()); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  handleDelete(item: PatternMagazine): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
