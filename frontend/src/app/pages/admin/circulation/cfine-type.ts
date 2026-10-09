import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CFineTypeService } from '../../../services/printbook/cfine-type.service';
import { BarcodeStatusService } from '../../../services/printbook/barcode-status.service';
import { CFineType, BarcodeStatus } from '../../../models/printbook/cfine-type';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-cfine-type',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './cfine-type.html'
})
export class CFineTypePage implements OnInit, OnDestroy {
  private service              = inject(CFineTypeService);
  private barcodeStatusService = inject(BarcodeStatusService);
  private toastr               = inject(ToastrService);
  public  translate            = inject(TranslateService);
  private auth                 = inject(Auth);
  private destroy$             = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'code', 'name', 'statusRegName', 'actions'];
  dataSource:       CFineType[]     = [];
  barcodeStatuses   = signal<BarcodeStatus[]>([]);
  selection         = new SelectionModel<CFineType>(true, []);

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
    Code:        new FormControl<string>('', { nonNullable: true }),
    Name:        new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    StatusRegId: new FormControl<string | null>(null),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadBarcodeStatuses();
    this.loadData();
  }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
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

  loadBarcodeStatuses(): void {
    this.barcodeStatusService.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => this.barcodeStatuses.set(list),
      error: () => {}
    });
  }

  // Lưu MÃ trạng thái (Barcode_Status.Id) — phiếu phạt dùng mã này để đặt Barcode.Status (dữ liệu cũ cũng lưu mã).
  getStatusValue(s: BarcodeStatus): string {
    return s.id != null ? String(s.id) : '';
  }

  barcodeStatusOptions = computed(() =>
    this.barcodeStatuses().map(s => ({ value: this.getStatusValue(s), label: s.commentStatus || s.name || s.id || '' }))
  );

  getStatusName(statusRegId: string | null | undefined): string {
    if (!statusRegId) return '—';
    const found = this.barcodeStatuses().find(s =>
      String(s.id) === statusRegId || s.publicId === statusRegId || s.code === statusRegId
    );
    return found?.commentStatus || found?.name || statusRegId;
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

  openAddModal(): void {
    this.editMode.set(false);
    this.currentPublicId.set(null);
    this.dataForm.reset();
    this.showModal.set(true);
  }

  openEditModal(item: CFineType): void {
    if (!item.publicId) return;
    this.editMode.set(true);
    this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.dataForm.patchValue({
        Code:        data.code        ?? '',
        Name:        data.name        ?? '',
        StatusRegId: data.statusRegId ?? null,
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
    const payload: Partial<CFineType> = {
      code:        v.Code        || undefined,
      name:        v.Name        || undefined,
      statusRegId: v.StatusRegId ?? undefined,
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

  handleDelete(item: CFineType): void {
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
}
