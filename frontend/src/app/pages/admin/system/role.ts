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
import { RoleService } from '../../../services/system/role.service';
import { Role } from '../../../models/system/role';
import { DataTableParams } from '../../../models/shared/datatable';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-role',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './role.html'
})
export class RolePage implements OnInit, OnDestroy {
  private roleService = inject(RoleService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);
  private auth = inject(Auth);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns: string[] = ['select', 'id', 'name', 'code', 'app', 'actions'];
  dataSource: Role[] = [];
  selection = new SelectionModel<Role>(true, []);

  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];
  keyword = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode = signal(false);
  currentId = signal<string | null>(null);
  isLoading = signal(false);

  showConfirmDelete = signal(false);
  confirmDeleteId = signal<string | null>(null);

  dataForm = new FormGroup({
    Name: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Code: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    App:  new FormControl<string>('', { nonNullable: true })
  });

  ngOnInit() {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadData();
  }

  onTenantChange(): void { this.refreshTable(); }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    const params: DataTableParams = {
      draw: 1,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.keyword, regex: false },
      tenantId: this.tenantId
    };
    this.isLoading.set(true);
    this.roleService.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      next: (res: any) => {
        this.dataSource = res.data || [];
        this.totalRecords = res.recordsTotal || 0;
        this.isLoading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  triggerSearch() {
    const input = document.getElementById('searchKeyword') as HTMLInputElement;
    this.keyword = input?.value || '';
    if (this.paginator) { this.paginator.pageIndex = 0; }
    this.pageIndex = 0;
    this.loadData();
  }

  refreshTable() {
    if (this.paginator) { this.paginator.pageIndex = 0; }
    this.pageIndex = 0;
    this.loadData();
  }

  isAllSelected() {
    return this.selection.selected.length === this.dataSource.length;
  }

  masterToggle() {
    if (this.isAllSelected()) {
      this.selection.clear();
    } else {
      this.dataSource.forEach(row => this.selection.select(row));
    }
  }

  openAddModal() {
    this.editMode.set(false);
    this.currentId.set(null);
    this.dataForm.reset();
    this.showModal.set(true);
  }

  openEditModal(id: string | number) {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    this.roleService.getById(String(id)).subscribe((record: any) => {
      if (record) {
        this.editMode.set(true);
        this.currentId.set(String(id));
        this.dataForm.patchValue({
          Name: record.name || record.Name || '',
          Code: record.code || record.Code || '',
          App:  record.app  || record.App  || ''
        });
        this.showModal.set(true);
      }
    });
  }

  closeModal() {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit() {
    if (!this.dataForm.valid) return;
    const raw = this.dataForm.getRawValue();
    const payload: Partial<Role> = { name: raw.Name, code: raw.Code, app: raw.App || undefined };

    if (this.editMode() && this.currentId()) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const updatePayload: any = { id: this.currentId(), publicId: this.currentId(), ...payload };
      this.roleService.update(updatePayload).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.loadData(); // sửa: giữ nguyên trang hiện tại
          this.closeModal();
        },
        error: () => {}
      });
    } else {
      this.roleService.create(payload as Role).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        },
        error: () => {}
      });
    }
  }

  handleDelete(id: string | number) {
    this.confirmDeleteId.set(String(id));
    this.showConfirmDelete.set(true);
  }

  deleteSelected() {
    if (this.selection.selected.length === 0) return;
    this.confirmDeleteId.set(null);
    this.showConfirmDelete.set(true);
  }

  closeConfirm() {
    this.showConfirmDelete.set(false);
    this.confirmDeleteId.set(null);
  }

  confirmActionExecute() {
    const id = this.confirmDeleteId();

    if (id === null) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const ids = this.selection.selected.map((item: any) => item.publicId || item.id || item.Id);
      forkJoin(ids.map(i => this.roleService.delete(String(i)))).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.selection.clear();
          this.refreshTable();
          this.closeConfirm();
        },
        error: () => {
          this.refreshTable();
          this.closeConfirm();
        }
      });
      return;
    }

    this.roleService.delete(id).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        this.refreshTable();
        this.closeConfirm();
      },
      error: () => {
        this.closeConfirm();
      }
    });
  }
}
