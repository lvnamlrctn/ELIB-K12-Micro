import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { FundService } from '../../../services/acquisition/fund.service';
import { BudgetService } from '../../../services/acquisition/budget.service';
import { Fund } from '../../../models/acquisition/fund';
import { Budget } from '../../../models/acquisition/budget';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-fund',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './fund.html'
})
export class FundPage implements OnInit, OnDestroy {
  private service       = inject(FundService);
  private budgetService = inject(BudgetService);
  private toastr        = inject(ToastrService);
  public  translate     = inject(TranslateService);
  private auth          = inject(Auth);
  private destroy$      = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'name', 'manager', 'purpose', 'blane', 'actions'];
  dataSource: Fund[] = [];
  selection = new SelectionModel<Fund>(true, []);
  budgets   = signal<Budget[]>([]);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal         = signal(false);
  editMode          = signal(false);
  currentId         = signal<number | null>(null);
  isLoading         = signal(false);
  isSaving          = signal(false);
  showConfirmDelete = signal(false);
  confirmDeleteId   = signal<number | null>(null);

  searchForm = new FormGroup({
    keyword: new FormControl<string>('', { nonNullable: true }),
  });

  dataForm = new FormGroup({
    Name:     new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Manager:  new FormControl<string>('', { nonNullable: true }),
    Purpose:  new FormControl<string>('', { nonNullable: true }),
    Note:     new FormControl<string>('', { nonNullable: true }),
    Blane:    new FormControl<number | null>(null),
    BudgetId: new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadBudgets();
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

  loadBudgets(): void {
    this.budgetService.search({ pageSize: 1000 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.budgets.set(res.data),
      error: () => {}
    });
  }

  getBudgetName(id: number | null | undefined): string {
    if (!id) return '—';
    return this.budgets().find(b => b.id === id)?.name || '—';
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
    this.currentId.set(null);
    this.dataForm.reset();
    this.showModal.set(true);
  }

  openEditModal(item: Fund): void {
    this.editMode.set(true);
    this.currentId.set(item.id);
    this.service.getById(item.id).pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.dataForm.patchValue({
        Name:     data.name     ?? '',
        Manager:  data.manager  ?? '',
        Purpose:  data.purpose  ?? '',
        Note:     data.note     ?? '',
        Blane:    data.blane    ?? null,
        BudgetId: data.budgetId ?? null,
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
    const payload: Partial<Fund> = {
      name:     v.Name,
      manager:  v.Manager  || undefined,
      purpose:  v.Purpose  || undefined,
      note:     v.Note     || undefined,
      blane:    v.Blane    ?? undefined,
      budgetId: v.BudgetId ?? undefined,
    };

    const id   = this.currentId();
    const mode = this.editMode();
    const req$ = mode && id !== null
      ? this.service.update(id, payload)
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

  handleDelete(item: Fund): void {
    this.confirmDeleteId.set(item.id);
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
      const reqs = this.selection.selected.map(item => this.service.delete(item.id));
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
