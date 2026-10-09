import { Component, inject, OnInit, OnDestroy, Input, signal, ViewChild, AfterViewInit } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { firstValueFrom, Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { DicCodeDescService } from '../../../services/cataloging/dic-code-desc.service';
import { DataTableParams } from '../../../models/shared/datatable';
import { CanDirective } from '../../../directives/can.directive';
import { FormsModule } from '@angular/forms';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-dic-code-desc',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, CanDirective, NgSelectModule],
  templateUrl: './dic-code-desc.html'
})
export class DicCodeDescComponent implements OnInit, OnDestroy, AfterViewInit {
  @Input() service!: DicCodeDescService;
  @Input() translationKeyPrefix!: string;

  public translate = inject(TranslateService);
  private toastr = inject(ToastrService);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$ = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;
  
  private currentLang = '';

  showModal = signal<boolean>(false);
  showConfirmDelete = signal<boolean>(false);
  showImportModal = signal<boolean>(false);
  editMode = signal<boolean>(false);
  itemToDelete = signal<string | null>(null);
  currentId = signal<string | null>(null);
  currentItem = signal<any>(null);
  importFile = signal<File | null>(null);
  isLoading = signal<boolean>(false);

  dataForm = new FormGroup({
    Code: new FormControl('', [Validators.required]),
    VnDescription: new FormControl('', [Validators.required]),
    Description: new FormControl('', [Validators.required])
  });

  displayedColumns: string[] = this.auth.isPrivileged()
    ? ['select', 'code', 'vnDescription', 'description', 'tenant', 'actions']
    : ['select', 'code', 'vnDescription', 'description', 'actions'];
  dataSource = new MatTableDataSource<any>([]);
  selection = new SelectionModel<any>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(event => {
      this.currentLang = event.lang;
      this.loadData();
    });
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }
  }

  onTenantChange() {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
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
      order: [],
      tenantId: this.tenantId,
    };

    this.isLoading.set(true);
    this.service.getAll(params, this.translate.currentLang || '').pipe(takeUntil(this.destroy$)).subscribe({
      next: (response) => {
        this.dataSource.data = response.data;
        this.totalRecords = response.recordsTotal;
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error fetching data', err);
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
    this.searchTerm = (document.getElementById('searchCodeDesc') as HTMLInputElement)?.value || '';
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
    this.itemToDelete.set('bulk');
    this.showConfirmDelete.set(true);
  }

  openAddModal() {
    this.editMode.set(false);
    this.currentId.set(null);
    this.currentItem.set(null);
    this.dataForm.reset();
    this.showModal.set(true);
  }

  async openEditModal(id: string) {
    try {
      const item = await firstValueFrom(this.service.getById(id));
      this.currentItem.set(item);
      this.currentId.set(item.publicId || item.id || item.Id || id);
      this.dataForm.patchValue({
        Code: item.code || item.Code,
        VnDescription: item.vnDescription || item.VnDescription,
        Description: item.description || item.Description
      });
      this.editMode.set(true);
      this.showModal.set(true);
    } catch (error) {
      console.error('Error fetching data details', error);
      this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
    }
  }

  closeModal() {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit() {
    if (this.dataForm.invalid) return;

    const payload = {
      Code: this.dataForm.value.Code,
      VnDescription: this.dataForm.value.VnDescription,
      Description: this.dataForm.value.Description
    };

    if (this.editMode() && this.currentId()) {
      const current = this.currentItem() || {};
      const updatePayload = {
        ...current,
        ...payload
      };

      this.service.update(this.currentId()!, updatePayload).subscribe({
        next: () => {
          this.closeModal();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
        },
        error: (err) => {
          console.error('Update failed', err);
        }
      });
    } else {
      this.service.create(payload).subscribe({
        next: () => {
          this.closeModal();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
        },
        error: (err) => {
          console.error('Create failed', err);
        }
      });
    }
  }

  confirmDelete(id: string) {
    this.itemToDelete.set(id);
    this.showConfirmDelete.set(true);
  }

  closeConfirm() {
    this.showConfirmDelete.set(false);
    this.itemToDelete.set(null);
  }

  confirmActionExecute() {
    const id = this.itemToDelete();
    if (id === 'bulk') {
      const selectedIds = this.selection.selected.map((item: any) => item.publicId || item.id || item.Id);
      const requests = selectedIds.map(selectedId => this.service.delete(selectedId));
      forkJoin(requests).subscribe({
        next: () => {
          this.closeConfirm();
          this.loadData();
          this.selection.clear();
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        },
        error: (err) => {
          console.error('Delete failed', err);
          this.closeConfirm();
        }
      });
      return;
    }

    if (id) {
      this.service.delete(id).subscribe({
        next: () => {
          this.closeConfirm();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        },
        error: (err) => {
          console.error('Delete failed', err);
          this.closeConfirm();
        }
      });
    }
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
    this.service.import(file).subscribe({
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
