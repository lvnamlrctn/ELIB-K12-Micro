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
import { BannerService } from '../../../services/cms/banner';

import { Banner } from '../../../models/cms/banner';

import { environment } from '../../../../environments/environment';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-banner',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './banner.html'
})
export class BannerPage implements OnInit, OnDestroy, AfterViewInit {
  private bannerService = inject(BannerService);
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
    Link: new FormControl('', { nonNullable: true }),
    Url: new FormControl('', { nonNullable: true }),
    SortOrder: new FormControl<number | null>(null),
    Width: new FormControl<number | null>(null),
    Height: new FormControl<number | null>(null),
    Status: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] })
  });

  // Table State
  displayedColumns: string[] = ['select', 'id', 'url', 'name', 'link', 'status', 'actions'];

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
  dataSource = new MatTableDataSource<Banner>([]);
  selection = new SelectionModel<Banner>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';
  selectedImageBase64 = signal<string>('');
  selectedImageFile = signal<File | undefined>(undefined);

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    this.applyTenantColumn();
    this.loadTenants();
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
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
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const params: any = {
      draw: this.drawCount,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.searchTerm },
      tenantId: this.tenantId,
      order: []
    };

    this.isLoading.set(true);
    this.bannerService.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.dataSource.data = res.data;
        this.totalRecords = res.recordsTotal;
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading data', err);
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
    this.selectedImageBase64.set('');
    this.selectedImageFile.set(undefined);
    this.dataForm.reset({
      Name: '',
      Link: '',
      Url: '',
      SortOrder: null,
      Width: null,
      Height: null,
      Status: true
    });
    this.showModal.set(true);
  }

  async openEditModal(id: string | number) {
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const record = await firstValueFrom(this.bannerService.getById(String(id))) as any;
      if (record) {
        this.editMode.set(true);
        this.currentId.set(id);
        
        const nameVal = record.name || record.Name || '';
        const linkVal = record.link || record.Link || '';
        const sortOrderVal = record.sortOrder || record.SortOrder;
        const widthVal = record.width || record.Width;
        const heightVal = record.height || record.Height;

        const imageVal = record.url || record.Url || '';
        const statusVal = record.status ?? record.Status ?? 1;
        
        this.selectedImageBase64.set(imageVal);
        this.selectedImageFile.set(undefined);

        this.dataForm.patchValue({
          Name: nameVal,
          Link: linkVal,
          SortOrder: sortOrderVal,
          Width: widthVal,
          Height: heightVal,
          Url: imageVal,
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

  get displayImage(): string {
    const val = this.selectedImageBase64();
    if (!val) return '';
    if (val.startsWith('data:image') || val.startsWith('http')) {
      return val;
    }
    const path = val.startsWith('/') ? val : '/' + val;
    return `${environment.imageUrl}${path}`;
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getImageUrl(element: any): string {
    // Dùng thẳng URL ảnh do API trả về (giống ảnh bìa OPAC)
    return element.url || element.Url || '';
  }

  onFileSelected(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) {
      this.selectedImageFile.set(file);
      const reader = new FileReader();
      reader.onload = () => {
        this.selectedImageBase64.set(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  }

  onSubmit() {
    if (this.dataForm.valid) {
      const rawValues = this.dataForm.getRawValue();
      const payload: Partial<Banner> = {
        name: rawValues.Name,
        link: rawValues.Link,
        sortOrder: rawValues.SortOrder || undefined,
        width: rawValues.Width || undefined,
        height: rawValues.Height || undefined,
        url: rawValues.Url,
        status: rawValues.Status ? 2 : 1
      };
      
      if (this.editMode() && this.currentId()) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const updatePayload: any = {
          id: String(this.currentId()),
          ...payload,
          publicId: String(this.currentId()),
        };
        this.bannerService.update(updatePayload, this.selectedImageFile()).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        });
      } else {
        this.bannerService.create(payload as Banner, this.selectedImageFile()).subscribe(() => {
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
      this.bannerService.changeStatus(publicId, !currentStatus).subscribe({
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
      const requests = selectedIds.map(selectedId => this.bannerService.delete(String(selectedId)));
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
      this.bannerService.delete(String(id)).subscribe({
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
}
