import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BadgeService } from '../../../services/gamification/badge.service';
import { Badge, BADGE_CRITERIA_TYPES } from '../../../models/gamification/badge';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

// Quản trị danh mục Huy hiệu đọc (Gamification) — CRUD thường, giống BibTypePage. Danh mục tenant-scoped
// (mỗi tenant tự định nghĩa bộ huy hiệu riêng) — TenantId được backend tự gán theo JWT khi tài khoản
// thường tạo mới; tài khoản đặc quyền (Đợt 24) có thêm ô chọn đơn vị để xem/lọc theo từng đơn vị. Việc
// cấp huy hiệu thật cho bạn đọc do BadgeEvaluationJob (Hangfire, chạy mỗi ngày) đảm nhiệm, trang này chỉ
// quản lý danh mục.
@Component({
  selector: 'app-badges',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './badges.html'
})
export class BadgesPage implements OnInit, OnDestroy {
  private service = inject(BadgeService);
  private toastr  = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth    = inject(Auth);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  readonly criteriaTypes = BADGE_CRITERIA_TYPES;

  displayedColumns: string[] = ['select', 'id', 'name', 'criteria', 'sortOrder', 'actions'];
  dataSource: Badge[] = [];
  selection = new SelectionModel<Badge>(true, []);

  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];
  keyword = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode  = signal(false);
  currentId = signal<string | null>(null);
  isLoading = signal(false);
  isSaving  = signal(false);
  showConfirmDelete = signal(false);
  confirmDeleteId   = signal<string | null>(null);

  dataForm = new FormGroup({
    Code:         new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Name:         new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Description:  new FormControl<string>('', { nonNullable: true }),
    IconName:     new FormControl<string>('military_tech', { nonNullable: true }),
    CriteriaType: new FormControl<string>('TotalDigitalReads', { nonNullable: true, validators: [Validators.required] }),
    Threshold:    new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    SortOrder:    new FormControl<number | null>(null)
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  onPageChange(e: PageEvent): void {
    this.pageIndex = e.pageIndex;
    this.pageSize  = e.pageSize;
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search(this.keyword, this.pageIndex + 1, this.pageSize, this.tenantId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
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
    const el = document.getElementById('badgeSearch') as HTMLInputElement;
    this.keyword   = el?.value || '';
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  criteriaLabel(type: string): string {
    return this.criteriaTypes.find(c => c.value === type)?.label || type;
  }

  isAllSelected(): boolean {
    return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length;
  }

  masterToggle(): void {
    this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r));
  }

  openAddModal(): void {
    this.editMode.set(false);
    this.currentId.set(null);
    this.dataForm.reset({ Code: '', Name: '', Description: '', IconName: 'military_tech', CriteriaType: 'TotalDigitalReads', Threshold: null, SortOrder: null });
    this.showModal.set(true);
  }

  openEditModal(item: Badge): void {
    this.editMode.set(true);
    this.currentId.set(item.publicId ?? null);
    this.dataForm.patchValue({
      Code:         item.code         || '',
      Name:         item.name         || '',
      Description:  item.description  || '',
      IconName:     item.iconName     || 'military_tech',
      CriteriaType: item.criteriaType || 'TotalDigitalReads',
      Threshold:    item.threshold    ?? null,
      SortOrder:    item.sortOrder    ?? null
    });
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    this.isSaving.set(true);
    const v = this.dataForm.getRawValue();
    const payload: Partial<Badge> = {
      code: v.Code, name: v.Name, description: v.Description, iconName: v.IconName,
      criteriaType: v.CriteriaType, threshold: v.Threshold ?? 0, sortOrder: v.SortOrder
    };

    const req$ = this.editMode() && this.currentId()
      ? this.service.update(this.currentId()!, payload)
      : this.service.create(payload);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeModal();
        if (!this.editMode()) {
          this.pageIndex = 0;
          if (this.paginator) this.paginator.pageIndex = 0;
        }
        this.loadData();
        this.toastr.success(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: () => {
        this.isSaving.set(false);
      }
    });
  }

  handleDelete(publicId: string): void {
    this.confirmDeleteId.set(publicId);
    this.showConfirmDelete.set(true);
  }

  deleteSelected(): void {
    if (!this.selection.selected.length) return;
    this.confirmDeleteId.set(null);
    this.showConfirmDelete.set(true);
  }

  closeConfirm(): void {
    this.showConfirmDelete.set(false);
    this.confirmDeleteId.set(null);
  }

  confirmActionExecute(): void {
    const id = this.confirmDeleteId();
    if (id === null) {
      const reqs = this.selection.selected.filter(i => !!i.publicId).map(item => this.service.delete(item.publicId!));
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
    this.service.delete(id).pipe(takeUntil(this.destroy$)).subscribe({
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
