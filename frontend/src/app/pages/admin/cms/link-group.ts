import { Component, inject, OnInit, OnDestroy, signal, ViewChild, AfterViewInit } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom, Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { LinkGroupService } from '../../../services/cms/link-group';
import { LinkGroup } from '../../../models/cms/link-group';
import { DataTableParams } from '../../../models/shared/datatable';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-link-group',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './link-group.html'
})
export class LinkGroupPage implements OnInit, OnDestroy, AfterViewInit {
  private linkGroupService = inject(LinkGroupService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$ = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  // Modal State
  showModal = signal<boolean>(false);
  showConfirm = signal<boolean>(false);
  showImportModal = signal<boolean>(false);
  pendingId = signal<string | number | null>(null);
  editMode = signal<boolean>(false);
  currentId = signal<string | number | null>(null);
  importFile = signal<File | null>(null);
  isLoading = signal<boolean>(false);

  // Form
  dataForm = new FormGroup({
    Name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    Status: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] })
  });

  // Table State
  displayedColumns: string[] = ['select', 'id', 'name', 'status', 'actions'];

  private applyTenantColumn(): void {
    if (this.isPrivileged && !this.displayedColumns.includes('tenant')) {
      this.displayedColumns.splice(this.displayedColumns.length - 1, 0, 'tenant');
    }
  }

  private loadTenants(): void {
    if (!this.isPrivileged) return;
    this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
      .pipe(takeUntil(this.destroy$))
      .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
  }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }
  dataSource = new MatTableDataSource<LinkGroup>([]);
  selection = new SelectionModel<LinkGroup>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    this.applyTenantColumn();
    this.loadTenants();
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      // Refresh texts inside component if needed
      this.loadData();
    });
  }

  ngAfterViewInit() {
    this.loadData();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    this.drawCount++;
    const params: DataTableParams = {
      draw: this.drawCount,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.searchTerm },
      tenantId: this.tenantId,
      order: []
    };

    this.isLoading.set(true);
    this.linkGroupService.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.dataSource.data = res.data;
        this.totalRecords = res.recordsTotal;
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading data', err);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  triggerSearch() {
    this.searchTerm = (document.getElementById('searchName') as HTMLInputElement).value;
    this.pageIndex = 0;
    if (this.paginator) {
      this.paginator.pageIndex = 0;
    }
    this.selection.clear();
    this.loadData();
  }

  isAllSelected() {
    const numSelected = this.selection.selected.length;
    const numRows = this.dataSource.data.length;
    return numSelected > 0 && numSelected === numRows;
  }

  toggleAllRows() {
    if (this.isAllSelected()) {
      this.selection.clear();
      return;
    }
    this.selection.select(...this.dataSource.data);
  }

  deleteSelected() {
    if (this.selection.selected.length === 0) return;
    this.pendingId.set('bulk');
    this.showConfirm.set(true);
  }

  openAddModal() {
    this.editMode.set(false);
    this.currentId.set(null);
    this.dataForm.reset({
      Name: '',
      Status: true
    });
    this.showModal.set(true);
  }

  async openEditModal(id: string | number) {
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const record = await firstValueFrom(this.linkGroupService.getById(String(id))) as any;
      if (record) {
        this.editMode.set(true);
        this.currentId.set(id);
        
        // Handle variations of keys (Name/name, Status/status)
        const nameVal = record.name || record.Name || '';
        const statusVal = record.status ?? record.Status ?? 1;
        
        this.dataForm.patchValue({
          Name: nameVal,
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          Status: statusVal === 2 || (statusVal as any) === true
        });
        
        this.showModal.set(true);
      }
    } catch(err) {
      console.error(err);
    }
  }

  closeModal() {
    this.showModal.set(false);
  }

  onSubmit() {
    if (this.dataForm.valid) {
      const rawValues = this.dataForm.getRawValue();
      const payload: Partial<LinkGroup> = {
        name: rawValues.Name,
        status: rawValues.Status ? 2 : 1
      };
      
      if (this.editMode() && this.currentId()) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const updatePayload: any = {
          id: String(this.currentId()),
          name: payload.name || '',
          publicId: String(this.currentId()),
          status: payload.status
        };
        this.linkGroupService.update(updatePayload).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        });
      } else {
        this.linkGroupService.create(payload as LinkGroup).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        });
      }
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  toggleStatus(element: any) {
    const publicId = element.publicId || element.id || element.Id;
    const currentStatus = element.status === 2 || element.Status === 2 || element.status === true || element.Status === true;
    if (publicId) {
      this.linkGroupService.changeStatus(publicId, !currentStatus).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.loadData();
        },
        error: (err) => {
          console.error('Error toggling status', err);
          this.loadData(); // Revert back display
        }
      });
    }
  }

  handleDelete(id: string | number) {
    this.pendingId.set(id);
    this.showConfirm.set(true);
  }

  confirmActionExecute() {
    const id = this.pendingId();
    if (id === 'bulk') {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const selectedIds = this.selection.selected.map((item: any) => item.publicId || item.id || item.Id);
      const requests = selectedIds.map(selectedId => this.linkGroupService.delete(String(selectedId)));
      forkJoin(requests).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.refreshTable();
          this.selection.clear();
          this.closeConfirm();
        },
        error: (err) => {
          console.error(err);
          this.refreshTable();
          this.closeConfirm();
        }
      });
      return;
    }

    if (id) {
      this.linkGroupService.delete(String(id)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.refreshTable();
          this.closeConfirm();
        },
        error: (err) => {
          console.error(err);
          this.closeConfirm();
        }
      });
    }
  }

  closeConfirm() {
    this.showConfirm.set(false);
    this.pendingId.set(null);
  }

  public refreshTable() {
    this.loadData();
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.importFile.set(input.files[0]);
    } else {
      this.importFile.set(null);
    }
  }

  openImportModal() {
    this.importFile.set(null);
    this.showImportModal.set(true);
  }

  closeImportModal() {
    this.showImportModal.set(false);
    this.importFile.set(null);
  }

  executeImport() {
    const file = this.importFile();
    if (!file) return;

    this.isLoading.set(true);
    this.linkGroupService.import(file).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('COMMON.IMPORT_SUCCESS'));
        this.loadData();
        this.closeImportModal();
      },
      error: (err) => {
        console.error('Import failed', err);
        this.isLoading.set(false);
      }
    });
  }
}

