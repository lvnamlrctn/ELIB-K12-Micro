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
import { CurrencyService } from '../../../services/acquisition/currency.service';
import { Currency } from '../../../models/acquisition/currency';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-currency',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './currency.html'
})
export class CurrencyPage implements OnInit, OnDestroy {
  private service   = inject(CurrencyService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'code', 'name', 'exchangeRate', 'status', 'actions'];
  dataSource: Currency[] = [];
  selection = new SelectionModel<Currency>(true, []);

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
    status:  new FormControl<number | null>(null),
  });

  dataForm = new FormGroup({
    Code:         new FormControl<string>('',   { nonNullable: true, validators: [Validators.required] }),
    Name:         new FormControl<string>('',   { nonNullable: true, validators: [Validators.required] }),
    ExchangeRate: new FormControl<number | null>(null),
    Status:       new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
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

  loadData(): void {
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({
      keyword:   s.keyword || null,
      status:    s.status  ?? null,
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

  openEditModal(item: Currency): void {
    this.editMode.set(true);
    this.currentId.set(item.id);
    this.service.getById(item.id).pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.dataForm.patchValue({
        Code:         data.code         ?? '',
        Name:         data.name         ?? '',
        ExchangeRate: data.exchangeRate  ?? null,
        Status:       data.status        ?? null,
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
    const payload: Partial<Currency> = {
      code:         v.Code,
      name:         v.Name,
      exchangeRate: v.ExchangeRate ?? undefined,
      status:       v.Status       ?? undefined,
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

  toggleStatus(item: Currency): void {
    this.service.toggleStatus(item.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { item.status = item.status === 1 ? 0 : 1; },
      error: () => {}
    });
  }

  handleDelete(item: Currency): void {
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
